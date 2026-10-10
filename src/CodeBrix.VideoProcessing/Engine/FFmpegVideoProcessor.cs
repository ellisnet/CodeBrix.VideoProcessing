using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;
using CodeBrix.VideoProcessing.Exceptions;

namespace CodeBrix.VideoProcessing.Engine;

/// <summary>
/// The ffmpeg engine for the CodeBrix.VideoProcessing.Core contract: an <see cref="IVideoProcessor" /> that drives
/// the external <c>ffmpeg</c> and <c>ffprobe</c> executables. For Windows, Linux and macOS.
/// </summary>
/// <remarks>
/// <para>
/// Applications normally do not create one: they call <see cref="CodeBrixVideoProcessing.Register()" /> once at
/// start-up and code against <see cref="VideoProcessors.Current" />. Create one directly only to give it its own
/// <see cref="FFOptions" /> (for example a different binary folder).
/// </para>
/// <para>
/// CAPABILITIES: everything except <see cref="VideoProcessorCapabilities.Nv12Input" />. Frame sources deliver I420,
/// I444 and Gray at 8, 10 or 12 bits (with or without alpha - except 12-bit I420 with alpha, which ffmpeg has no
/// format for and which <see cref="OpenVideo" /> refuses with <see cref="NotSupportedException" />) and 8-bit RGBA.
/// The writer takes 8-bit I420 or RGBA frames only. HEVC output needs an ffmpeg
/// built with libx265 (H.264 needs libx264); an ffmpeg without them fails the write with a
/// <see cref="VideoProcessingException" /> naming the missing encoder.
/// </para>
/// <para>
/// HOW: <see cref="Analyse" /> runs ffprobe; frame and sample sources and <see cref="Snapshot" /> run ffmpeg
/// decoding to raw video / f32le audio on its standard output; <see cref="CreateWriter" /> runs one ffmpeg fed through
/// two named pipes (raw video + f32le audio) producing libx264 / libx265 + AAC in MP4; the direct operations are
/// single ffmpeg runs built with <see cref="FFMpegArguments" />.
/// </para>
/// </remarks>
public sealed class FFmpegVideoProcessor : IVideoProcessor
{
    /// <summary>What this engine supports: every capability except <see cref="VideoProcessorCapabilities.Nv12Input" />.</summary>
    public const VideoProcessorCapabilities SupportedCapabilities =
        VideoProcessorCapabilities.AlphaFrameSource |
        VideoProcessorCapabilities.RgbaInput |
        VideoProcessorCapabilities.HevcOutput |
        VideoProcessorCapabilities.DirectTranscode |
        VideoProcessorCapabilities.DirectTrim |
        VideoProcessorCapabilities.DirectConcatenate |
        VideoProcessorCapabilities.Snapshot |
        VideoProcessorCapabilities.I444FrameSource |
        VideoProcessorCapabilities.GrayFrameSource |
        VideoProcessorCapabilities.HighBitDepthFrameSource;

    private readonly FFOptions options;

    /// <summary>Creates an engine that uses <see cref="GlobalFFOptions.Current" /> (read at each operation).</summary>
    public FFmpegVideoProcessor()
        : this(null)
    {
    }

    /// <summary>Creates an engine with its own ffmpeg options.</summary>
    /// <param name="options">The options to use; null means <see cref="GlobalFFOptions.Current" />, read at each operation.</param>
    public FFmpegVideoProcessor(FFOptions options)
    {
        this.options = options;
    }

    /// <summary>"ffmpeg".</summary>
    public string Name => "ffmpeg";

    /// <summary>Every capability except <see cref="VideoProcessorCapabilities.Nv12Input" />; see <see cref="SupportedCapabilities" />.</summary>
    public VideoProcessorCapabilities Capabilities => SupportedCapabilities;

    /// <inheritdoc />
    public MediaInfo Analyse(string path, CancellationToken cancellationToken = default) =>
        AnalyseAsync(path, cancellationToken).GetAwaiter().GetResult();

    /// <inheritdoc />
    public Task<MediaInfo> AnalyseAsync(string path, CancellationToken cancellationToken = default) =>
        FFmpegProbe.AnalyseAsync(path, Options, cancellationToken);

    /// <inheritdoc />
    public VideoFrameData Snapshot(string path, TimeSpan at, CancellationToken cancellationToken = default)
    {
        this.RequireCapability(VideoProcessorCapabilities.Snapshot, nameof(Snapshot));
        if (at < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(at), at, "The position cannot be negative.");

        FFOptions current = Options;
        VideoStreamInfo stream = RequireVideo(path, current, cancellationToken);
        var sourceOptions = new VideoSourceOptions { StartTime = at, IncludeAlpha = true };

        using var source = new FFmpegVideoFrameSource(path, stream, sourceOptions, current, maximumFrames: 1);
        using CancellationTokenRegistration registration = cancellationToken.Register(source.Dispose);
        try
        {
            if (!source.TryReadFrame(out VideoFrameData frame))
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new MediaFormatException($"'{path}' has no frame at or after {at}.");
            }

            return frame.Copy();
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    /// <inheritdoc />
    public IVideoFrameSource OpenVideo(string path, VideoSourceOptions options = null)
    {
        options ??= new VideoSourceOptions();
        options.Validate();
        if (options.PixelLayout == VideoPixelLayout.Nv12) this.RequireCapability(VideoProcessorCapabilities.Nv12Input, "NV12 frames");
        if (options.PixelLayout == VideoPixelLayout.I444) this.RequireCapability(VideoProcessorCapabilities.I444FrameSource, "I444 frames");
        if (options.PixelLayout == VideoPixelLayout.Gray) this.RequireCapability(VideoProcessorCapabilities.GrayFrameSource, "Gray frames");
        if (options.BitDepth > 8) this.RequireCapability(VideoProcessorCapabilities.HighBitDepthFrameSource, $"{options.BitDepth}-bit frames");

        FFOptions current = Options;
        VideoStreamInfo stream = RequireVideo(path, current, CancellationToken.None);
        return new FFmpegVideoFrameSource(path, stream, options, current);
    }

    /// <inheritdoc />
    public IAudioSampleSource OpenAudio(string path, AudioSourceOptions options = null)
    {
        options ??= new AudioSourceOptions();
        options.Validate();

        FFOptions current = Options;
        MediaInfo info = FFmpegProbe.AnalyseAsync(path, current, CancellationToken.None).GetAwaiter().GetResult();
        AudioStreamInfo stream = info.PrimaryAudio ?? throw new MediaFormatException($"'{path}' has no audio stream.");
        return new FFmpegAudioSampleSource(path, stream, options, current);
    }

    /// <inheritdoc />
    public IStandardVideoWriter CreateWriter(StandardVideoRequest request, IProgress<VideoProgress> progress = null)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.Validate();
        RequireProfile(request.Profile);
        if (request.InputPixelLayout == VideoPixelLayout.Nv12) this.RequireCapability(VideoProcessorCapabilities.Nv12Input, "NV12 input frames");

        return new FFmpegStandardVideoWriter(request.Clone(), Options, progress);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The pictures keep their stored orientation and the source's rotation metadata is carried over; a non-zero
    /// <see cref="StandardVideoRequest.RotationDegrees" /> replaces it.
    /// </remarks>
    public void TranscodeFile(TranscodeRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default)
    {
        this.RequireCapability(VideoProcessorCapabilities.DirectTranscode, nameof(TranscodeFile));
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.Validate();
        RequireProfile(request.Output.Profile);

        FFOptions current = Options;
        MediaInfo info = Analyse(request.InputPath, cancellationToken);
        if (!info.HasVideo) throw new MediaFormatException($"'{request.InputPath}' has no video stream.");

        StandardVideoRequest output = request.Output.Clone();
        output.HasAudio = output.HasAudio && info.HasAudio;

        FFMpegArguments arguments = FFMpegArguments.FromFileInput(request.InputPath, true, input =>
        {
            input.AutoRotate(false);
            if (output.RotationDegrees != 0)
            {
                input.WithCustomArgument("-display_rotation:v:0 " + (-output.RotationDegrees).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        });

        Run(arguments.OutputToFile(output.OutputPath, true, o =>
            {
                o.WithCustomArgument("-map 0:v:0");
                if (output.HasAudio) o.WithCustomArgument("-map 0:a:0");
                FFmpegOutputOptions.Apply(o, output, includeMaps: false, isTranscode: true);
            }),
            output.OutputPath, info.Duration, current, progress, cancellationToken);
    }

    /// <inheritdoc />
    public void TrimFile(TrimRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default)
    {
        this.RequireCapability(VideoProcessorCapabilities.DirectTrim, nameof(TrimFile));
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.Validate();

        FFOptions current = Options;
        MediaInfo info = Analyse(request.InputPath, cancellationToken);
        TimeSpan length = request.To - request.From;

        FFMpegArguments arguments = FFMpegArguments.FromFileInput(request.InputPath, true, input => input.Seek(request.From));
        FFMpegArgumentProcessor processor = arguments.OutputToFile(request.OutputPath, true, o =>
        {
            o.WithDuration(length);
            if (info.HasVideo) o.WithCustomArgument("-map 0:v:0");
            if (info.HasAudio) o.WithCustomArgument("-map 0:a:0");

            if (request.SnapToKeyframes)
            {
                o.CopyChannel();
                o.WithCustomArgument("-avoid_negative_ts make_zero");
            }
            else
            {
                if (info.HasVideo) o.WithVideoCodec(FFmpegOutputOptions.H264Encoder).ForcePixelFormat("yuv420p");
                if (info.HasAudio) o.WithAudioCodec("aac");
            }
        });

        Run(processor, request.OutputPath, length, current, progress, cancellationToken);
    }

    /// <inheritdoc />
    public void ConcatenateFiles(ConcatenateRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default)
    {
        this.RequireCapability(VideoProcessorCapabilities.DirectConcatenate, nameof(ConcatenateFiles));
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.Validate();

        FFOptions current = Options;
        var inputs = new List<string>();
        TimeSpan total = TimeSpan.Zero;
        foreach (string input in request.InputPaths)
        {
            // The concat list lives in the temporary folder, so every entry must be absolute.
            string full = Path.GetFullPath(input);
            total += Analyse(full, cancellationToken).Duration;
            inputs.Add(full);
        }

        FFMpegArgumentProcessor processor = FFMpegArguments
            .FromDemuxConcatInput(inputs)
            .OutputToFile(request.OutputPath, true, o => o.CopyChannel());

        Run(processor, request.OutputPath, total, current, progress, cancellationToken);
    }

    private FFOptions Options => options ?? GlobalFFOptions.Current;

    private void RequireProfile(StandardVideoProfile profile)
    {
        if (profile == StandardVideoProfile.Mp4HevcAac) this.RequireCapability(VideoProcessorCapabilities.HevcOutput, "HEVC output");
    }

    private static VideoStreamInfo RequireVideo(string path, FFOptions options, CancellationToken cancellationToken)
    {
        MediaInfo info = FFmpegProbe.AnalyseAsync(path, options, cancellationToken).GetAwaiter().GetResult();
        return info.PrimaryVideo ?? throw new MediaFormatException($"'{path}' has no video stream.");
    }

    // Runs a one-shot ffmpeg command: progress from ffmpeg's time= lines, cancellation through the processor,
    // failures wrapped for the contract, and the partial output deleted on failure or cancellation.
    private static void Run(
        FFMpegArgumentProcessor processor,
        string outputPath,
        TimeSpan totalDuration,
        FFOptions options,
        IProgress<VideoProgress> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stopwatch stopwatch = Stopwatch.StartNew();

        if (progress != null)
        {
            processor.NotifyOnProgress(position =>
            {
                double? fraction = totalDuration > TimeSpan.Zero
                    ? Math.Clamp(position.TotalSeconds / totalDuration.TotalSeconds, 0, 1)
                    : null;
                progress.Report(new VideoProgress(fraction, 0, position < TimeSpan.Zero ? TimeSpan.Zero : position, stopwatch.Elapsed));
            });
        }

        try
        {
            processor.CancellableThrough(cancellationToken).ProcessSynchronously(true, options);
        }
        catch (OperationCanceledException)
        {
            DeleteQuietly(outputPath);
            throw;
        }
        catch (FFMpegException exception)
        {
            DeleteQuietly(outputPath);
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            throw new VideoProcessingException($"ffmpeg failed to write '{outputPath}': {exception.Message}", exception);
        }

        progress?.Report(new VideoProgress(1, 0, totalDuration, stopwatch.Elapsed));
    }

    private static void DeleteQuietly(string path)
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
