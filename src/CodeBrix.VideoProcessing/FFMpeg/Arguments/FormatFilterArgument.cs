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
///     Represents FFmpeg's <c>format</c> video filter, which converts the frames flowing through a
///     filter chain to a given pixel format.
/// </summary>
/// <remarks>
///     Emits <c>format=yuv420p</c>. This is the in-chain conversion; the encoder-level
///     <c>-pix_fmt</c> option is <c>ForcePixelFormat</c> instead.
/// </remarks>
public class FormatFilterArgument : IVideoFilterArgument, IPreEscapedFilterArgument
{
    private readonly string _pixelFormat;

    /// <summary>
    ///     Converts to the named pixel format.
    /// </summary>
    /// <param name="pixelFormat">An FFmpeg pixel-format name such as <c>yuv420p</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="pixelFormat" /> is null or empty.</exception>
    public FormatFilterArgument(string pixelFormat)
    {
        if (string.IsNullOrWhiteSpace(pixelFormat))
        {
            throw new ArgumentException("A pixel format is required", nameof(pixelFormat));
        }

        _pixelFormat = pixelFormat;
    }

    /// <summary>
    ///     Converts to a pixel format reported by the installed FFmpeg.
    /// </summary>
    /// <param name="pixelFormat">The pixel format.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pixelFormat" /> is null.</exception>
    public FormatFilterArgument(PixelFormat pixelFormat)
    {
        if (pixelFormat == null)
        {
            throw new ArgumentNullException(nameof(pixelFormat));
        }

        _pixelFormat = pixelFormat.Name;
    }

    /// <summary>The filter name, <c>format</c>.</summary>
    public string Key => "format";

    /// <summary>The escaped pixel-format name.</summary>
    public string Value => FilterGraphEscaper.EscapeValue(_pixelFormat);
}
