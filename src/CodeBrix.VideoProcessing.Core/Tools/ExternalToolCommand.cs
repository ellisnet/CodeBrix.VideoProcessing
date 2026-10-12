using System;

namespace CodeBrix.VideoProcessing.Core.Tools;

/// <summary>One run of an external tool: which tool, its arguments, and how to receive its output.</summary>
public sealed class ExternalToolCommand
{
    private readonly TimeSpan killGracePeriod = TimeSpan.Zero;

    /// <summary>Creates a command.</summary>
    /// <param name="toolName">The tool's name without any extension (for example "ffmpeg"), as <see cref="IExternalTools.Locate" /> takes it.</param>
    /// <param name="arguments">The arguments; null means none.</param>
    /// <exception cref="ArgumentException"><paramref name="toolName" /> is null or empty.</exception>
    public ExternalToolCommand(string toolName, ExternalToolArguments arguments = null)
    {
        if (string.IsNullOrEmpty(toolName)) throw new ArgumentException("The tool name is required.", nameof(toolName));

        ToolName = toolName;
        Arguments = arguments ?? new ExternalToolArguments();
    }

    /// <summary>The tool's name.</summary>
    public string ToolName { get; }

    /// <summary>The arguments; never null.</summary>
    public ExternalToolArguments Arguments { get; }

    /// <summary>The working directory of the process; null or empty means the current directory.</summary>
    public string WorkingDirectory { get; init; }

    /// <summary>
    /// Receives each line of standard output as it arrives, in order, on a background thread. When set, the output is
    /// NOT also captured into <see cref="ExternalToolResult.StandardOutput" />. Must not block for long; an exception
    /// it throws stops the run (the process is killed) and is rethrown by the run.
    /// </summary>
    public Action<string> OnStandardOutputLine { get; init; }

    /// <summary>
    /// Receives each line of standard error as it arrives, in order, on a background thread (ffmpeg writes its
    /// progress there). When set, the text is NOT also captured into <see cref="ExternalToolResult.StandardError" />.
    /// Same rules as <see cref="OnStandardOutputLine" />.
    /// </summary>
    public Action<string> OnStandardErrorLine { get; init; }

    /// <summary>
    /// How long a cancelled run waits for the process to end on its own before killing it (and its children).
    /// <see cref="TimeSpan.Zero" />, the default, kills it outright.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan KillGracePeriod
    {
        get => killGracePeriod;
        init
        {
            if (value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(KillGracePeriod), value, "The grace period cannot be negative.");
            killGracePeriod = value;
        }
    }
}
