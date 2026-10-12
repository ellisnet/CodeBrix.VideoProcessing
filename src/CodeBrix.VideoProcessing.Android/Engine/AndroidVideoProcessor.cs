using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;
using JavaThrowable = global::Java.Lang.Throwable;

namespace CodeBrix.VideoProcessing.Android.Engine;

/// <summary>
/// The Android engine for the CodeBrix.VideoProcessing.Core contract: an <see cref="IVideoProcessor" /> built on the
/// platform's own MediaExtractor, MediaCodec, MediaMuxer and MediaMetadataRetriever. For Android API level 33 and
/// newer.
/// </summary>
/// <remarks>
/// <para>
/// Applications normally do not create one: they call <see cref="CodeBrixVideoProcessingAndroid.Register()" /> once
/// at start-up and code against <see cref="VideoProcessors.Current" />.
/// </para>
/// <para>
/// CAPABILITIES (computed once, when the instance is created): RgbaInput, Nv12Input, GrayFrameSource, Snapshot,
/// DirectTranscode, DirectTrim and DirectConcatenate always; HevcOutput when the device has a "video/hevc" encoder;
/// HighBitDepthFrameSource when the device has a video decoder that lists P010 output
/// (<see cref="HighBitDepthDecoders" />). Never AlphaFrameSource (Android's decoders expose no alpha) or
/// I444FrameSource (they deliver 4:2:0).
/// </para>
/// <para>
/// BIT DEPTH: 8-bit frames are the default; a 10-bit source is then cut to 8 bits and says so through
/// <see cref="VideoSourceInfo.SourceBitDepth" />. With HighBitDepthFrameSource, <see cref="VideoSourceOptions.BitDepth" />
/// = 10 delivers I420, NV12 or Gray frames at 10 bits (the contract's low-bits samples): a 10-bit source through the
/// decoder's P010 output (refused with NotSupportedException, naming the codec and stream, when the decoder will not
/// output P010 for it), an 8-bit source widened (value &lt;&lt; 2). 12 bits are always refused.
/// </para>
/// <para>
/// THREADING: the engine itself holds no per-call state and is safe to use from several threads. Every source,
/// writer and whole-file operation drives its codecs in MediaCodec's synchronous mode ON THE CALLING THREAD - no
/// background threads are started - so a long operation should be called off the UI thread.
/// </para>
/// </remarks>
public sealed class AndroidVideoProcessor : IVideoProcessor
{
    /// <summary>The engine's <see cref="Name" />: "android-mediacodec".</summary>
    public const string EngineName = "android-mediacodec";

    /// <summary>Creates the engine, reading the device's codec list once to decide <see cref="Capabilities" />.</summary>
    public AndroidVideoProcessor()
    {
        List<CodecTypeInfo> codecs = AndroidMedia.ListCodecs();
        HasHevcEncoder = AndroidMedia.HasEncoder(MediaFormatKeys.MimeHevc);
        HighBitDepthDecoders = AndroidEngineCapabilities.FindP010Decoders(codecs);
        Capabilities = AndroidEngineCapabilities.Compute(HasHevcEncoder, HighBitDepthDecoders.Count > 0);
    }

    /// <summary>"android-mediacodec".</summary>
    public string Name => EngineName;

    /// <summary>What this engine supports on this device (see the class remarks).</summary>
    public VideoProcessorCapabilities Capabilities { get; }

    /// <summary>True when the device's codec list has a "video/hevc" encoder (which sets <see cref="VideoProcessorCapabilities.HevcOutput" />).</summary>
    public bool HasHevcEncoder { get; }

    /// <summary>
    /// The device's video decoders that list COLOR_FormatYUVP010 (10-bit output), as "name (mime)" - for example
    /// "c2.android.hevc.decoder (video/hevc)" - one entry per decoder and type, in codec-list order. Not empty exactly
    /// when <see cref="Capabilities" /> includes <see cref="VideoProcessorCapabilities.HighBitDepthFrameSource" />.
    /// A decoder listed here still has to accept P010 output for a PARTICULAR stream: OpenVideo with
    /// <see cref="VideoSourceOptions.BitDepth" /> = 10 throws NotSupportedException naming the codec and stream when
    /// it does not.
    /// </summary>
    public IReadOnlyList<string> HighBitDepthDecoders { get; }

    /// <inheritdoc />
    /// <remarks>
    /// MediaExtractor reports the tracks; for MP4 / MOV files the engine also reads the sample tables itself to give
    /// the EXACT frame rate (30000/1001 stays 30000/1001) and the frame count. MediaMetadataRetriever supplies the
    /// container MIME type ("video/mp4", ...) and the tags (title, date - also as "creation_time" - artist, ...).
    /// Codec names use ffmpeg's words ("h264", "hevc", "aac", ...). Not reported (left 0 / null / Unspecified):
    /// HasAlpha (always false), the frame count of non-MP4 files, and the bit depth / pixel format of a stream whose
    /// profile does not fix them.
    /// </remarks>
    public MediaInfo Analyse(string path, CancellationToken cancellationToken = default)
    {
        EngineRequestGate.CheckPath(path, nameof(path));
        return AnalyseDetailed(path, cancellationToken).Info;
    }

    /// <inheritdoc />
    public Task<MediaInfo> AnalyseAsync(string path, CancellationToken cancellationToken = default)
    {
        EngineRequestGate.CheckPath(path, nameof(path));
        return Task.Run(() => Analyse(path, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Decodes with the engine's own frame source from the sync sample at or before <paramref name="at" /> and returns
    /// the FIRST frame of the constant-rate grid at or after it - an owned 8-bit I420 frame in stored orientation, exactly
    /// as the contract describes (not MediaMetadataRetriever's display-rotated bitmap of the "closest" frame).
    /// </remarks>
    public VideoFrameData Snapshot(string path, TimeSpan at, CancellationToken cancellationToken = default)
    {
        EngineRequestGate.CheckSnapshot(this, path, at);
        VideoStreamInfo stream = RequireVideo(path, cancellationToken);
        using var source = new AndroidVideoFrameSource(path, stream, new VideoSourceOptions { StartTime = at }, maximumFrames: 1, cancellationToken);
        if (!source.TryReadFrame(out VideoFrameData frame))
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new MediaFormatException($"'{path}' has no frame at or after {at}.");
        }

        return frame.Copy();
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="VideoSourceOptions.BitDepth" /> = 10 needs <see cref="VideoProcessorCapabilities.HighBitDepthFrameSource" />
    /// (see <see cref="HighBitDepthDecoders" />). A 10-bit (or unknown-depth) source is decoded with P010 output,
    /// shifted down to the contract's low-bits samples; when the decoder will not configure P010 or delivers 8-bit
    /// pictures for this stream, OpenVideo throws <see cref="NotSupportedException" /> naming the codec and the
    /// stream instead of delivering cut samples. An 8-bit source is widened (value &lt;&lt; 2) and
    /// <see cref="VideoSourceInfo.SourceBitDepth" /> says 8. BitDepth = 12 always throws NotSupportedException.
    /// </remarks>
    public IVideoFrameSource OpenVideo(string path, VideoSourceOptions options = null)
    {
        EngineRequestGate.CheckPath(path, nameof(path));
        options = EngineRequestGate.CheckSourceOptions(this, options);
        VideoStreamInfo stream = RequireVideo(path, CancellationToken.None);
        return new AndroidVideoFrameSource(path, stream, options);
    }

    /// <inheritdoc />
    public IAudioSampleSource OpenAudio(string path, AudioSourceOptions options = null)
    {
        EngineRequestGate.CheckPath(path, nameof(path));
        options = EngineRequestGate.CheckAudioOptions(options);
        AudioStreamInfo stream = AnalyseDetailed(path, CancellationToken.None).Info.PrimaryAudio
            ?? throw new MediaFormatException($"'{path}' has no audio stream.");
        return new AndroidAudioSampleSource(path, stream, options);
    }

    /// <inheritdoc />
    public IStandardVideoWriter CreateWriter(StandardVideoRequest request, IProgress<VideoProgress> progress = null)
    {
        EngineRequestGate.CheckWriterRequest(this, request);
        return new AndroidStandardVideoWriter(request.Clone(), progress);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Decodes the first video (and, when kept, audio) stream and re-encodes it through the engine's own MP4 writer:
    /// size changes use a managed bilinear scaler, rate changes pick the source frame on screen at each output time,
    /// and the sound is resampled / re-mixed to the output format. The pictures keep their stored orientation and the
    /// source's rotation metadata is carried over; a non-zero <see cref="StandardVideoRequest.RotationDegrees" />
    /// replaces it. On cancellation or failure the partial output is deleted.
    /// </remarks>
    public void TranscodeFile(TranscodeRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default)
    {
        EngineRequestGate.CheckTranscode(this, request);
        Guard(request.InputPath, () => AndroidFileOperations.Transcode(this, request, progress, cancellationToken));
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="TrimRequest.SnapToKeyframes" /> = true copies the coded samples (MediaExtractor to MediaMuxer) from
    /// the video sync sample at or before From up to (not including) the first video sample at or after To, so no
    /// picture references a dropped one; false decodes and re-encodes for a frame-accurate cut to H.264 + AAC at the
    /// engine default quality. A file without video is always trimmed by copying. The output container follows the
    /// extension for a copy (.webm, .3gp, .ogg; MP4 otherwise) and is always MP4 for a re-encode.
    /// </remarks>
    public void TrimFile(TrimRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default)
    {
        EngineRequestGate.CheckTrim(this, request);
        Guard(request.InputPath, () => AndroidFileOperations.Trim(this, request, progress, cancellationToken));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Copies the coded samples when every input has the same kinds of stream and, stream by stream, the same codec,
    /// size or sample format, and byte-identical codec configuration (H.264 SPS / PPS, AAC AudioSpecificConfig);
    /// otherwise - where the contract's copy-only promise cannot be kept - the inputs are decoded and re-encoded to
    /// H.264 + AAC at the first input's size, rate and audio format instead of failing.
    /// </remarks>
    public void ConcatenateFiles(ConcatenateRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default)
    {
        EngineRequestGate.CheckConcatenate(this, request);
        Guard(request.OutputPath, () => AndroidFileOperations.Concatenate(this, request, progress, cancellationToken));
    }

    internal AnalysedMedia AnalyseDetailed(string path, CancellationToken cancellationToken)
    {
        try
        {
            return AndroidMediaAnalyser.Analyse(path, cancellationToken);
        }
        catch (JavaThrowable exception)
        {
            throw new VideoProcessingException($"Analysing '{path}' failed: {AndroidMedia.Describe(exception)}", exception);
        }
    }

    private VideoStreamInfo RequireVideo(string path, CancellationToken cancellationToken) =>
        AnalyseDetailed(path, cancellationToken).Info.PrimaryVideo ?? throw new MediaFormatException($"'{path}' has no video stream.");

    private static void Guard(string path, Action action)
    {
        try
        {
            action();
        }
        catch (JavaThrowable exception)
        {
            throw new VideoProcessingException($"Processing '{path}' failed: {AndroidMedia.Describe(exception)}", exception);
        }
    }
}
