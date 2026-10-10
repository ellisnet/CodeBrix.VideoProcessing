using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;

namespace CodeBrix.VideoProcessing.Core;

/// <summary>
/// A video processor ENGINE: the one interface a platform-specific package implements so that libraries and
/// applications can analyse, decode, encode and edit video without knowing which engine is underneath.
/// </summary>
/// <remarks>
/// <para>
/// A consumer does not construct an engine. It asks the registry - <see cref="VideoProcessors.Current" /> - after
/// the application has registered one at start-up (for example <c>CodeBrixVideoProcessing.Register()</c> on
/// Windows, Linux and macOS).
/// </para>
/// <para>
/// THE MANDATORY CORE every engine implements: <see cref="Analyse" /> / <see cref="AnalyseAsync" />,
/// <see cref="OpenVideo" /> delivering 8-bit <see cref="VideoPixelLayout.I420" /> frames, <see cref="OpenAudio" />
/// delivering interleaved 32-bit float samples, and <see cref="CreateWriter" /> producing
/// <see cref="StandardVideoProfile.Mp4H264Aac" /> from 8-bit I420 frames. Everything else is announced through
/// <see cref="Capabilities" />, and an operation whose capability flag is missing throws
/// <see cref="NotSupportedException" />.
/// </para>
/// <para>
/// ERRORS: a missing input file throws <see cref="System.IO.FileNotFoundException" />; an input the engine cannot
/// read (or that lacks the stream asked for) throws <see cref="MediaFormatException" />; any other failure of the
/// engine throws <see cref="VideoProcessingException" />; argument problems throw the usual
/// <see cref="ArgumentException" /> family; cancellation throws <see cref="OperationCanceledException" />.
/// </para>
/// <para>
/// THREADING: an engine instance is safe to use from several threads at once. The objects it hands out (frame
/// sources, sample sources and writers) are NOT - each is meant to be driven by one thread at a time.
/// </para>
/// </remarks>
public interface IVideoProcessor
{
    /// <summary>A short human-readable name for the engine, for logs and diagnostics (for example "ffmpeg").</summary>
    string Name { get; }

    /// <summary>The optional operations this engine supports.</summary>
    VideoProcessorCapabilities Capabilities { get; }

    /// <summary>Describes a media file: its container, duration, video and audio streams, and tags.</summary>
    /// <param name="path">The file to analyse.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <returns>The description of the file.</returns>
    /// <exception cref="ArgumentException"><paramref name="path" /> is null or empty.</exception>
    /// <exception cref="System.IO.FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="MediaFormatException">The engine cannot read the file.</exception>
    MediaInfo Analyse(string path, CancellationToken cancellationToken = default);

    /// <summary>Describes a media file asynchronously. See <see cref="Analyse" />.</summary>
    /// <param name="path">The file to analyse.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <returns>The description of the file.</returns>
    Task<MediaInfo> AnalyseAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Decodes ONE picture from a video file.</summary>
    /// <param name="path">The video file.</param>
    /// <param name="at">The position of the picture; the first frame at or after it is returned.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// An 8-bit <see cref="VideoPixelLayout.I420" /> frame in stored (not display-rotated) orientation, with an
    /// alpha plane when the source carries alpha and the engine has <see cref="VideoProcessorCapabilities.AlphaFrameSource" />.
    /// The frame OWNS its memory - nothing is pooled.
    /// </returns>
    /// <exception cref="NotSupportedException">The engine lacks <see cref="VideoProcessorCapabilities.Snapshot" />.</exception>
    /// <exception cref="MediaFormatException">The file has no video stream, or <paramref name="at" /> is past its end.</exception>
    VideoFrameData Snapshot(string path, TimeSpan at, CancellationToken cancellationToken = default);

    /// <summary>Opens the first video stream of a file for frame-by-frame decoding.</summary>
    /// <param name="path">The video file.</param>
    /// <param name="options">What to deliver; null means the defaults (8-bit I420, no alpha, from the start).</param>
    /// <returns>A frame source; dispose it when done.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The options are invalid (see <see cref="VideoSourceOptions.Validate" />).</exception>
    /// <exception cref="NotSupportedException">
    /// The options ask for a layout, bit depth or alpha the engine does not offer (see
    /// <see cref="VideoProcessorCapabilities" />), or a combination of them it cannot produce exactly.
    /// </exception>
    /// <exception cref="MediaFormatException">The file has no video stream.</exception>
    IVideoFrameSource OpenVideo(string path, VideoSourceOptions options = null);

    /// <summary>Opens the first audio stream of a file for decoding to interleaved 32-bit float samples.</summary>
    /// <param name="path">The media file.</param>
    /// <param name="options">The sample rate and channel count to deliver; null keeps the source's.</param>
    /// <returns>A sample source; dispose it when done.</returns>
    /// <exception cref="MediaFormatException">The file has no audio stream.</exception>
    IAudioSampleSource OpenAudio(string path, AudioSourceOptions options = null);

    /// <summary>Starts writing a standard MP4 video from frames (and optionally sound) supplied by the caller.</summary>
    /// <param name="request">What to write. It is validated with <see cref="StandardVideoRequest.Validate" />.</param>
    /// <param name="progress">Receives progress as frames are written; may be null.</param>
    /// <returns>A writer; call <see cref="IStandardVideoWriter.Finish" />, then dispose it.</returns>
    /// <exception cref="NotSupportedException">
    /// The request asks for <see cref="StandardVideoProfile.Mp4HevcAac" /> without
    /// <see cref="VideoProcessorCapabilities.HevcOutput" />, or for an input layout the engine does not accept.
    /// </exception>
    IStandardVideoWriter CreateWriter(StandardVideoRequest request, IProgress<VideoProgress> progress = null);

    /// <summary>Re-encodes a whole file to a standard MP4 in one engine operation.</summary>
    /// <param name="request">The input and the output settings.</param>
    /// <param name="progress">Receives progress; may be null.</param>
    /// <param name="cancellationToken">Cancels the operation; a partial output file is deleted.</param>
    /// <exception cref="NotSupportedException">The engine lacks <see cref="VideoProcessorCapabilities.DirectTranscode" />.</exception>
    void TranscodeFile(TranscodeRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>Cuts a time range out of a file into a new file.</summary>
    /// <param name="request">The input, the range and the output.</param>
    /// <param name="progress">Receives progress; may be null.</param>
    /// <param name="cancellationToken">Cancels the operation; a partial output file is deleted.</param>
    /// <exception cref="NotSupportedException">The engine lacks <see cref="VideoProcessorCapabilities.DirectTrim" />.</exception>
    void TrimFile(TrimRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>Joins files that share the same codecs and stream parameters, one after the other, without re-encoding.</summary>
    /// <param name="request">The inputs, in order, and the output.</param>
    /// <param name="progress">Receives progress; may be null.</param>
    /// <param name="cancellationToken">Cancels the operation; a partial output file is deleted.</param>
    /// <exception cref="NotSupportedException">The engine lacks <see cref="VideoProcessorCapabilities.DirectConcatenate" />.</exception>
    void ConcatenateFiles(ConcatenateRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default);
}
