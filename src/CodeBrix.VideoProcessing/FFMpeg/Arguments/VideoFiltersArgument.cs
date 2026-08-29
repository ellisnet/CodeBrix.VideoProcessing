using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Exceptions;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

public class VideoFiltersArgument : IArgument
{
    public readonly VideoFilterOptions Options;

    public VideoFiltersArgument(VideoFilterOptions options)
    {
        Options = options;
    }

    public string Text => GetText();

    private string GetText()
    {
        if (!Options.Arguments.Any())
        {
            throw new FFMpegArgumentException("No video-filter arguments provided");
        }

        var arguments = Options.Arguments
            .Where(arg => !string.IsNullOrEmpty(arg.Value))
            .Select(arg =>
            {
                //Arguments that carry IPreEscapedFilterArgument have already applied the full
                //filtergraph escaping rules to their own value; escaping their commas again here
                //would turn "\," into "\\," and hand ffmpeg a stray backslash.
                var escapedValue = arg is IPreEscapedFilterArgument ? arg.Value : arg.Value.Replace(",", "\\,");
                return string.IsNullOrEmpty(arg.Key) ? escapedValue : $"{arg.Key}={escapedValue}";
            });

        return $"-vf \"{string.Join(", ", arguments)}\"";
    }
}

public interface IVideoFilterArgument
{
    string Key { get; }
    string Value { get; }
}

public class VideoFilterOptions
{
    public List<IVideoFilterArgument> Arguments { get; } = new();

    public VideoFilterOptions Scale(VideoSize videoSize)
    {
        return WithArgument(new ScaleArgument(videoSize));
    }

    public VideoFilterOptions Scale(int width, int height)
    {
        return WithArgument(new ScaleArgument(width, height));
    }

    public VideoFilterOptions Scale(Size size)
    {
        return WithArgument(new ScaleArgument(size));
    }

    public VideoFilterOptions Transpose(Transposition transposition)
    {
        return WithArgument(new TransposeArgument(transposition));
    }

    public VideoFilterOptions Mirror(Mirroring mirroring)
    {
        return WithArgument(new SetMirroringArgument(mirroring));
    }

    public VideoFilterOptions DrawText(DrawTextOptions drawTextOptions)
    {
        return WithArgument(new DrawTextArgument(drawTextOptions));
    }

    public VideoFilterOptions HardBurnSubtitle(SubtitleHardBurnOptions subtitleHardBurnOptions)
    {
        return WithArgument(new SubtitleHardBurnArgument(subtitleHardBurnOptions));
    }

    public VideoFilterOptions BlackDetect(double minimumDuration = 2.0, double pictureBlackRatioThreshold = 0.98, double pixelBlackThreshold = 0.1)
    {
        return WithArgument(new BlackDetectArgument(minimumDuration, pictureBlackRatioThreshold, pixelBlackThreshold));
    }

    public VideoFilterOptions BlackFrame(int amount = 98, int threshold = 32)
    {
        return WithArgument(new BlackFrameArgument(amount, threshold));
    }

    public VideoFilterOptions Pad(PadOptions padOptions)
    {
        return WithArgument(new PadArgument(padOptions));
    }

    /// <summary>
    ///     Adds a filter from raw filtergraph text, emitted exactly as written.
    /// </summary>
    /// <param name="rawFilterText">
    ///     The complete filter text, for example <c>negate</c> or <c>eq=contrast=1.2</c>. NOTHING is
    ///     escaped: the caller owns every backslash the filtergraph parser needs. Use this when the
    ///     text is a literal that is already correct, and one of the other overloads when a value
    ///     comes from a path, a user or a file.
    /// </param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public VideoFilterOptions Custom(string rawFilterText)
    {
        return WithArgument(new CustomFilterArgument(rawFilterText));
    }

    /// <summary>
    ///     Adds a filter from a name and an already-formed FFmpeg option string.
    /// </summary>
    /// <param name="filterName">The filter name, for example <c>drawbox</c>.</param>
    /// <param name="options">
    ///     The options as FFmpeg spells them, for example <c>x=10:y=10:w=100:h=100</c>. Only the
    ///     characters that would end the filter - <c>,</c> <c>;</c> <c>[</c> <c>]</c> - are escaped;
    ///     <c>:</c> and <c>=</c> pass through so they keep separating the options. Null or empty
    ///     emits the bare filter name.
    /// </param>
    /// <returns>The same options object, so calls can be chained.</returns>
    /// <remarks>
    ///     Because the escaping is not aware of quoting, a comma inside a <c>'...'</c> section of the
    ///     option string is escaped too. When that matters, build the text yourself and use the raw
    ///     overload.
    /// </remarks>
    public VideoFilterOptions Custom(string filterName, string options)
    {
        return WithArgument(new CustomFilterArgument(filterName, options));
    }

    /// <summary>
    ///     Adds a filter from a name and a set of key/value options, escaping each value fully.
    /// </summary>
    /// <param name="filterName">The filter name, for example <c>lut3d</c>.</param>
    /// <param name="options">
    ///     The options as key/value pairs, joined with <c>:</c> in the order given. Every VALUE is
    ///     fully filtergraph-escaped, so it may contain <c>:</c>, <c>\</c>, <c>,</c>, <c>'</c>,
    ///     <c>;</c>, <c>[</c>, <c>]</c> or <c>=</c>; the keys are emitted verbatim.
    /// </param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public VideoFilterOptions Custom(string filterName, params (string Key, string Value)[] options)
    {
        return WithArgument(new CustomFilterArgument(filterName, options));
    }

    /// <summary>
    ///     Forces a constant output frame rate with FFmpeg's <c>fps</c> filter.
    /// </summary>
    /// <param name="fps">Frames per second, formatted with the invariant culture.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public VideoFilterOptions Fps(double fps)
    {
        return WithArgument(new FpsArgument(fps));
    }

    /// <summary>
    ///     Forces a constant output frame rate from an FFmpeg frame-rate expression.
    /// </summary>
    /// <param name="fps">
    ///     An FFmpeg frame-rate string such as <c>ntsc</c>, <c>pal</c>, <c>film</c> or
    ///     <c>30000/1001</c>.
    /// </param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public VideoFilterOptions Fps(string fps)
    {
        return WithArgument(new FpsArgument(fps));
    }

    /// <summary>
    ///     Applies a 3D colour lookup table with FFmpeg's <c>lut3d</c> filter.
    /// </summary>
    /// <param name="cubePath">Path to the LUT file; it is fully filtergraph-escaped.</param>
    /// <param name="interpolation">The interpolation mode; defaults to tetrahedral, as FFmpeg does.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public VideoFilterOptions Lut3D(string cubePath, Lut3DInterpolation interpolation = Lut3DInterpolation.Tetrahedral)
    {
        return WithArgument(new Lut3DArgument(cubePath, interpolation));
    }

    /// <summary>
    ///     Converts the frames flowing through the chain to a pixel format, with FFmpeg's
    ///     <c>format</c> filter.
    /// </summary>
    /// <param name="pixelFormat">A pixel format reported by the installed FFmpeg.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public VideoFilterOptions Format(PixelFormat pixelFormat)
    {
        return WithArgument(new FormatFilterArgument(pixelFormat));
    }

    /// <summary>
    ///     Converts the frames flowing through the chain to a named pixel format, with FFmpeg's
    ///     <c>format</c> filter.
    /// </summary>
    /// <param name="pixelFormat">An FFmpeg pixel-format name such as <c>yuv420p</c>.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public VideoFilterOptions Format(string pixelFormat)
    {
        return WithArgument(new FormatFilterArgument(pixelFormat));
    }

    /// <summary>
    ///     Resizes and converts colour space with FFmpeg's <c>zscale</c> filter.
    /// </summary>
    /// <param name="options">The options to emit; at least one of them must be set.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public VideoFilterOptions ZScale(ZScaleOptions options)
    {
        return WithArgument(new ZScaleArgument(options));
    }

    /// <summary>
    ///     Resizes and converts colour space with FFmpeg's <c>zscale</c> filter.
    /// </summary>
    /// <param name="configure">Callback that sets the options; at least one of them must be set.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure" /> is null.</exception>
    public VideoFilterOptions ZScale(Action<ZScaleOptions> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var zScaleOptions = new ZScaleOptions();
        configure(zScaleOptions);
        return WithArgument(new ZScaleArgument(zScaleOptions));
    }

    /// <summary>
    ///     Maps one dynamic range onto another with FFmpeg's <c>tonemap</c> filter.
    /// </summary>
    /// <param name="algorithm">The tone-mapping curve.</param>
    /// <param name="param">The curve's tunable parameter; omitted when null.</param>
    /// <param name="desat">Desaturation strength, 0 to disable; omitted when null.</param>
    /// <param name="peak">Signal peak override; omitted when null.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    /// <remarks>
    ///     The filter expects linear light, so it usually sits between a <c>zscale</c> that converts
    ///     to linear and one that converts back.
    /// </remarks>
    public VideoFilterOptions Tonemap(TonemapAlgorithm algorithm, double? param = null, double? desat = null, double? peak = null)
    {
        return WithArgument(new TonemapArgument(algorithm, param, desat, peak));
    }

    /// <summary>
    ///     Rotates the picture by a fixed angle with FFmpeg's <c>rotate</c> filter.
    /// </summary>
    /// <param name="radians">The clockwise rotation in radians.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    /// <remarks>For the four right-angle cases <c>Transpose</c> is cheaper and lossless.</remarks>
    public VideoFilterOptions Rotate(double radians)
    {
        return WithArgument(new RotateArgument(radians));
    }

    /// <summary>
    ///     Rotates the picture by an FFmpeg angle expression with the <c>rotate</c> filter.
    /// </summary>
    /// <param name="expression">An expression evaluated per frame, such as <c>PI/2</c>.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public VideoFilterOptions Rotate(string expression)
    {
        return WithArgument(new RotateArgument(expression));
    }

    private VideoFilterOptions WithArgument(IVideoFilterArgument argument)
    {
        Arguments.Add(argument);
        return this;
    }
}
