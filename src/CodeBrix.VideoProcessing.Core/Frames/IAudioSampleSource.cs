using System;

namespace CodeBrix.VideoProcessing.Core.Frames;

/// <summary>
/// A decoder delivering one audio stream as interleaved 32-bit float samples (nominally -1..1). Returned by
/// <see cref="IVideoProcessor.OpenAudio" />.
/// </summary>
/// <remarks>
/// A "frame" here is one sample for every channel - <see cref="AudioSourceInfo.Channels" /> floats. Not
/// thread-safe: drive one source from one thread at a time.
/// </remarks>
public interface IAudioSampleSource : IDisposable
{
    /// <summary>What this source delivers.</summary>
    AudioSourceInfo Info { get; }

    /// <summary>Decodes samples into a buffer, blocking until it is full or the stream ends.</summary>
    /// <param name="interleaved">The destination; its length must be a multiple of <see cref="AudioSourceInfo.Channels" />.</param>
    /// <returns>The number of FRAMES written (floats / channels); 0 at the end of the stream.</returns>
    /// <exception cref="ArgumentException">The buffer length is not a multiple of the channel count.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="VideoProcessingException">The engine failed while decoding.</exception>
    int Read(Span<float> interleaved);

    /// <summary>True when <see cref="Seek" /> is available.</summary>
    bool CanSeek { get; }

    /// <summary>Moves so that the next sample read is the one at <paramref name="position" />.</summary>
    /// <param name="position">A time at or after zero.</param>
    /// <exception cref="NotSupportedException"><see cref="CanSeek" /> is false.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position" /> is negative.</exception>
    void Seek(TimeSpan position);
}
