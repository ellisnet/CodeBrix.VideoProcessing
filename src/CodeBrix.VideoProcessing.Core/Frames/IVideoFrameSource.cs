using System;

namespace CodeBrix.VideoProcessing.Core.Frames;

/// <summary>A decoder delivering the frames of one video stream in order. Returned by <see cref="IVideoProcessor.OpenVideo" />.</summary>
/// <remarks>
/// <para>
/// Frames arrive in presentation order at the CONSTANT rate in <see cref="VideoSourceInfo.FrameRate" />;
/// <see cref="VideoFrameData.Timestamp" /> is <c>FrameRate.TimestampOf(FrameIndex)</c> and
/// <see cref="VideoFrameData.FrameIndex" /> counts from the start of the stream (not from a seek position).
/// </para>
/// <para>
/// OWNERSHIP: a frame is valid only until the next <see cref="TryReadFrame" />, <see cref="Seek" /> or
/// <see cref="IDisposable.Dispose" />; call <see cref="VideoFrameData.Copy" /> to keep it.
/// </para>
/// <para>Not thread-safe: drive one source from one thread at a time.</para>
/// </remarks>
public interface IVideoFrameSource : IDisposable
{
    /// <summary>What this source delivers.</summary>
    VideoSourceInfo Info { get; }

    /// <summary>Decodes the next frame.</summary>
    /// <param name="frame">The frame, or the default (empty) frame at the end of the stream.</param>
    /// <returns>True when a frame was produced; false at the end of the stream.</returns>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="VideoProcessingException">The engine failed while decoding.</exception>
    bool TryReadFrame(out VideoFrameData frame);

    /// <summary>True when <see cref="Seek" /> is available.</summary>
    bool CanSeek { get; }

    /// <summary>Moves so that the next frame read is the first frame that starts at or after <paramref name="position" />.</summary>
    /// <param name="position">A time at or after zero.</param>
    /// <exception cref="NotSupportedException"><see cref="CanSeek" /> is false.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position" /> is negative.</exception>
    void Seek(TimeSpan position);
}
