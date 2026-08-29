using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments;

/// <summary>
///     Escapes free-text values so that they survive FFmpeg's filtergraph parser.
/// </summary>
/// <remarks>
///     <para>
///         FFmpeg unescapes a filter option value TWICE while parsing a filtergraph description:
///         once when the whole "name=options" filter description is tokenised (terminators
///         <c>[</c> <c>]</c> <c>,</c> <c>;</c>), and once more when that options string is split
///         into <c>key=value</c> pairs (terminators <c>:</c> and <c>=</c>). Both passes drop a
///         backslash in front of any character, so a value that must arrive intact has to be
///         escaped twice - which is what <see cref="EscapeValue" /> does.
///     </para>
///     <para>
///         Characters that are only meaningful to the first (chain) pass - <c>,</c> <c>;</c>
///         <c>[</c> <c>]</c> - need a single backslash instead, which is what
///         <see cref="EscapeChainSeparators" /> does; it leaves <c>:</c> and <c>=</c> alone so an
///         already-formed option string such as <c>text=hello:fontsize=24</c> keeps working.
///     </para>
///     <para>
///         Nothing here quotes for the operating-system command line; the argument builder wraps
///         the finished <c>-vf</c> / <c>-af</c> chain in double quotes, exactly as it does for the
///         other arguments.
///     </para>
/// </remarks>
internal static class FilterGraphEscaper
{
    /// <summary>
    ///     Characters that are special to at least one of FFmpeg's two filtergraph unescaping passes.
    /// </summary>
    private static readonly char[] _valueSpecialCharacters = { '\\', '\'', ':', ',', ';', '[', ']', '=' };

    /// <summary>
    ///     Characters that separate filters (and links) inside a filter chain.
    /// </summary>
    private static readonly char[] _chainSpecialCharacters = { ',', ';', '[', ']' };

    /// <summary>
    ///     Fully escapes a free-text filter option VALUE - a file path, a font name, a caption - so
    ///     that FFmpeg receives it character for character.
    /// </summary>
    /// <param name="value">The raw value. <see langword="null" /> is treated as an empty string.</param>
    /// <returns>The escaped value, safe to place after <c>key=</c> inside a filter chain.</returns>
    /// <remarks>
    ///     Escapes <c>\</c> <c>'</c> <c>:</c> <c>,</c> <c>;</c> <c>[</c> <c>]</c> <c>=</c>, twice
    ///     over, so a Windows path such as <c>C:\luts\warm.cube</c> and a POSIX path containing a
    ///     comma both survive. A double-quote character is NOT escaped: like every other path the
    ///     library passes to FFmpeg, a value containing <c>"</c> is not supported.
    /// </remarks>
    public static string EscapeValue(string value)
    {
        return Escape(Escape(value ?? string.Empty, _valueSpecialCharacters), _valueSpecialCharacters);
    }

    /// <summary>
    ///     Escapes only the characters that would end the current filter inside a chain, leaving an
    ///     already-formed FFmpeg option string (its <c>:</c> separators and <c>=</c> signs) untouched.
    /// </summary>
    /// <param name="text">The raw option string. <see langword="null" /> is treated as an empty string.</param>
    /// <returns>The escaped option string.</returns>
    /// <remarks>
    ///     Escapes <c>,</c> <c>;</c> <c>[</c> <c>]</c> once each. Backslashes and single quotes are
    ///     left as the caller wrote them, so an option string that already contains FFmpeg escapes
    ///     is not escaped a second time.
    /// </remarks>
    public static string EscapeChainSeparators(string text)
    {
        return Escape(text ?? string.Empty, _chainSpecialCharacters);
    }

    private static string Escape(string value, char[] specialCharacters)
    {
        if (value.Length == 0 || value.IndexOfAny(specialCharacters) < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 8);
        foreach (var character in value)
        {
            if (Array.IndexOf(specialCharacters, character) >= 0)
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
