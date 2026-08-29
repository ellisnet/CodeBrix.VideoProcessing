using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Exceptions;

namespace CodeBrix.VideoProcessing.Arguments;

/// <summary>
///     Represents FFmpeg's <c>zscale</c> video filter (the zimg-backed scaler), which resizes and
///     converts colour space, transfer characteristic, primaries, matrix, range and bit depth.
/// </summary>
/// <remarks>
///     <para>
///         Emits the options that were set, always in the same order:
///         <c>w</c>, <c>h</c>, <c>s</c>, <c>t</c>, <c>p</c>, <c>m</c>, <c>r</c>, <c>npl</c>, <c>d</c>
///         - for example <c>zscale=t=linear:npl=100</c>.
///     </para>
///     <para>
///         The filter is only present when FFmpeg was built with libzimg; a build without it fails
///         at run time with "No such filter: 'zscale'".
///     </para>
/// </remarks>
public class ZScaleArgument : IVideoFilterArgument, IPreEscapedFilterArgument
{
    /// <summary>
    ///     Applies the given zscale options.
    /// </summary>
    /// <param name="options">The options to emit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is null.</exception>
    public ZScaleArgument(ZScaleOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>The options this argument emits.</summary>
    public ZScaleOptions Options { get; }

    /// <summary>The filter name, <c>zscale</c>.</summary>
    public string Key => "zscale";

    /// <summary>The escaped option string, in the documented stable order.</summary>
    /// <exception cref="FFMpegArgumentException">No zscale option was set.</exception>
    public string Value => Options.TextInternal;
}

/// <summary>
///     The options of FFmpeg's <c>zscale</c> filter. Every option is optional; only the ones that
///     were set are emitted.
/// </summary>
/// <remarks>
///     The colour-related values are FFmpeg's own names, passed through as strings. The
///     <c>ZScaleTransfer</c>, <c>ZScalePrimaries</c>, <c>ZScaleMatrix</c>, <c>ZScaleRange</c> and
///     <c>ZScaleDither</c> classes hold the common ones, and any other name the installed FFmpeg
///     accepts can be used directly.
/// </remarks>
public class ZScaleOptions
{
    /// <summary>Output width, emitted as <c>w</c>. Ignored when <see cref="Size" /> is set.</summary>
    public int? Width { get; set; }

    /// <summary>Output height, emitted as <c>h</c>. Ignored when <see cref="Size" /> is set.</summary>
    public int? Height { get; set; }

    /// <summary>
    ///     Output size, emitted as <c>s=WIDTHxHEIGHT</c>. When set it replaces
    ///     <see cref="Width" /> and <see cref="Height" />.
    /// </summary>
    public Size? Size { get; set; }

    /// <summary>Output transfer characteristic, emitted as <c>t</c> - for example <c>linear</c>.</summary>
    public string Transfer { get; set; }

    /// <summary>Output colour primaries, emitted as <c>p</c> - for example <c>bt2020</c>.</summary>
    public string Primaries { get; set; }

    /// <summary>Output colour matrix, emitted as <c>m</c> - for example <c>bt709</c>.</summary>
    public string Matrix { get; set; }

    /// <summary>Output colour range, emitted as <c>r</c> - for example <c>tv</c> or <c>pc</c>.</summary>
    public string Range { get; set; }

    /// <summary>Nominal peak luminance in cd/m2, emitted as <c>npl</c> - for example <c>100</c>.</summary>
    public double? Npl { get; set; }

    /// <summary>Dither mode, emitted as <c>d</c> - for example <c>error_diffusion</c>.</summary>
    public string DitherMode { get; set; }

    internal string TextInternal
    {
        get
        {
            var parts = new List<string>();

            if (Size.HasValue)
            {
                parts.Add($"s={Size.Value.Width.ToString(CultureInfo.InvariantCulture)}x{Size.Value.Height.ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                if (Width.HasValue)
                {
                    parts.Add($"w={Width.Value.ToString(CultureInfo.InvariantCulture)}");
                }

                if (Height.HasValue)
                {
                    parts.Add($"h={Height.Value.ToString(CultureInfo.InvariantCulture)}");
                }
            }

            AddIfSet(parts, "t", Transfer);
            AddIfSet(parts, "p", Primaries);
            AddIfSet(parts, "m", Matrix);
            AddIfSet(parts, "r", Range);

            if (Npl.HasValue)
            {
                if (double.IsNaN(Npl.Value) || double.IsInfinity(Npl.Value))
                {
                    throw new FFMpegArgumentException("The zscale nominal peak luminance must be a finite number");
                }

                parts.Add($"npl={Npl.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            AddIfSet(parts, "d", DitherMode);

            if (parts.Count == 0)
            {
                throw new FFMpegArgumentException("At least one zscale option must be set");
            }

            return string.Join(":", parts);
        }
    }

    private static void AddIfSet(List<string> parts, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{key}={FilterGraphEscaper.EscapeValue(value)}");
        }
    }
}
