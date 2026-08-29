using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Enums;

/// <summary>
///     Interpolation mode used by FFmpeg's <c>lut3d</c> filter, emitted as its <c>interp</c> option.
/// </summary>
public enum Lut3DInterpolation
{
    /// <summary>Use the values of the nearest defined point (<c>interp=nearest</c>).</summary>
    Nearest,

    /// <summary>Interpolate using the eight points defining a cube (<c>interp=trilinear</c>).</summary>
    Trilinear,

    /// <summary>Interpolate using a tetrahedron (<c>interp=tetrahedral</c>). FFmpeg's default.</summary>
    Tetrahedral,

    /// <summary>Interpolate using a pyramid (<c>interp=pyramid</c>).</summary>
    Pyramid,

    /// <summary>Interpolate using a prism (<c>interp=prism</c>).</summary>
    Prism
}
