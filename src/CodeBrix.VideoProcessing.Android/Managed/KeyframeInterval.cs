using System;
using CodeBrix.VideoProcessing.Core.Media;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// Turns <c>StandardVideoRequest.KeyframeIntervalFrames</c> into MediaFormat KEY_I_FRAME_INTERVAL, which is in
/// SECONDS. Since API 25 the key accepts a float, so the value is frames / fps rounded to the nearest millisecond
/// (never below one frame period's worth, 0.001 s); 0 frames means the engine default of
/// <see cref="DefaultSeconds" /> seconds. The encoder converts the seconds back to frames itself and may round again
/// (most platform encoders round to whole frames).
/// </summary>
internal static class KeyframeInterval
{
    /// <summary>The interval used when the request leaves it to the engine.</summary>
    internal const float DefaultSeconds = 2f;

    /// <summary>The KEY_I_FRAME_INTERVAL value for a request.</summary>
    internal static float ToSeconds(int keyframeIntervalFrames, FrameRate rate)
    {
        if (keyframeIntervalFrames < 0) throw new ArgumentOutOfRangeException(nameof(keyframeIntervalFrames));
        if (keyframeIntervalFrames == 0 || !rate.IsSpecified) return DefaultSeconds;

        double seconds = (double)keyframeIntervalFrames * rate.Denominator / rate.Numerator;
        double milliseconds = Math.Round(seconds * 1000, MidpointRounding.AwayFromZero);
        return (float)(Math.Max(1, milliseconds) / 1000.0);
    }
}
