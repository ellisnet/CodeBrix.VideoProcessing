using System;
using System.Globalization;
using System.Text.RegularExpressions;
using CodeBrix.VideoProcessing.Core.Media;

namespace CodeBrix.VideoProcessing.Engine;

/// <summary>Translates between ffmpeg's names and the Core contract's enums.</summary>
internal static class FFmpegNames
{
    private static readonly Regex DepthSuffix = new Regex(@"(\d{1,2})(le|be)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static VideoColorRange ToColorRange(string range, string pixelFormat)
    {
        switch (range)
        {
            case "pc":
            case "jpeg":
            case "full":
                return VideoColorRange.Full;
            case "tv":
            case "mpeg":
            case "limited":
                return VideoColorRange.Limited;
        }

        return pixelFormat != null && pixelFormat.StartsWith("yuvj", StringComparison.Ordinal)
            ? VideoColorRange.Full
            : VideoColorRange.Unspecified;
    }

    internal static VideoColorMatrix ToColorMatrix(string space) => space switch
    {
        null or "" or "unknown" or "reserved" => VideoColorMatrix.Unspecified,
        "bt709" => VideoColorMatrix.Bt709,
        "bt470bg" or "smpte170m" or "bt601" => VideoColorMatrix.Bt601,
        "bt2020nc" or "bt2020_ncl" => VideoColorMatrix.Bt2020NonConstant,
        "bt2020c" or "bt2020_cl" => VideoColorMatrix.Bt2020Constant,
        "gbr" or "rgb" => VideoColorMatrix.Rgb,
        _ => VideoColorMatrix.Other
    };

    internal static VideoColorPrimaries ToColorPrimaries(string primaries) => primaries switch
    {
        null or "" or "unknown" or "reserved" => VideoColorPrimaries.Unspecified,
        "bt709" => VideoColorPrimaries.Bt709,
        "bt470bg" => VideoColorPrimaries.Bt601Pal,
        "smpte170m" or "smpte240m" => VideoColorPrimaries.Bt601Ntsc,
        "bt2020" => VideoColorPrimaries.Bt2020,
        "smpte431" => VideoColorPrimaries.DciP3,
        "smpte432" => VideoColorPrimaries.DisplayP3,
        _ => VideoColorPrimaries.Other
    };

    internal static VideoColorTransfer ToColorTransfer(string transfer) => transfer switch
    {
        null or "" or "unknown" or "reserved" => VideoColorTransfer.Unspecified,
        "bt709" or "smpte170m" or "bt2020-10" or "bt2020-12" or "bt470m" or "bt470bg" => VideoColorTransfer.Bt709,
        "iec61966-2-1" or "iec61966_2_1" => VideoColorTransfer.Srgb,
        "linear" => VideoColorTransfer.Linear,
        "smpte2084" => VideoColorTransfer.Pq,
        "arib-std-b67" => VideoColorTransfer.Hlg,
        _ => VideoColorTransfer.Other
    };

    // The colorspace name ffmpeg's -colorspace option and the scale filter's out_color_matrix accept.
    internal static string ToFFmpegColorSpace(VideoColorMatrix matrix) => matrix switch
    {
        VideoColorMatrix.Bt601 => "smpte170m",
        VideoColorMatrix.Bt709 => "bt709",
        VideoColorMatrix.Bt2020NonConstant => "bt2020nc",
        VideoColorMatrix.Bt2020Constant => "bt2020c",
        _ => null
    };

    internal static string ToFFmpegRange(VideoColorRange range) => range == VideoColorRange.Full ? "pc" : "tv";

    internal static VideoChromaSubsampling ToChromaSubsampling(string pixelFormat)
    {
        if (string.IsNullOrEmpty(pixelFormat)) return VideoChromaSubsampling.Unknown;

        if (pixelFormat.StartsWith("gray", StringComparison.Ordinal) || pixelFormat is "ya8" or "ya16le" or "ya16be" or "monow" or "monob")
            return VideoChromaSubsampling.Monochrome;
        if (pixelFormat.Contains("420", StringComparison.Ordinal) || pixelFormat is "nv12" or "nv21" or "p010le" or "p010be" or "p016le" or "p016be")
            return VideoChromaSubsampling.Yuv420;
        if (pixelFormat.Contains("422", StringComparison.Ordinal) || pixelFormat is "nv16" or "yuyv422" or "uyvy422" or "y210le")
            return VideoChromaSubsampling.Yuv422;
        if (pixelFormat.Contains("444", StringComparison.Ordinal) || pixelFormat is "nv24" or "nv42")
            return VideoChromaSubsampling.Yuv444;
        if (pixelFormat.StartsWith("rgb", StringComparison.Ordinal) || pixelFormat.StartsWith("bgr", StringComparison.Ordinal) ||
            pixelFormat.StartsWith("gbr", StringComparison.Ordinal) || pixelFormat.StartsWith("argb", StringComparison.Ordinal) ||
            pixelFormat.StartsWith("abgr", StringComparison.Ordinal) || pixelFormat.StartsWith("0rgb", StringComparison.Ordinal) ||
            pixelFormat.StartsWith("0bgr", StringComparison.Ordinal) || pixelFormat.StartsWith("x2rgb", StringComparison.Ordinal))
            return VideoChromaSubsampling.Rgb;

        return VideoChromaSubsampling.Other;
    }

    // True for pixel formats with an alpha channel. Palette formats count: the palette may carry transparency,
    // and ffprobe cannot say whether it does.
    internal static bool HasAlpha(string pixelFormat)
    {
        if (string.IsNullOrEmpty(pixelFormat)) return false;

        return pixelFormat.StartsWith("yuva", StringComparison.Ordinal) ||
               pixelFormat.StartsWith("gbrap", StringComparison.Ordinal) ||
               pixelFormat.StartsWith("rgba", StringComparison.Ordinal) ||
               pixelFormat.StartsWith("bgra", StringComparison.Ordinal) ||
               pixelFormat.StartsWith("argb", StringComparison.Ordinal) ||
               pixelFormat.StartsWith("abgr", StringComparison.Ordinal) ||
               pixelFormat.StartsWith("ya", StringComparison.Ordinal) ||
               pixelFormat is "pal8" or "ayuv64le" or "ayuv64be" or "vuya";
    }

    internal static int BitDepthOf(string pixelFormat)
    {
        if (string.IsNullOrEmpty(pixelFormat)) return 0;
        if (pixelFormat.StartsWith("p010", StringComparison.Ordinal)) return 10;
        if (pixelFormat.StartsWith("p012", StringComparison.Ordinal)) return 12;
        if (pixelFormat.StartsWith("p016", StringComparison.Ordinal)) return 16;

        Match match = DepthSuffix.Match(pixelFormat);
        if (match.Success)
        {
            int bits = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            // Suffixes such as "rgb48le" / "rgba64le" give the whole pixel, not one sample.
            if (bits >= 9 && bits <= 16) return bits;
            if (bits == 48 || bits == 64) return 16;
        }

        return 8;
    }

    // Normalises any angle to 0..359 and snaps it to the nearest quarter turn.
    internal static int NormaliseQuarterTurn(double degrees)
    {
        int quarter = (int)Math.Round(degrees / 90.0, MidpointRounding.AwayFromZero);
        return ((quarter % 4) + 4) % 4 * 90;
    }
}
