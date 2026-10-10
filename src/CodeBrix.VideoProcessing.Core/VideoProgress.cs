using System;

namespace CodeBrix.VideoProcessing.Core;

/// <summary>A progress report from a long-running engine operation.</summary>
public readonly struct VideoProgress
{
    /// <summary>Creates a progress report.</summary>
    /// <param name="fraction">How far through the operation is, 0..1, or null when the total is not known.</param>
    /// <param name="frames">Video frames processed so far (0 when the engine does not count frames).</param>
    /// <param name="position">The media time processed so far.</param>
    /// <param name="elapsed">Wall-clock time since the operation started.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is negative, or <paramref name="fraction" /> is outside 0..1.</exception>
    public VideoProgress(double? fraction, long frames, TimeSpan position, TimeSpan elapsed)
    {
        if (fraction.HasValue && (double.IsNaN(fraction.Value) || fraction.Value < 0 || fraction.Value > 1))
            throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "The fraction must be between 0 and 1.");
        if (frames < 0) throw new ArgumentOutOfRangeException(nameof(frames), frames, "The frame count cannot be negative.");
        if (position < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(position), position, "The position cannot be negative.");
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "The elapsed time cannot be negative.");

        Fraction = fraction;
        Frames = frames;
        Position = position;
        Elapsed = elapsed;
    }

    /// <summary>How far through the operation is, 0..1; null when the total is not known (for example while a writer is still being fed).</summary>
    public double? Fraction { get; }

    /// <summary>Video frames processed so far (0 when the engine does not count frames for this operation).</summary>
    public long Frames { get; }

    /// <summary>The media time processed so far.</summary>
    public TimeSpan Position { get; }

    /// <summary>Wall-clock time since the operation started.</summary>
    public TimeSpan Elapsed { get; }

    /// <inheritdoc />
    public override string ToString() =>
        Fraction.HasValue
            ? $"{Fraction.Value:P1} ({Frames} frames, {Position} media, {Elapsed} elapsed)"
            : $"{Frames} frames, {Position} media, {Elapsed} elapsed";
}
