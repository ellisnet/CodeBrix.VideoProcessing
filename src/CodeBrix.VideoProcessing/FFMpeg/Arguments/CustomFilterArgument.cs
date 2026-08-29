using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments;

/// <summary>
///     A filter that is not modelled by a typed argument: the caller supplies the filter name and,
///     optionally, its options.
/// </summary>
/// <remarks>
///     <para>
///         Three shapes are available, and they escape differently. The rule, in one line: the
///         key/value shape escapes each VALUE completely, the option-string shape escapes only the
///         characters that would end the filter, and the raw shape escapes nothing.
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 <c>CustomFilterArgument("lut3d", ("file", path), ("interp", "nearest"))</c> -
///                 each value is fully filtergraph-escaped, so a path with <c>:</c>, <c>\</c>,
///                 <c>,</c> or <c>'</c> in it arrives intact. The keys are emitted verbatim.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <c>CustomFilterArgument("drawbox", "x=10:y=10:w=100:h=100")</c> - the options
///                 string is treated as already-formed FFmpeg syntax, so its <c>:</c> separators and
///                 <c>=</c> signs pass through untouched; only <c>,</c> <c>;</c> <c>[</c> <c>]</c>
///                 are escaped, because those would otherwise end the filter.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <c>CustomFilterArgument("negate")</c> - the text is emitted exactly as written
///                 and the caller owns every escape in it.
///             </description>
///         </item>
///     </list>
/// </remarks>
public class CustomFilterArgument : IVideoFilterArgument, IAudioFilterArgument, IPreEscapedFilterArgument
{
    private readonly string _key;
    private readonly string _value;

    /// <summary>
    ///     Creates a filter from raw filtergraph text that is emitted verbatim.
    /// </summary>
    /// <param name="rawFilterText">
    ///     The complete filter text, for example <c>negate</c> or <c>eq=contrast=1.2</c>. Nothing is
    ///     escaped: the caller is responsible for every backslash the filtergraph parser needs.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="rawFilterText" /> is null or empty.</exception>
    public CustomFilterArgument(string rawFilterText)
    {
        if (string.IsNullOrWhiteSpace(rawFilterText))
        {
            throw new ArgumentException("A filter text is required", nameof(rawFilterText));
        }

        _key = string.Empty;
        _value = rawFilterText;
    }

    /// <summary>
    ///     Creates a filter from a name and an already-formed FFmpeg option string.
    /// </summary>
    /// <param name="filterName">The filter name, for example <c>drawbox</c>.</param>
    /// <param name="options">
    ///     The options as FFmpeg spells them, for example <c>x=10:y=10:w=100:h=100</c>. Only
    ///     <c>,</c> <c>;</c> <c>[</c> <c>]</c> are escaped; <c>:</c> and <c>=</c> pass through so
    ///     they keep separating the options. Null or empty emits the bare filter name.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="filterName" /> is null or empty.</exception>
    public CustomFilterArgument(string filterName, string options)
    {
        if (string.IsNullOrWhiteSpace(filterName))
        {
            throw new ArgumentException("A filter name is required", nameof(filterName));
        }

        if (string.IsNullOrEmpty(options))
        {
            _key = string.Empty;
            _value = filterName;
        }
        else
        {
            _key = filterName;
            _value = FilterGraphEscaper.EscapeChainSeparators(options);
        }
    }

    /// <summary>
    ///     Creates a filter from a name and a set of key/value options, escaping each value fully.
    /// </summary>
    /// <param name="filterName">The filter name, for example <c>lut3d</c>.</param>
    /// <param name="options">
    ///     The options as key/value pairs. Every value is fully filtergraph-escaped, so a value may
    ///     contain <c>:</c>, <c>\</c>, <c>,</c>, <c>'</c>, <c>;</c>, <c>[</c>, <c>]</c> or <c>=</c>.
    ///     The keys are emitted verbatim. A null value is treated as an empty string. No pairs at
    ///     all emits the bare filter name.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     <paramref name="filterName" /> is null or empty, or an option key is null or empty.
    /// </exception>
    public CustomFilterArgument(string filterName, params (string Key, string Value)[] options)
    {
        if (string.IsNullOrWhiteSpace(filterName))
        {
            throw new ArgumentException("A filter name is required", nameof(filterName));
        }

        if (options == null || options.Length == 0)
        {
            _key = string.Empty;
            _value = filterName;
            return;
        }

        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option.Key))
            {
                throw new ArgumentException("Every filter option needs a key", nameof(options));
            }
        }

        _key = filterName;
        _value = string.Join(":", options.Select(option => $"{option.Key}={FilterGraphEscaper.EscapeValue(option.Value)}"));
    }

    /// <summary>
    ///     The filter name, or an empty string when the whole filter text is carried by
    ///     <see cref="Value" />.
    /// </summary>
    public string Key => _key;

    /// <summary>
    ///     The escaped filter options, or the whole filter text when <see cref="Key" /> is empty.
    /// </summary>
    public string Value => _value;
}
