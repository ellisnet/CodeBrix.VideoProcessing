using System;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// The engine's DETERMINISTIC mapping from a <see cref="VideoQuality" /> to an encoder target bitrate. Android's
/// platform encoders take a bitrate, not a CRF, so a constant-quality level 0..51 is turned into bits per pixel per
/// frame and multiplied out by width x height x frames per second.
/// </summary>
/// <remarks>
/// <para>
/// Bits per pixel (H.264) = 0.1 x 2^((23 - level) / 6), rounded to a whole number of TEN-THOUSANDTHS of a bit
/// (<see cref="BitsPerPixelTenThousandths" />): level 23 (the engine default) is 0.1000, and every 6 levels halves or
/// doubles it - the familiar x264 rule of thumb that +6 CRF halves the bitrate. HEVC gets 65% of the H.264 figure.
/// </para>
/// <para>
/// kbit/s = floor(width x height x fps x bpp / 1000), then clamped to <see cref="MinimumKilobitsPerSecond" />..
/// <see cref="MaximumKilobitsPerSecond" />. The engine then clamps once more to the encoder's own supported bitrate
/// range on the device. A <see cref="VideoQualityMode.TargetBitrate" /> request is used as given (device clamp only).
/// </para>
/// </remarks>
internal static class QualityBitrateTable
{
    /// <summary>The level used for <see cref="VideoQualityMode.EngineDefault" />.</summary>
    internal const int DefaultLevel = 23;

    /// <summary>The floor of the computed bitrate.</summary>
    internal const int MinimumKilobitsPerSecond = 64;

    /// <summary>The ceiling of the computed bitrate (before the device's own clamp).</summary>
    internal const int MaximumKilobitsPerSecond = 200_000;

    /// <summary>HEVC's share of the H.264 bitrate, in percent.</summary>
    internal const int HevcPercent = 65;

    /// <summary>Bits per pixel per frame for each level 0..51, in ten-thousandths of a bit (level 23 = 1000).</summary>
    internal static readonly int[] BitsPerPixelTenThousandths = BuildTable();

    /// <summary>The target bitrate in kbit/s for a quality request.</summary>
    /// <param name="quality">The request.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="rate">The constant frame rate.</param>
    /// <param name="hevc">True for HEVC output.</param>
    internal static int ToKilobitsPerSecond(VideoQuality quality, int width, int height, FrameRate rate, bool hevc)
    {
        if (quality.Mode == VideoQualityMode.TargetBitrate) return quality.BitrateKilobitsPerSecond;
        int level = quality.Mode == VideoQualityMode.ConstantQuality ? quality.QualityLevel : DefaultLevel;
        return ForLevel(level, width, height, rate, hevc);
    }

    /// <summary>The target bitrate in kbit/s for a constant-quality level.</summary>
    internal static int ForLevel(int level, int width, int height, FrameRate rate, bool hevc)
    {
        if (level < VideoQuality.BestQualityLevel || level > VideoQuality.WorstQualityLevel) throw new ArgumentOutOfRangeException(nameof(level));
        if (!rate.IsSpecified) throw new ArgumentException("The frame rate must be specified.", nameof(rate));

        // width * height * (num / den) * bpp / 10000 / 1000, in exact integer arithmetic.
        Int128 bits = (Int128)width * height * rate.Numerator * BitsPerPixelTenThousandths[level];
        Int128 kilobits = bits / ((Int128)rate.Denominator * 10_000 * 1000);
        if (hevc) kilobits = kilobits * HevcPercent / 100;
        if (kilobits < MinimumKilobitsPerSecond) return MinimumKilobitsPerSecond;
        if (kilobits > MaximumKilobitsPerSecond) return MaximumKilobitsPerSecond;
        return (int)kilobits;
    }

    private static int[] BuildTable()
    {
        int[] table = new int[VideoQuality.WorstQualityLevel + 1];
        for (int level = 0; level < table.Length; level++)
        {
            table[level] = (int)Math.Round(1000 * Math.Pow(2, (DefaultLevel - level) / 6.0), MidpointRounding.AwayFromZero);
        }

        return table;
    }
}
