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
///     Represents a single <c>-metadata</c> assignment - on the output as a whole, or on one of its
///     streams.
/// </summary>
/// <remarks>
///     <para>
///         Emits <c>-metadata title="A name"</c>, <c>-metadata:s:0 language="eng"</c> or
///         <c>-metadata:s:s:0 language="eng"</c> depending on which constructor is used. The value
///         is always wrapped in double quotes, and any double quote inside it is backslash-escaped,
///         so a value with spaces or punctuation survives the command line.
///     </para>
///     <para>
///         Whole metadata documents - tags plus chapters - go through the ffmetadata input instead
///         (<c>AddMetaData</c>); this argument is for the one-off assignments that ffmetadata cannot
///         express, above all per-stream <c>language</c> and <c>title</c> tags.
///     </para>
/// </remarks>
public class SetMetadataArgument : IArgument
{
    private readonly string _key;
    private readonly string _streamSpecifier;
    private readonly string _value;

    /// <summary>
    ///     Sets a metadata tag on the output as a whole.
    /// </summary>
    /// <param name="key">The tag name, for example <c>title</c>.</param>
    /// <param name="value">The tag value. Null is treated as an empty string, which clears the tag.</param>
    /// <exception cref="ArgumentException"><paramref name="key" /> is null or empty.</exception>
    public SetMetadataArgument(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A metadata key is required", nameof(key));
        }

        _streamSpecifier = string.Empty;
        _key = key;
        _value = value ?? string.Empty;
    }

    /// <summary>
    ///     Sets a metadata tag on one output stream, identified by its index among all streams.
    /// </summary>
    /// <param name="streamIndex">The zero-based output stream index.</param>
    /// <param name="key">The tag name, for example <c>language</c>.</param>
    /// <param name="value">The tag value. Null is treated as an empty string, which clears the tag.</param>
    /// <exception cref="ArgumentException"><paramref name="key" /> is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="streamIndex" /> is negative.</exception>
    public SetMetadataArgument(int streamIndex, string key, string value)
        : this(Channel.All, streamIndex, key, value) { }

    /// <summary>
    ///     Sets a metadata tag on one output stream, identified by its index among the streams of a
    ///     single type.
    /// </summary>
    /// <param name="channel">
    ///     The stream type the index counts within. <c>Channel.All</c> and <c>Channel.Both</c> leave
    ///     the type out, so the index counts across every stream.
    /// </param>
    /// <param name="streamIndex">The zero-based stream index within that type.</param>
    /// <param name="key">The tag name, for example <c>language</c>.</param>
    /// <param name="value">The tag value. Null is treated as an empty string, which clears the tag.</param>
    /// <exception cref="ArgumentException"><paramref name="key" /> is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="streamIndex" /> is negative.</exception>
    public SetMetadataArgument(Channel channel, int streamIndex, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A metadata key is required", nameof(key));
        }

        if (streamIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(streamIndex), streamIndex, "A stream index cannot be negative");
        }

        _streamSpecifier = $":s{channel.StreamType()}:{streamIndex.ToString(CultureInfo.InvariantCulture)}";
        _key = key;
        _value = value ?? string.Empty;
    }

    /// <summary>The emitted text, for example <c>-metadata:s:s:0 language="eng"</c>.</summary>
    public string Text => $"-metadata{_streamSpecifier} {_key}=\"{_value.Replace("\"", "\\\"")}\"";
}
