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
///     Represents FFmpeg's <c>rotate</c> video filter, which rotates the picture by an arbitrary
///     angle.
/// </summary>
/// <remarks>
///     <para>
///         Emits <c>rotate=&lt;angle&gt;</c>, where the angle is in RADIANS and turns the picture
///         clockwise. For the four right-angle cases the <c>transpose</c> filter is cheaper and
///         lossless - see <c>Transpose</c>.
///     </para>
/// </remarks>
public class RotateArgument : IVideoFilterArgument, IPreEscapedFilterArgument
{
    private readonly string _angle;

    /// <summary>
    ///     Rotates by a fixed angle.
    /// </summary>
    /// <param name="radians">
    ///     The clockwise rotation in radians. Formatted with the invariant culture, so the decimal
    ///     separator is never a comma.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radians" /> is not a finite number.</exception>
    public RotateArgument(double radians)
    {
        if (double.IsNaN(radians) || double.IsInfinity(radians))
        {
            throw new ArgumentOutOfRangeException(nameof(radians), radians, "An angle must be a finite number");
        }

        _angle = radians.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Rotates by an FFmpeg angle expression.
    /// </summary>
    /// <param name="expression">
    ///     An FFmpeg expression evaluated per frame, such as <c>PI/2</c> or <c>-PI/6</c>. The value
    ///     is filtergraph-escaped.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="expression" /> is null or empty.</exception>
    public RotateArgument(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("An angle expression is required", nameof(expression));
        }

        _angle = FilterGraphEscaper.EscapeValue(expression);
    }

    /// <summary>The filter name, <c>rotate</c>.</summary>
    public string Key => "rotate";

    /// <summary>The angle, formatted for FFmpeg.</summary>
    public string Value => _angle;
}
