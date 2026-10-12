using System;
using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Core.Tools;

/// <summary>
/// The arguments of one external tool run, rendered to ONE command-line string with fixed, documented quoting rules.
/// </summary>
/// <remarks>
/// <para>
/// THE QUOTING CONTRACT (pinned by tests; it is the rendering the ffmpeg package's ported argument builder has always
/// produced, so command lines recorded by consumers stay byte-identical):
/// </para>
/// <list type="number">
/// <item><description><see cref="Add" /> appends the token VERBATIM - no quotes are added, nothing is escaped. A
/// token may hold several words ("-map 0:v:0") or carry its own quotes
/// (<c>-vf "scale=w=64:h=48, format=yuv420p"</c>, <c>title="A clip"</c>); the caller owns them.</description></item>
/// <item><description>A null or empty token is IGNORED: it adds nothing, not even a separator.</description></item>
/// <item><description><see cref="AddPath" /> appends the path wrapped in double quotes - <c>"/a b/c.mp4"</c> -
/// exactly as given: it is not made absolute, and nothing inside it is escaped. A path that contains a double quote,
/// or that ends in a backslash, cannot be rendered into a command line the runner starts correctly, so it is REFUSED
/// with <see cref="ArgumentException" />; a null path throws <see cref="ArgumentNullException" /> and an empty one
/// <see cref="ArgumentException" />.</description></item>
/// <item><description><see cref="Render" /> joins the tokens in the order they were added with ONE space between
/// each; there is no leading or trailing space, and no arguments render as the empty string.</description></item>
/// </list>
/// <para>
/// The rendered string is what <see cref="ExternalToolRunner" /> hands to the operating system
/// (<c>ProcessStartInfo.Arguments</c>), so what a consumer records is what ran. .NET splits that string into the
/// process's arguments by the usual rules: whitespace separates arguments, double quotes group, <c>\"</c> is a literal
/// quote.
/// </para>
/// </remarks>
public sealed class ExternalToolArguments
{
    private readonly List<string> tokens = new List<string>();

    /// <summary>Creates an empty argument list.</summary>
    public ExternalToolArguments()
    {
    }

    /// <summary>Creates an argument list holding the given tokens, each added with <see cref="Add" />.</summary>
    /// <param name="tokens">The tokens; null means none.</param>
    public ExternalToolArguments(IEnumerable<string> tokens)
    {
        AddRange(tokens);
    }

    /// <summary>The number of rendered pieces held (an ignored empty token is not counted).</summary>
    public int Count => tokens.Count;

    /// <summary>The pieces in order, as they will be rendered (a path already in its double quotes).</summary>
    public IReadOnlyList<string> Tokens => tokens;

    /// <summary>Appends a token verbatim. A null or empty token is ignored.</summary>
    /// <param name="token">The token, with any quotes it needs already in it.</param>
    /// <returns>This list, for chaining.</returns>
    public ExternalToolArguments Add(string token)
    {
        if (!string.IsNullOrEmpty(token)) tokens.Add(token);
        return this;
    }

    /// <summary>Appends every token with <see cref="Add" />. A null sequence adds nothing.</summary>
    /// <param name="tokens">The tokens.</param>
    /// <returns>This list, for chaining.</returns>
    public ExternalToolArguments AddRange(IEnumerable<string> tokens)
    {
        if (tokens == null) return this;
        foreach (string token in tokens) Add(token);
        return this;
    }

    /// <summary>Appends a file or folder path wrapped in double quotes, exactly as given.</summary>
    /// <param name="path">The path.</param>
    /// <returns>This list, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path" /> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="path" /> is empty, contains a double quote, or ends in a backslash - none of which can be rendered
    /// into a command line the runner starts correctly (the quote would end the quoted path early; a trailing backslash
    /// would escape the closing quote).
    /// </exception>
    public ExternalToolArguments AddPath(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (path.Length == 0) throw new ArgumentException("The path is empty.", nameof(path));
        if (path.IndexOf('"') >= 0)
        {
            throw new ArgumentException(
                $"The path '{path}' contains a double quote, which cannot be rendered into a command line the runner can start correctly.",
                nameof(path));
        }

        if (path[path.Length - 1] == '\\')
        {
            throw new ArgumentException(
                $"The path '{path}' ends in a backslash, which cannot be rendered into a command line the runner can start correctly " +
                "(it would escape the closing quote). Remove the trailing backslash.",
                nameof(path));
        }

        tokens.Add("\"" + path + "\"");
        return this;
    }

    /// <summary>The one command-line string: the tokens in order, separated by single spaces.</summary>
    /// <returns>The rendered arguments; empty when there are none.</returns>
    public string Render() => string.Join(" ", tokens);

    /// <summary>The same as <see cref="Render" />.</summary>
    /// <returns>The rendered arguments.</returns>
    public override string ToString() => Render();
}
