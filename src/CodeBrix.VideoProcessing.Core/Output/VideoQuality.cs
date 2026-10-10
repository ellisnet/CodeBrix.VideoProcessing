using System;
using System.Globalization;

namespace CodeBrix.VideoProcessing.Core.Output;

/// <summary>
/// How much quality to buy for how many bits: EITHER a constant-quality level on the familiar 0..51 "CRF" scale
/// (lower is better; about 18 is visually lossless, 23 is typical) OR a target bitrate. One struct, with a mode.
/// </summary>
/// <remarks>
/// <c>default(VideoQuality)</c> is <see cref="VideoQualityMode.EngineDefault" />. Engines without a true CRF
/// encoder (platform hardware encoders) map the quality level onto a bitrate or a quality hint as best they can;
/// the scale is a request, not a guarantee of identical output across engines.
/// </remarks>
public readonly struct VideoQuality : IEquatable<VideoQuality>
{
    /// <summary>The lowest (best) constant-quality level.</summary>
    public const int BestQualityLevel = 0;

    /// <summary>The highest (smallest file) constant-quality level.</summary>
    public const int WorstQualityLevel = 51;

    private VideoQuality(VideoQualityMode mode, int qualityLevel, int bitrateKilobitsPerSecond)
    {
        Mode = mode;
        QualityLevel = qualityLevel;
        BitrateKilobitsPerSecond = bitrateKilobitsPerSecond;
    }

    /// <summary>Let the engine choose. The same as <c>default(VideoQuality)</c>.</summary>
    public static VideoQuality EngineDefault => default;

    /// <summary>Constant quality on the 0..51 scale (lower is better).</summary>
    /// <param name="level">0..51; 23 is typical, about 18 is visually lossless.</param>
    /// <returns>The quality.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level" /> is outside 0..51.</exception>
    public static VideoQuality ConstantQuality(int level)
    {
        if (level < BestQualityLevel || level > WorstQualityLevel)
            throw new ArgumentOutOfRangeException(nameof(level), level, $"The quality level must be between {BestQualityLevel} and {WorstQualityLevel}.");
        return new VideoQuality(VideoQualityMode.ConstantQuality, level, 0);
    }

    /// <summary>An average target bitrate.</summary>
    /// <param name="kilobitsPerSecond">Greater than zero (1000 = 1 Mbit/s).</param>
    /// <returns>The quality.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kilobitsPerSecond" /> is not positive.</exception>
    public static VideoQuality TargetBitrate(int kilobitsPerSecond)
    {
        if (kilobitsPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(kilobitsPerSecond), kilobitsPerSecond, "The bitrate must be greater than zero.");
        return new VideoQuality(VideoQualityMode.TargetBitrate, 0, kilobitsPerSecond);
    }

    /// <summary>Which of the two kinds of request this is.</summary>
    public VideoQualityMode Mode { get; }

    /// <summary>The 0..51 level when <see cref="Mode" /> is <see cref="VideoQualityMode.ConstantQuality" />; otherwise 0.</summary>
    public int QualityLevel { get; }

    /// <summary>The bitrate when <see cref="Mode" /> is <see cref="VideoQualityMode.TargetBitrate" />; otherwise 0.</summary>
    public int BitrateKilobitsPerSecond { get; }

    /// <inheritdoc />
    public bool Equals(VideoQuality other) =>
        Mode == other.Mode && QualityLevel == other.QualityLevel && BitrateKilobitsPerSecond == other.BitrateKilobitsPerSecond;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is VideoQuality other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Mode, QualityLevel, BitrateKilobitsPerSecond);

    /// <inheritdoc />
    public override string ToString() => Mode switch
    {
        VideoQualityMode.ConstantQuality => "quality " + QualityLevel.ToString(CultureInfo.InvariantCulture),
        VideoQualityMode.TargetBitrate => BitrateKilobitsPerSecond.ToString(CultureInfo.InvariantCulture) + " kbit/s",
        _ => "engine default"
    };

    /// <summary>Equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when equal.</returns>
    public static bool operator ==(VideoQuality left, VideoQuality right) => left.Equals(right);

    /// <summary>Inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when not equal.</returns>
    public static bool operator !=(VideoQuality left, VideoQuality right) => !left.Equals(right);
}
