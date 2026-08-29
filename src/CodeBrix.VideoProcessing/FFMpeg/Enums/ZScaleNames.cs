using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Enums;

/// <summary>
///     Transfer-characteristic names accepted by FFmpeg's <c>zscale</c> filter (<c>t</c> option).
/// </summary>
/// <remarks>These are plain strings; any other name FFmpeg accepts can be passed directly.</remarks>
public static class ZScaleTransfer
{
    /// <summary>Keep the input's transfer characteristic (<c>input</c>).</summary>
    public const string Input = "input";

    /// <summary>Linear light (<c>linear</c>).</summary>
    public const string Linear = "linear";

    /// <summary>ITU-R BT.709 (<c>bt709</c>).</summary>
    public const string Bt709 = "bt709";

    /// <summary>SMPTE 170M (<c>smpte170m</c>).</summary>
    public const string Smpte170M = "smpte170m";

    /// <summary>ITU-R BT.2020 10-bit (<c>bt2020-10</c>).</summary>
    public const string Bt2020Ten = "bt2020-10";

    /// <summary>ITU-R BT.2020 12-bit (<c>bt2020-12</c>).</summary>
    public const string Bt2020Twelve = "bt2020-12";

    /// <summary>SMPTE ST 2084 / PQ (<c>smpte2084</c>).</summary>
    public const string Smpte2084 = "smpte2084";

    /// <summary>ARIB STD-B67 / HLG (<c>arib-std-b67</c>).</summary>
    public const string AribStdB67 = "arib-std-b67";
}

/// <summary>
///     Colour-primaries names accepted by FFmpeg's <c>zscale</c> filter (<c>p</c> option).
/// </summary>
/// <remarks>These are plain strings; any other name FFmpeg accepts can be passed directly.</remarks>
public static class ZScalePrimaries
{
    /// <summary>Keep the input's primaries (<c>input</c>).</summary>
    public const string Input = "input";

    /// <summary>ITU-R BT.709 (<c>bt709</c>).</summary>
    public const string Bt709 = "bt709";

    /// <summary>ITU-R BT.470BG / EBU 3213 (<c>bt470bg</c>).</summary>
    public const string Bt470Bg = "bt470bg";

    /// <summary>SMPTE 170M (<c>smpte170m</c>).</summary>
    public const string Smpte170M = "smpte170m";

    /// <summary>ITU-R BT.2020 (<c>bt2020</c>).</summary>
    public const string Bt2020 = "bt2020";

    /// <summary>SMPTE 431 / DCI-P3 (<c>smpte431</c>).</summary>
    public const string Smpte431 = "smpte431";

    /// <summary>SMPTE 432 / Display P3 (<c>smpte432</c>).</summary>
    public const string Smpte432 = "smpte432";
}

/// <summary>
///     Colour-matrix names accepted by FFmpeg's <c>zscale</c> filter (<c>m</c> option).
/// </summary>
/// <remarks>These are plain strings; any other name FFmpeg accepts can be passed directly.</remarks>
public static class ZScaleMatrix
{
    /// <summary>Keep the input's matrix (<c>input</c>).</summary>
    public const string Input = "input";

    /// <summary>Identity / GBR (<c>gbr</c>).</summary>
    public const string Gbr = "gbr";

    /// <summary>ITU-R BT.709 (<c>bt709</c>).</summary>
    public const string Bt709 = "bt709";

    /// <summary>ITU-R BT.470BG (<c>bt470bg</c>).</summary>
    public const string Bt470Bg = "bt470bg";

    /// <summary>SMPTE 170M (<c>smpte170m</c>).</summary>
    public const string Smpte170M = "smpte170m";

    /// <summary>ITU-R BT.2020 non-constant luminance (<c>bt2020nc</c>).</summary>
    public const string Bt2020Ncl = "bt2020nc";

    /// <summary>ITU-R BT.2020 constant luminance (<c>bt2020c</c>).</summary>
    public const string Bt2020Cl = "bt2020c";
}

/// <summary>
///     Colour-range names accepted by FFmpeg's <c>zscale</c> filter (<c>r</c> option).
/// </summary>
/// <remarks>These are plain strings; any other name FFmpeg accepts can be passed directly.</remarks>
public static class ZScaleRange
{
    /// <summary>Keep the input's range (<c>input</c>).</summary>
    public const string Input = "input";

    /// <summary>Limited / broadcast range (<c>limited</c>).</summary>
    public const string Limited = "limited";

    /// <summary>Full / PC range (<c>full</c>).</summary>
    public const string Full = "full";

    /// <summary>Limited range, spelled the way FFmpeg's stream metadata spells it (<c>tv</c>).</summary>
    public const string Tv = "tv";

    /// <summary>Full range, spelled the way FFmpeg's stream metadata spells it (<c>pc</c>).</summary>
    public const string Pc = "pc";
}

/// <summary>
///     Dither-mode names accepted by FFmpeg's <c>zscale</c> filter (<c>d</c> option).
/// </summary>
/// <remarks>These are plain strings; any other name FFmpeg accepts can be passed directly.</remarks>
public static class ZScaleDither
{
    /// <summary>No dithering (<c>none</c>). FFmpeg's default.</summary>
    public const string None = "none";

    /// <summary>Ordered dithering (<c>ordered</c>).</summary>
    public const string Ordered = "ordered";

    /// <summary>Random dithering (<c>random</c>).</summary>
    public const string Random = "random";

    /// <summary>Error-diffusion dithering (<c>error_diffusion</c>).</summary>
    public const string ErrorDiffusion = "error_diffusion";
}
