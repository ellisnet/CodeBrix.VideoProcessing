namespace CodeBrix.VideoProcessing.Core.Tools;

/// <summary>How one external tool run ended.</summary>
public sealed class ExternalToolResult
{
    /// <summary>Creates a result.</summary>
    /// <param name="exitCode">The process's exit code.</param>
    /// <param name="renderedCommand">The tool path and the rendered arguments, as run.</param>
    /// <param name="standardOutput">The captured standard output; null means empty.</param>
    /// <param name="standardError">The captured standard error; null means empty.</param>
    public ExternalToolResult(int exitCode, string renderedCommand, string standardOutput, string standardError)
    {
        ExitCode = exitCode;
        RenderedCommand = renderedCommand ?? string.Empty;
        StandardOutput = standardOutput ?? string.Empty;
        StandardError = standardError ?? string.Empty;
    }

    /// <summary>The process's exit code.</summary>
    public int ExitCode { get; }

    /// <summary>True when <see cref="ExitCode" /> is 0.</summary>
    public bool Succeeded => ExitCode == 0;

    /// <summary>
    /// The command as run, for messages and records: the tool's path (in double quotes only when it contains
    /// whitespace), a space, and <see cref="ExternalToolArguments.Render" /> - or just the path when there are no
    /// arguments.
    /// </summary>
    public string RenderedCommand { get; }

    /// <summary>
    /// Everything the tool wrote to standard output, lines joined with "\n" - captured ONLY when the command had no
    /// <see cref="ExternalToolCommand.OnStandardOutputLine" /> callback; otherwise empty.
    /// </summary>
    public string StandardOutput { get; }

    /// <summary>
    /// Everything the tool wrote to standard error, lines joined with "\n" - captured ONLY when the command had no
    /// <see cref="ExternalToolCommand.OnStandardErrorLine" /> callback; otherwise empty.
    /// </summary>
    public string StandardError { get; }
}
