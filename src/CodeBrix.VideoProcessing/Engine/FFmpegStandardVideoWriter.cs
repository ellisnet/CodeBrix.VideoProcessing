using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;
using CodeBrix.VideoProcessing.Exceptions;

namespace CodeBrix.VideoProcessing.Engine;

/// <summary>
/// Writes a standard MP4 through ONE ffmpeg process fed by TWO named pipes - raw video and, when the request has
/// sound, <c>f32le</c> audio - built with the ordinary fluent <see cref="FFMpegArguments" /> builder.
/// </summary>
/// <remarks>
/// <para>
/// Each WriteFrame / WriteAudio copies the caller's data into a pooled buffer and queues it for that stream's
/// pipe, so the caller may reuse its memory at once. BACK-PRESSURE: a call waits while its own stream has more
/// than a small backlog queued AND the other stream (if any) has been fed at least as far in media time, because
/// only then can ffmpeg be relied on to drain this stream. If the other stream lags, the call does not wait - ffmpeg
/// may be waiting for that stream - and the backlog simply grows; this is why the contract asks callers to feed
/// sound and pictures roughly in step. A call never waits more than a few seconds without seeing progress, so a
/// misjudgement costs memory, never a deadlock.
/// </para>
/// <para>
/// Finish completes both queues (closing the pipes, which ffmpeg sees as end-of-file) and waits for ffmpeg. Dispose
/// without Finish cancels ffmpeg and deletes the output file.
/// </para>
/// </remarks>
internal sealed class FFmpegStandardVideoWriter : IStandardVideoWriter
{
    private const int VideoBacklogFrames = 8;
    private static readonly TimeSpan AudioBacklog = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan OtherStreamStarvedAfter = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaximumWaitWithoutProgress = TimeSpan.FromSeconds(10);

    private readonly object gate = new object();
    private readonly QueuedPipeSource videoSource;
    private readonly QueuedPipeSource audioSource;
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private readonly IProgress<VideoProgress> progress;
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private readonly Task processing;
    private readonly int frameBytes;
    private readonly int audioChunkLimit;

    private long audioChunks;
    private bool finished;
    private bool disposed;

    internal FFmpegStandardVideoWriter(StandardVideoRequest request, FFOptions options, IProgress<VideoProgress> progress)
    {
        Request = request;
        this.progress = progress;

        int width = request.Width;
        int height = request.Height;
        int chromaSize = ((width + 1) / 2) * ((height + 1) / 2);
        string inputPixelFormat;
        switch (request.InputPixelLayout)
        {
            case VideoPixelLayout.Rgba:
                inputPixelFormat = "rgba";
                frameBytes = 4 * width * height;
                break;
            case VideoPixelLayout.Nv12:
                inputPixelFormat = "nv12";
                frameBytes = width * height + 2 * chromaSize;
                break;
            case VideoPixelLayout.I420:
                inputPixelFormat = "yuv420p";
                frameBytes = width * height + 2 * chromaSize;
                break;
            default:
                // StandardVideoRequest.Validate refuses these already; never guess a format for them.
                throw new ArgumentException(
                    $"The standard video writer takes 8-bit I420, NV12 or RGBA frames; {request.InputPixelLayout} is not accepted.",
                    nameof(request));
        }

        videoSource = new QueuedPipeSource(
            $"-thread_queue_size 64 -f rawvideo -pix_fmt {inputPixelFormat} -s {width}x{height} -framerate {request.FrameRate}");
        videoSource.ChunkConsumed += OnChunkConsumed;

        // One audio chunk per WriteAudio call; the backlog limit is counted in chunks of a typical size.
        audioChunkLimit = Math.Max(4, (int)(AudioBacklog.TotalSeconds * request.FrameRate.FramesPerSecond));
        if (request.HasAudio)
        {
            audioSource = new QueuedPipeSource(
                $"-thread_queue_size 64 -f f32le -ar {request.AudioSampleRate.ToString(CultureInfo.InvariantCulture)} -ac {request.AudioChannels.ToString(CultureInfo.InvariantCulture)}");
            audioSource.ChunkConsumed += OnChunkConsumed;
        }

        FFMpegArguments arguments = FFMpegArguments.FromPipeInput(videoSource, input =>
        {
            input.AutoRotate(false);
            if (request.RotationDegrees != 0)
            {
                // -display_rotation is counter-clockwise; the request is clockwise.
                input.WithCustomArgument("-display_rotation:v:0 " + (-request.RotationDegrees).ToString(CultureInfo.InvariantCulture));
            }
        });
        if (audioSource != null) arguments = arguments.AddPipeInput(audioSource);

        FFMpegArgumentProcessor processor = arguments
            .OutputToFile(request.OutputPath, overwrite: true, output => FFmpegOutputOptions.Apply(output, request, includeMaps: true, isTranscode: false))
            .CancellableThrough(cancellation.Token);

        processing = Task.Run(() => processor.ProcessAsynchronously(true, options));
    }

    public StandardVideoRequest Request { get; }

    public long FramesWritten { get; private set; }

    public long AudioFramesWritten { get; private set; }

    public void WriteFrame(in VideoFrameData frame)
    {
        ThrowIfUnusable();
        if (frame.Width != Request.Width || frame.Height != Request.Height)
            throw new ArgumentException($"The frame is {frame.Width}x{frame.Height}; this writer was created for {Request.Width}x{Request.Height}.", nameof(frame));
        if (frame.Layout != Request.InputPixelLayout)
            throw new ArgumentException($"The frame is {frame.Layout}; this writer was created for {Request.InputPixelLayout} frames.", nameof(frame));
        if (frame.BitDepth != 8)
            throw new ArgumentException($"The frame is {frame.BitDepth}-bit; the standard video writer takes 8-bit frames only.", nameof(frame));

        byte[] buffer = ArrayPool<byte>.Shared.Rent(frameBytes);
        Span<byte> destination = buffer.AsSpan(0, frameBytes);
        switch (frame.Layout)
        {
            case VideoPixelLayout.Rgba:
                frame.Rgba.CopyTo(destination);
                break;
            case VideoPixelLayout.Nv12:
                int lumaNv12 = frame.Y.RowLength * frame.Y.Rows;
                frame.Y.CopyTo(destination);
                frame.UV.CopyTo(destination.Slice(lumaNv12));
                break;
            default:
                int luma = frame.Y.RowLength * frame.Y.Rows;
                int chroma = frame.U.RowLength * frame.U.Rows;
                frame.Y.CopyTo(destination);
                frame.U.CopyTo(destination.Slice(luma));
                frame.V.CopyTo(destination.Slice(luma + chroma));
                break;
        }

        WaitForRoom(isVideo: true);
        videoSource.Enqueue(buffer, frameBytes);
        FramesWritten++;

        progress?.Report(new VideoProgress(null, FramesWritten, Request.FrameRate.TimestampOf(FramesWritten), stopwatch.Elapsed));
    }

    public void WriteAudio(ReadOnlySpan<float> interleaved)
    {
        ThrowIfUnusable();
        if (audioSource == null) throw new InvalidOperationException("This writer was created without audio (StandardVideoRequest.HasAudio is false).");
        if (interleaved.Length % Request.AudioChannels != 0)
            throw new ArgumentException($"The sample count must be a multiple of the channel count ({Request.AudioChannels}).", nameof(interleaved));
        if (interleaved.Length == 0) return;

        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(interleaved);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bytes.Length);
        bytes.CopyTo(buffer);

        WaitForRoom(isVideo: false);
        audioSource.Enqueue(buffer, bytes.Length);
        audioChunks++;
        AudioFramesWritten += interleaved.Length / Request.AudioChannels;
    }

    public void Finish()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (finished) return;
        if (FramesWritten == 0) throw new InvalidOperationException("No video frame has been written; an MP4 needs at least one.");

        finished = true;
        videoSource.Complete();
        audioSource?.Complete();

        try
        {
            processing.GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is FFMpegException or IOException or OperationCanceledException)
        {
            TryDelete(Request.OutputPath);
            throw new VideoProcessingException($"ffmpeg failed to write '{Request.OutputPath}': {exception.Message}", exception);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        bool completed = finished && processing.IsCompletedSuccessfully;
        if (!completed)
        {
            cancellation.Cancel();
            videoSource.Abandon();
            audioSource?.Abandon();
            try
            {
                processing.Wait(TimeSpan.FromSeconds(30));
            }
            catch (AggregateException)
            {
            }

            TryDelete(Request.OutputPath);
        }

        cancellation.Dispose();
    }

    private void ThrowIfUnusable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (finished) throw new InvalidOperationException("The writer has been finished.");
        if (processing.IsCompleted)
        {
            Exception failure = processing.Exception?.GetBaseException();
            throw new VideoProcessingException(
                $"ffmpeg stopped before the video was finished: {failure?.Message ?? "it exited early."}",
                failure);
        }
    }

    // See the class remarks. Waits while THIS stream is backed up, unless ffmpeg looks to be waiting for the OTHER
    // stream: the other queue is empty and this one has not moved for a moment. A hard limit without progress
    // guarantees the call returns even if that judgement is wrong.
    private void WaitForRoom(bool isVideo)
    {
        QueuedPipeSource own = isVideo ? videoSource : audioSource;
        QueuedPipeSource other = isVideo ? audioSource : videoSource;
        int limit = isVideo ? VideoBacklogFrames : audioChunkLimit;

        lock (gate)
        {
            long lastConsumed = own.Consumed;
            Stopwatch sinceProgress = Stopwatch.StartNew();

            while (own.Backlog >= limit && !processing.IsCompleted)
            {
                if (other != null && other.Backlog == 0 && sinceProgress.Elapsed > OtherStreamStarvedAfter) break;
                if (sinceProgress.Elapsed > MaximumWaitWithoutProgress) break;

                Monitor.Wait(gate, 20);
                if (own.Consumed != lastConsumed)
                {
                    lastConsumed = own.Consumed;
                    sinceProgress.Restart();
                }
            }
        }
    }

    private void OnChunkConsumed()
    {
        lock (gate) Monitor.PulseAll(gate);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
