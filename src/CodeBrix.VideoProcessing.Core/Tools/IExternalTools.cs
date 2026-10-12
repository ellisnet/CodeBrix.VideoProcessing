using System.Threading;

namespace CodeBrix.VideoProcessing.Core.Tools;

/// <summary>
/// The external command-line tools an engine can locate and run, offered to consumers that compose their OWN command
/// lines - for example a desktop authoring library running ffmpeg passes through whichever engine is registered,
/// without referencing that engine's package.
/// </summary>
/// <remarks>
/// <para>
/// Obtain it with <see cref="VideoProcessorExtensions.TryGetExternalTools" /> (or
/// <see cref="VideoProcessorExtensions.RequireExternalTools" />). Implementations look in
/// <see cref="BinaryFolder" /> first and then on the PATH (see <see cref="ExternalToolLocator" />), and run through the
/// ONE shared runner, <see cref="ExternalToolRunner" />, so every engine runs tools the same way.
/// </para>
/// <para>THREADING: safe to use from several threads at once; each <see cref="Run" /> is its own process.</para>
/// </remarks>
public interface IExternalTools
{
    /// <summary>The folder tools are looked for in first; null or empty means the PATH only.</summary>
    string BinaryFolder { get; }

    /// <summary>Finds a tool, without running it.</summary>
    /// <param name="toolName">The tool's name without any extension (for example "ffmpeg").</param>
    /// <returns>Where the tool is, or that it was not found and where it was looked for.</returns>
    /// <exception cref="System.ArgumentException"><paramref name="toolName" /> is null or empty.</exception>
    ExternalTool Locate(string toolName);

    /// <summary>
    /// Locates <see cref="ExternalToolCommand.ToolName" /> and runs it to the end with the command's arguments.
    /// </summary>
    /// <param name="command">What to run.</param>
    /// <param name="cancellationToken">Cancels the run: the process (and its children) is killed.</param>
    /// <returns>The exit code and the output. A non-zero exit code is a result, never an exception.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="command" /> is null.</exception>
    /// <exception cref="VideoProcessingException">The tool was not found, or could not be started; the message names it and where it was looked for.</exception>
    /// <exception cref="System.OperationCanceledException">The run was cancelled; the process has been killed.</exception>
    ExternalToolResult Run(ExternalToolCommand command, CancellationToken cancellationToken = default);
}
