using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;

namespace CodeBrix.VideoProcessing.Arguments;

/// <summary>
///     Represents FFmpeg's <c>lut3d</c> video filter, which applies a 3D colour lookup table.
/// </summary>
/// <remarks>
///     Emits <c>lut3d=file=&lt;path&gt;:interp=&lt;mode&gt;</c>. The path is fully
///     filtergraph-escaped, so a Windows path such as <c>C:\luts\warm.cube</c> and a path
///     containing a comma or a space both survive.
/// </remarks>
public class Lut3DArgument : IVideoFilterArgument, IPreEscapedFilterArgument
{
    private readonly string _cubePath;

    /// <summary>
    ///     Applies a 3D LUT file.
    /// </summary>
    /// <param name="cubePath">
    ///     Path to the LUT file. FFmpeg reads the 3D LUT formats its build supports (.cube, .3dl,
    ///     .dat, .m3d, .csp); the path is not checked here.
    /// </param>
    /// <param name="interpolation">The interpolation mode. Defaults to FFmpeg's own default, tetrahedral.</param>
    /// <exception cref="ArgumentException"><paramref name="cubePath" /> is null or empty.</exception>
    public Lut3DArgument(string cubePath, Lut3DInterpolation interpolation = Lut3DInterpolation.Tetrahedral)
    {
        if (string.IsNullOrWhiteSpace(cubePath))
        {
            throw new ArgumentException("A LUT file path is required", nameof(cubePath));
        }

        _cubePath = cubePath;
        Interpolation = interpolation;
    }

    /// <summary>The interpolation mode passed to the filter's <c>interp</c> option.</summary>
    public Lut3DInterpolation Interpolation { get; }

    /// <summary>The unescaped LUT file path this argument was created with.</summary>
    public string CubePath => _cubePath;

    /// <summary>The filter name, <c>lut3d</c>.</summary>
    public string Key => "lut3d";

    /// <summary>The escaped <c>file=...:interp=...</c> option string.</summary>
    public string Value => $"file={FilterGraphEscaper.EscapeValue(_cubePath)}:interp={InterpolationName()}";

    private string InterpolationName()
    {
        return Interpolation switch
        {
            Lut3DInterpolation.Nearest => "nearest",
            Lut3DInterpolation.Trilinear => "trilinear",
            Lut3DInterpolation.Tetrahedral => "tetrahedral",
            Lut3DInterpolation.Pyramid => "pyramid",
            Lut3DInterpolation.Prism => "prism",
            _ => throw new ArgumentOutOfRangeException(nameof(Interpolation))
        };
    }
}
