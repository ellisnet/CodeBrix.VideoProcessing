using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using global::Android.Media;
using ByteBuffer = global::Java.Nio.ByteBuffer;
using JavaThrowable = global::Java.Lang.Throwable;

namespace CodeBrix.VideoProcessing.Android.Engine;

/// <summary>
/// Decodes the first video stream of a file with MediaExtractor + a MediaCodec decoder (synchronous mode, on the
/// caller's thread) into 8-bit frames, with the same semantics as the desktop engine's frame source.
/// </summary>
/// <remarks>
/// <para>
/// CONSTANT RATE: every decoded picture is copied (through the Image API) into a tightly packed I420 buffer and
/// placed in the frame grid of <see cref="VideoSourceInfo.FrameRate" /> by
/// <see cref="ConstantRateScheduler{T}" /> - nearest slot, repeats for gaps, latest wins - so frame N is always
/// <c>FrameRate.TimestampOf(N)</c> and FrameIndex counts from the start of the stream (slot 0 is the stream's first
/// sample time), also after a seek.
/// </para>
/// <para>
/// LAYOUTS: I420 is the decoded buffer itself; Gray is its Y plane (exactly I420's luma, in the delivered range);
/// NV12 and RGBA are converted from it into one more reused buffer (RGBA with the stream's matrix and range;
/// <see cref="VideoSourceInfo.ColorMatrix" /> then reports <see cref="VideoColorMatrix.Rgb" />, the matrix of the
/// delivered samples, as the contract says). 10-bit (P010) decoder output is cut to its top 8
/// bits and reported through <see cref="VideoSourceInfo.SourceBitDepth" />. Pictures stay in STORED orientation;
/// <see cref="VideoSourceInfo.RotationDegrees" /> says how to display them.
/// </para>
/// <para>
/// OWNERSHIP: three picture buffers circulate (the frame on screen, a look-ahead, one being decoded); a frame is a
/// view over one of them and is valid until the next TryReadFrame, Seek or Dispose, as the contract says.
/// </para>
/// <para>
/// SEEK: MediaExtractor seeks to the sync sample at or before the target, the decoder is flushed, and pictures before
/// the target slot are decoded and dropped by the scheduler.
/// </para>
/// </remarks>
internal sealed class AndroidVideoFrameSource : IVideoFrameSource
{
    private readonly string path;
    private readonly int trackIndex;
    private readonly int width;
    private readonly int height;
    private readonly int pictureSize;
    private readonly long maximumFrames;
    private readonly VideoColorMatrix sourceMatrix;
    private readonly CancellationToken cancellationToken;
    private readonly Stack<byte[]> pool = new Stack<byte[]>();
    private readonly MediaCodec.BufferInfo bufferInfo = new MediaCodec.BufferInfo();

    private MediaExtractor extractor;
    private MediaCodec decoder;
    private MediaFormat outputFormat;
    private ConstantRateScheduler<byte[]> scheduler;
    private byte[] conversion;
    private long originMicroseconds;
    private long delivered;
    private bool inputDone;
    private bool disposed;

    internal AndroidVideoFrameSource(string path, VideoStreamInfo stream, VideoSourceOptions options, long maximumFrames = 0, CancellationToken cancellationToken = default)
    {
        this.path = path;
        this.maximumFrames = maximumFrames;
        this.cancellationToken = cancellationToken;
        trackIndex = stream.Index;
        width = stream.Width;
        height = stream.Height;

        if (width <= 0 || height <= 0) throw new MediaFormatException($"The video stream of '{path}' has no known size.");
        if (!stream.FrameRate.IsSpecified) throw new MediaFormatException($"The video stream of '{path}' has no known frame rate.");

        pictureSize = YuvConversion.I420Size(width, height);
        VideoPixelLayout layout = options.PixelLayout;
        sourceMatrix = stream.ColorMatrix;
        Info = new VideoSourceInfo
        {
            Width = width,
            Height = height,
            FrameRate = stream.FrameRate,
            Duration = stream.Duration,
            EstimatedFrameCount = stream.FrameCount > 0 ? stream.FrameCount : stream.FrameRate.FrameCountFor(stream.Duration),
            PixelLayout = layout,
            BitDepth = 8,
            SourceBitDepth = stream.BitDepth,
            SourceHasAlpha = false,
            HasAlpha = layout == VideoPixelLayout.Rgba,
            RotationDegrees = stream.RotationDegrees,
            ColorRange = stream.ColorRange == VideoColorRange.Full ? VideoColorRange.Full : VideoColorRange.Limited,
            // The matrix of the DELIVERED samples: the stream's for YUV / Gray, Rgb for RGBA (those samples are RGB).
            ColorMatrix = layout == VideoPixelLayout.Rgba ? VideoColorMatrix.Rgb : stream.ColorMatrix
        };

        try
        {
            extractor = AndroidMedia.OpenExtractor(path);
            extractor.SelectTrack(trackIndex);
            MediaFormat format = extractor.GetTrackFormat(trackIndex);
            extractor.SeekTo(0, MediaExtractorSeekTo.PreviousSync);
            originMicroseconds = Math.Max(0, extractor.SampleTime);
            decoder = AndroidMedia.CreateDecoder(format, path, AndroidMedia.GetString(format, MediaFormatKeys.Mime) ?? "video");
            outputFormat = decoder.OutputFormat;
        }
        catch (JavaThrowable exception)
        {
            Release();
            throw new VideoProcessingException($"The video of '{path}' could not be opened for decoding: {AndroidMedia.Describe(exception)}", exception);
        }
        catch
        {
            Release();
            throw;
        }

        scheduler = new ConstantRateScheduler<byte[]>(Info.FrameRate, originMicroseconds, 0, pool.Push);
        Seek(options.StartTime);
    }

    public VideoSourceInfo Info { get; }

    public bool CanSeek => true;

    public bool TryReadFrame(out VideoFrameData frame)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        frame = default;
        if (maximumFrames > 0 && delivered >= maximumFrames) return false;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (scheduler.TryDeliver(out byte[] picture, out long slot))
                {
                    delivered++;
                    frame = BuildFrame(picture, slot);
                    return true;
                }

                if (!scheduler.NeedsInput) return false;
                DecodeOne();
            }
        }
        catch (JavaThrowable exception)
        {
            throw new VideoProcessingException($"Decoding the video of '{path}' failed: {AndroidMedia.Describe(exception)}", exception);
        }
    }

    public void Seek(TimeSpan position)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (position < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(position), position, "The position cannot be negative.");

        FrameRate rate = Info.FrameRate;
        long slot = rate.FrameIndexAt(position);
        if (rate.TimestampOf(slot) < position) slot++;

        try
        {
            long seekMicroseconds = originMicroseconds + MediaFormatMapping.ToMicroseconds(rate.TimestampOf(slot));
            extractor.SeekTo(seekMicroseconds, MediaExtractorSeekTo.PreviousSync);
            decoder.Flush();
        }
        catch (JavaThrowable exception)
        {
            throw new VideoProcessingException($"Seeking in the video of '{path}' failed: {AndroidMedia.Describe(exception)}", exception);
        }

        inputDone = false;
        scheduler.Reset(slot);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Release();
    }

    private void Release()
    {
        AndroidMedia.ReleaseQuietly(decoder);
        AndroidMedia.ReleaseQuietly(extractor);
        decoder = null;
        extractor = null;
    }

    // Feeds the decoder and waits for ONE output picture (offered to the scheduler) or the end of the stream.
    private void DecodeOne()
    {
        Stopwatch sinceProgress = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!inputDone && FeedInput()) sinceProgress.Restart();

            int index = decoder.DequeueOutputBuffer(bufferInfo, AndroidMedia.DequeueTimeoutMicroseconds);
            if (index >= 0)
            {
                bool endOfStream = (bufferInfo.Flags & MediaCodecBufferFlags.EndOfStream) != 0;
                bool hasPicture = bufferInfo.Size > 0;
                if (hasPicture)
                {
                    byte[] picture = pool.Count > 0 ? pool.Pop() : new byte[pictureSize];
                    try
                    {
                        CopyPicture(index, picture);
                    }
                    catch
                    {
                        pool.Push(picture);
                        decoder.ReleaseOutputBuffer(index, false);
                        throw;
                    }

                    long presentation = bufferInfo.PresentationTimeUs;
                    decoder.ReleaseOutputBuffer(index, false);
                    scheduler.Offer(picture, presentation);
                    if (endOfStream) scheduler.EndOfInput();
                    return;
                }

                decoder.ReleaseOutputBuffer(index, false);
                if (endOfStream)
                {
                    scheduler.EndOfInput();
                    return;
                }

                sinceProgress.Restart();
            }
            else if (index == (int)MediaCodecInfoState.OutputFormatChanged)
            {
                outputFormat = decoder.OutputFormat;
                sinceProgress.Restart();
            }

            if (sinceProgress.Elapsed > AndroidMedia.StallTimeout)
            {
                throw new VideoProcessingException(
                    $"The video decoder for '{path}' stopped producing pictures (no progress for {AndroidMedia.StallTimeout.TotalSeconds:0} s).");
            }
        }
    }

    // Queues one compressed sample (or the end-of-stream marker) when the decoder has a free input buffer.
    private bool FeedInput()
    {
        int index = decoder.DequeueInputBuffer(0);
        if (index < 0) return false;

        ByteBuffer input = decoder.GetInputBuffer(index);
        int size = extractor.ReadSampleData(input, 0);
        if (size < 0)
        {
            decoder.QueueInputBuffer(index, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
            inputDone = true;
            return true;
        }

        decoder.QueueInputBuffer(index, 0, size, extractor.SampleTime, MediaCodecBufferFlags.None);
        extractor.Advance();
        return true;
    }

    private void CopyPicture(int index, byte[] picture)
    {
        using (global::Android.Media.Image image = decoder.GetOutputImage(index))
        {
            if (image != null)
            {
                AndroidImageCopy.ReadI420(image, width, height, picture);
                return;
            }
        }

        ByteBuffer buffer = decoder.GetOutputBuffer(index);
        MediaFormat format = decoder.GetOutputFormat(index) ?? outputFormat;
        AndroidImageCopy.ReadI420FromBuffer(buffer, bufferInfo.Offset, format, width, height, picture);
    }

    private VideoFrameData BuildFrame(byte[] picture, long slot)
    {
        TimeSpan timestamp = Info.FrameRate.TimestampOf(slot);
        int luma = width * height;
        int chromaWidth = (width + 1) / 2;
        int chromaHeight = (height + 1) / 2;
        int chroma = chromaWidth * chromaHeight;
        var memory = new ReadOnlyMemory<byte>(picture);
        VideoPlane y = new VideoPlane(memory.Slice(0, luma), width, width, height);

        switch (Info.PixelLayout)
        {
            case VideoPixelLayout.Gray:
                return VideoFrameData.CreateGray(width, height, y, timestamp, slot);
            case VideoPixelLayout.Nv12:
                conversion ??= new byte[pictureSize];
                YuvConversion.I420ToNv12(picture, width, height, conversion);
                return VideoFrameData.CreateNv12(width, height,
                    new VideoPlane(new ReadOnlyMemory<byte>(conversion, 0, luma), width, width, height),
                    new VideoPlane(new ReadOnlyMemory<byte>(conversion, luma, 2 * chroma), 2 * chromaWidth, 2 * chromaWidth, chromaHeight),
                    timestamp, slot);
            case VideoPixelLayout.Rgba:
                conversion ??= new byte[4 * luma];
                YuvConversion.I420ToRgba(picture, width, height, sourceMatrix, Info.ColorRange, conversion);
                return VideoFrameData.CreateRgba(width, height, new VideoPlane(conversion, 4 * width, 4 * width, height), timestamp, slot);
            default:
                return VideoFrameData.CreateI420(width, height, y,
                    new VideoPlane(memory.Slice(luma, chroma), chromaWidth, chromaWidth, chromaHeight),
                    new VideoPlane(memory.Slice(luma + chroma, chroma), chromaWidth, chromaWidth, chromaHeight),
                    timestamp, slot);
        }
    }
}
