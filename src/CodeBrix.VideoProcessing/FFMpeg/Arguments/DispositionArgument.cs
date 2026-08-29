using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;

namespace CodeBrix.VideoProcessing.Arguments;

/// <summary>
///     Represents FFmpeg's <c>-disposition</c> output option, which sets the flags a player uses to
///     decide which stream to pick by default.
/// </summary>
/// <remarks>
///     Emits <c>-disposition:s:0 default+forced</c>, or <c>-disposition:0 default</c> when no stream
///     type is given. Setting a disposition REPLACES whatever the stream had, so every flag that
///     should stay must be listed again; <c>Disposition.None</c> emits <c>0</c> and clears them all.
/// </remarks>
public class DispositionArgument : IArgument
{
    private readonly Disposition[] _flags;
    private readonly string _streamSpecifier;

    /// <summary>
    ///     Sets the disposition of one output stream, identified by its index among all streams.
    /// </summary>
    /// <param name="streamIndex">The zero-based output stream index.</param>
    /// <param name="flags">
    ///     The flags to set. No flags at all, or a set containing <c>Disposition.None</c>, clears
    ///     every flag.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="streamIndex" /> is negative.</exception>
    public DispositionArgument(int streamIndex, params Disposition[] flags)
        : this(Channel.All, streamIndex, flags) { }

    /// <summary>
    ///     Sets the disposition of one output stream, identified by its index among the streams of a
    ///     single type.
    /// </summary>
    /// <param name="channel">
    ///     The stream type the index counts within. <c>Channel.All</c> and <c>Channel.Both</c> leave
    ///     the type out, so the index counts across every stream.
    /// </param>
    /// <param name="streamIndex">The zero-based stream index within that type.</param>
    /// <param name="flags">
    ///     The flags to set. No flags at all, or a set containing <c>Disposition.None</c>, clears
    ///     every flag.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="streamIndex" /> is negative.</exception>
    public DispositionArgument(Channel channel, int streamIndex, params Disposition[] flags)
    {
        if (streamIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(streamIndex), streamIndex, "A stream index cannot be negative");
        }

        _streamSpecifier = $"{channel.StreamType()}:{streamIndex.ToString(CultureInfo.InvariantCulture)}";
        _flags = flags ?? Array.Empty<Disposition>();
    }

    /// <summary>The emitted text, for example <c>-disposition:s:0 default+forced</c>.</summary>
    public string Text => $"-disposition{_streamSpecifier} {FlagText()}";

    private string FlagText()
    {
        if (_flags.Length == 0 || _flags.Any(flag => flag == Disposition.None))
        {
            return Disposition.None.FlagName();
        }

        return string.Join("+", _flags.Select(flag => flag.FlagName()));
    }
}
