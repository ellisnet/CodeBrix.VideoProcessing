using System;
using CodeBrix.VideoProcessing.Core.Frames;

namespace CodeBrix.VideoProcessing.Core.Output;

/// <summary>
/// Writes a standard MP4 file from frames and sound supplied by the caller. Returned by
/// <see cref="IVideoProcessor.CreateWriter" />.
/// </summary>
/// <remarks>
/// <para>
/// FRAMES go in presentation order, one call per frame, all with the size and layout of the request. The ENGINE
/// assigns the timestamps from the frame index and the constant frame rate - <see cref="VideoFrameData.Timestamp" />
/// and <see cref="VideoFrameData.FrameIndex" /> of the frames you pass are ignored. An alpha plane is ignored too
/// (MP4 carries no alpha).
/// </para>
/// <para>
/// SOUND (when <see cref="StandardVideoRequest.HasAudio" />) goes in as interleaved 32-bit float samples at the
/// request's rate and channel count, in as many calls as you like. Feed sound and pictures roughly in step with
/// each other - for example one video frame's worth of samples per frame. An engine may buffer to cope with one
/// running ahead of the other, but memory use grows with the gap.
/// </para>
/// <para>
/// <see cref="Finish" /> flushes the encoders and completes the file. Disposing WITHOUT finishing abandons the
/// file and deletes whatever had been written. Not thread-safe: drive one writer from one thread at a time.
/// </para>
/// </remarks>
public interface IStandardVideoWriter : IDisposable
{
    /// <summary>The (validated) request this writer was created from.</summary>
    StandardVideoRequest Request { get; }

    /// <summary>The number of video frames accepted so far.</summary>
    long FramesWritten { get; }

    /// <summary>The number of audio frames (one sample per channel) accepted so far.</summary>
    long AudioFramesWritten { get; }

    /// <summary>Adds the next video frame.</summary>
    /// <param name="frame">The picture; its memory may be reused as soon as this returns.</param>
    /// <exception cref="ArgumentException">
    /// The frame's size or layout does not match the request, or its <see cref="VideoFrameData.BitDepth" /> is not 8.
    /// </exception>
    /// <exception cref="InvalidOperationException">The writer has been finished.</exception>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    /// <exception cref="VideoProcessingException">The engine failed.</exception>
    void WriteFrame(in VideoFrameData frame);

    /// <summary>Adds sound.</summary>
    /// <param name="interleaved">Interleaved float samples; the length must be a multiple of the channel count.</param>
    /// <exception cref="InvalidOperationException">The request has no audio, or the writer has been finished.</exception>
    /// <exception cref="ArgumentException">The length is not a multiple of the channel count.</exception>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    /// <exception cref="VideoProcessingException">The engine failed.</exception>
    void WriteAudio(ReadOnlySpan<float> interleaved);

    /// <summary>Flushes everything and completes the file. Call it once; a second call does nothing.</summary>
    /// <exception cref="InvalidOperationException">No frame was written.</exception>
    /// <exception cref="VideoProcessingException">The engine failed to complete the file.</exception>
    void Finish();
}
