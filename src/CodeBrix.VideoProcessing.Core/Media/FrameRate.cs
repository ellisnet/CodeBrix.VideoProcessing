using System;
using System.Globalization;

namespace CodeBrix.VideoProcessing.Core.Media;

/// <summary>
/// A frame rate as an exact rational number of frames per second - for example 30000/1001 for "29.97".
/// </summary>
/// <remarks>
/// <para>
/// The value is always stored REDUCED (60/2 becomes 30/1), so two rates that are equal compare equal. The default
/// value, 0/0, means "not specified" (<see cref="IsSpecified" /> is false); every other value has a positive
/// numerator and denominator.
/// </para>
/// <para>
/// Timestamps are derived from frame indexes, never the other way round, with
/// <see cref="TimestampOf" /> and <see cref="FrameIndexAt" />, which are exact inverses of each other:
/// <c>FrameIndexAt(TimestampOf(i)) == i</c> for every index.
/// </para>
/// </remarks>
public readonly struct FrameRate : IEquatable<FrameRate>
{
    /// <summary>Creates a frame rate of <paramref name="numerator" />/<paramref name="denominator" /> frames per second.</summary>
    /// <param name="numerator">Frames, greater than zero.</param>
    /// <param name="denominator">Seconds, greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is zero or negative.</exception>
    public FrameRate(int numerator, int denominator)
    {
        if (numerator <= 0) throw new ArgumentOutOfRangeException(nameof(numerator), numerator, "The numerator must be greater than zero.");
        if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator), denominator, "The denominator must be greater than zero.");

        int divisor = GreatestCommonDivisor(numerator, denominator);
        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    /// <summary>"Not specified": 0/0. The same as <c>default(FrameRate)</c>.</summary>
    public static FrameRate Unspecified => default;

    /// <summary>The numerator (frames); 0 when not specified.</summary>
    public int Numerator { get; }

    /// <summary>The denominator (seconds); 0 when not specified.</summary>
    public int Denominator { get; }

    /// <summary>True for every rate except <see cref="Unspecified" />.</summary>
    public bool IsSpecified => Numerator > 0 && Denominator > 0;

    /// <summary>The rate as a floating-point number of frames per second; 0 when not specified.</summary>
    public double FramesPerSecond => IsSpecified ? (double)Numerator / Denominator : 0;

    /// <summary>The duration of one frame (rounded up to the next tick).</summary>
    /// <exception cref="InvalidOperationException">The rate is not specified.</exception>
    public TimeSpan FrameDuration => TimestampOf(1);

    /// <summary>The presentation time of a frame: <c>frameIndex * Denominator / Numerator</c> seconds, rounded UP to a whole tick.</summary>
    /// <param name="frameIndex">The zero-based frame index.</param>
    /// <returns>The time at which that frame starts.</returns>
    /// <exception cref="InvalidOperationException">The rate is not specified.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frameIndex" /> is negative.</exception>
    public TimeSpan TimestampOf(long frameIndex)
    {
        RequireSpecified();
        if (frameIndex < 0) throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, "The frame index cannot be negative.");

        Int128 scaled = (Int128)frameIndex * Denominator * TimeSpan.TicksPerSecond;
        Int128 ticks = (scaled + Numerator - 1) / Numerator;
        if (ticks > TimeSpan.MaxValue.Ticks) throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, "The frame index is too large for a TimeSpan.");
        return TimeSpan.FromTicks((long)ticks);
    }

    /// <summary>The index of the frame on screen at a given time: the last frame that starts at or before it.</summary>
    /// <param name="time">A time at or after zero.</param>
    /// <returns>The zero-based frame index.</returns>
    /// <exception cref="InvalidOperationException">The rate is not specified.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="time" /> is negative.</exception>
    public long FrameIndexAt(TimeSpan time)
    {
        RequireSpecified();
        if (time < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(time), time, "The time cannot be negative.");

        Int128 index = (Int128)time.Ticks * Numerator / ((Int128)Denominator * TimeSpan.TicksPerSecond);
        return (long)index;
    }

    /// <summary>The number of whole frames that start before <paramref name="duration" /> - the frame count of a stream that long.</summary>
    /// <param name="duration">A duration at or after zero.</param>
    /// <returns>The frame count (rounded up, since a partial frame is still a frame).</returns>
    /// <exception cref="InvalidOperationException">The rate is not specified.</exception>
    public long FrameCountFor(TimeSpan duration)
    {
        RequireSpecified();
        if (duration <= TimeSpan.Zero) return 0;

        Int128 scaled = (Int128)duration.Ticks * Numerator;
        Int128 per = (Int128)Denominator * TimeSpan.TicksPerSecond;
        return (long)((scaled + per - 1) / per);
    }

    /// <summary>Parses "30000/1001", "25/1" or a whole number such as "25".</summary>
    /// <param name="text">The text.</param>
    /// <returns>The frame rate.</returns>
    /// <exception cref="FormatException">The text is not a positive rational or whole number.</exception>
    public static FrameRate Parse(string text)
    {
        if (TryParse(text, out FrameRate rate)) return rate;
        throw new FormatException($"'{text}' is not a frame rate. Expected 'numerator/denominator' (for example 30000/1001) or a whole number.");
    }

    /// <summary>Parses "30000/1001", "25/1" or a whole number such as "25", without throwing.</summary>
    /// <param name="text">The text.</param>
    /// <param name="rate">The frame rate, or <see cref="Unspecified" /> when parsing fails.</param>
    /// <returns>True when the text was a positive rational or whole number.</returns>
    public static bool TryParse(string text, out FrameRate rate)
    {
        rate = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string[] parts = text.Trim().Split('/');
        if (parts.Length > 2) return false;

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int numerator)) return false;
        int denominator = 1;
        if (parts.Length == 2 && !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out denominator)) return false;
        if (numerator <= 0 || denominator <= 0) return false;

        rate = new FrameRate(numerator, denominator);
        return true;
    }

    /// <inheritdoc />
    public bool Equals(FrameRate other) => Numerator == other.Numerator && Denominator == other.Denominator;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is FrameRate other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    /// <summary>"numerator/denominator", for example "30000/1001"; "unspecified" for <see cref="Unspecified" />.</summary>
    /// <returns>The text form, which <see cref="Parse" /> reads back.</returns>
    public override string ToString() =>
        IsSpecified
            ? Numerator.ToString(CultureInfo.InvariantCulture) + "/" + Denominator.ToString(CultureInfo.InvariantCulture)
            : "unspecified";

    /// <summary>Equality.</summary>
    /// <param name="left">The first rate.</param>
    /// <param name="right">The second rate.</param>
    /// <returns>True when equal.</returns>
    public static bool operator ==(FrameRate left, FrameRate right) => left.Equals(right);

    /// <summary>Inequality.</summary>
    /// <param name="left">The first rate.</param>
    /// <param name="right">The second rate.</param>
    /// <returns>True when not equal.</returns>
    public static bool operator !=(FrameRate left, FrameRate right) => !left.Equals(right);

    private void RequireSpecified()
    {
        if (!IsSpecified) throw new InvalidOperationException("The frame rate is not specified.");
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        while (b != 0)
        {
            int t = a % b;
            a = b;
            b = t;
        }

        return a;
    }
}
