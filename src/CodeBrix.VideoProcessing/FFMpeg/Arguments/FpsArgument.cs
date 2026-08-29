using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments;

/// <summary>
///     Represents FFmpeg's <c>fps</c> video filter, which forces a constant output frame rate by
///     duplicating or dropping frames.
/// </summary>
/// <remarks>
///     Emits <c>fps=30</c> for a numeric rate, or <c>fps=&lt;expression&gt;</c> for one of FFmpeg's
///     abbreviations (<c>ntsc</c>, <c>pal</c>, <c>film</c>) or ratios (<c>30000/1001</c>).
/// </remarks>
public class FpsArgument : IVideoFilterArgument, IPreEscapedFilterArgument
{
    private readonly string _fps;

    /// <summary>
    ///     Sets a numeric frame rate.
    /// </summary>
    /// <param name="fps">
    ///     Frames per second. Formatted with the invariant culture, so a whole number prints without
    ///     a decimal point and a decimal separator is never a comma.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fps" /> is not a positive, finite number.</exception>
    public FpsArgument(double fps)
    {
        if (double.IsNaN(fps) || double.IsInfinity(fps) || fps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fps), fps, "A frame rate must be a positive, finite number");
        }

        _fps = fps.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Sets the frame rate from an FFmpeg frame-rate expression.
    /// </summary>
    /// <param name="fps">
    ///     An FFmpeg frame-rate string such as <c>ntsc</c>, <c>pal</c>, <c>film</c>, <c>30000/1001</c>
    ///     or <c>source_fps</c>. The value is filtergraph-escaped.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="fps" /> is null or empty.</exception>
    public FpsArgument(string fps)
    {
        if (string.IsNullOrWhiteSpace(fps))
        {
            throw new ArgumentException("A frame rate is required", nameof(fps));
        }

        _fps = FilterGraphEscaper.EscapeValue(fps);
    }

    /// <summary>The filter name, <c>fps</c>.</summary>
    public string Key => "fps";

    /// <summary>The frame rate, formatted for FFmpeg.</summary>
    public string Value => _fps;
}
