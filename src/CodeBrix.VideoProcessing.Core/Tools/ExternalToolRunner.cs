using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Core.Tools;

/// <summary>
/// THE one process runner behind every engine's <see cref="IExternalTools.Run" />: engines locate the tool, then
/// delegate here, so no engine writes its own process code and every engine runs tools the same way.
/// </summary>
/// <remarks>
/// <para>
/// The process is started from the RENDERED argument string (<see cref="ExternalToolArguments.Render" /> as
/// <c>ProcessStartInfo.Arguments</c>), so what <see cref="ExternalToolResult.RenderedCommand" /> records is what ran.
/// Standard output and standard error are both redirected and drained concurrently, line by line (UTF-8), so a tool
/// that writes a great deal to one of them (ffmpeg's progress on standard error) never stalls; each line goes to the
/// command's callback as it arrives, or is captured into the result when there is no callback. Standard input is
/// redirected and closed at once, so a tool that reads it sees end-of-input instead of waiting.
/// </para>
/// <para>
/// A non-zero exit code is a RESULT, never an exception. Cancellation kills the process and all of its children
/// (after <see cref="ExternalToolCommand.KillGracePeriod" />, immediately by default), waits for it to end, then throws
/// <see cref="OperationCanceledException" />. A tool that cannot be started throws
/// <see cref="VideoProcessingException" /> naming the tool and the path tried.
/// </para>
/// <para>THREADING: no shared state; any number of runs may go at once from any threads.</para>
/// </remarks>
public static class ExternalToolRunner
{
    private static readonly System.Text.Encoding Utf8 = new System.Text.UTF8Encoding(false);

    /// <summary>Runs a tool at a known path to the end.</summary>
    /// <param name="toolPath">The executable (normally <see cref="ExternalTool.Path" /> from a lookup).</param>
    /// <param name="command">What to run; its <see cref="ExternalToolCommand.ToolName" /> is used in messages.</param>
    /// <param name="cancellationToken">Cancels the run: the process (and its children) is killed.</param>
    /// <returns>The exit code and the captured output.</returns>
    /// <exception cref="ArgumentException"><paramref name="toolPath" /> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="command" /> is null.</exception>
    /// <exception cref="VideoProcessingException">The tool could not be started.</exception>
    /// <exception cref="OperationCanceledException">The run was cancelled; the process has been killed.</exception>
    public static ExternalToolResult Run(string toolPath, ExternalToolCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(toolPath)) throw new ArgumentException("The tool path is required.", nameof(toolPath));
        if (command == null) throw new ArgumentNullException(nameof(command));
        cancellationToken.ThrowIfCancellationRequested();

        string renderedCommand = RenderCommand(toolPath, command.Arguments);
        var startInfo = new ProcessStartInfo
        {
            FileName = toolPath,
            Arguments = command.Arguments.Render(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
            WorkingDirectory = command.WorkingDirectory ?? string.Empty
        };

        using var process = new Process { StartInfo = startInfo };
        var run = new RunState(process, command);
        process.OutputDataReceived += run.OnStandardOutput;
        process.ErrorDataReceived += run.OnStandardError;

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new VideoProcessingException(
                $"The external tool '{command.ToolName}' could not be started from '{toolPath}': {exception.Message}", exception);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            process.StandardInput.Close();
        }
        catch (IOException)
        {
        }

        using (cancellationToken.Register(() => run.Cancel(command.KillGracePeriod)))
        {
            // The no-timeout wait also waits for both redirected streams to reach their end, so every line has been
            // delivered when it returns.
            process.WaitForExit();
        }

        if (run.CallbackFailure != null) ExceptionDispatchInfo.Capture(run.CallbackFailure).Throw();
        if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);

        return new ExternalToolResult(process.ExitCode, renderedCommand, run.CapturedOutput(), run.CapturedError());
    }

    /// <summary>
    /// The command line as recorded in <see cref="ExternalToolResult.RenderedCommand" />: the tool path (in double
    /// quotes only when it contains whitespace), a space, and the rendered arguments - or just the path when there
    /// are none.
    /// </summary>
    /// <param name="toolPath">The executable.</param>
    /// <param name="arguments">The arguments; null means none.</param>
    /// <returns>The command line.</returns>
    public static string RenderCommand(string toolPath, ExternalToolArguments arguments)
    {
        string path = toolPath ?? string.Empty;
        if (ContainsWhitespace(path)) path = "\"" + path + "\"";

        string rendered = arguments == null ? string.Empty : arguments.Render();
        return rendered.Length == 0 ? path : path + " " + rendered;
    }

    private static bool ContainsWhitespace(string text)
    {
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c)) return true;
        }

        return false;
    }

    // The per-run state the process events write into; one instance per run, so runs share nothing.
    private sealed class RunState
    {
        private readonly Process process;
        private readonly ExternalToolCommand command;
        private readonly List<string> output = new List<string>();
        private readonly List<string> error = new List<string>();
        private readonly object gate = new object();
        private Exception callbackFailure;

        internal RunState(Process process, ExternalToolCommand command)
        {
            this.process = process;
            this.command = command;
        }

        internal Exception CallbackFailure
        {
            get
            {
                lock (gate) return callbackFailure;
            }
        }

        internal void OnStandardOutput(object sender, DataReceivedEventArgs e) => Deliver(e.Data, command.OnStandardOutputLine, output);

        internal void OnStandardError(object sender, DataReceivedEventArgs e) => Deliver(e.Data, command.OnStandardErrorLine, error);

        internal string CapturedOutput()
        {
            lock (gate) return string.Join("\n", output);
        }

        internal string CapturedError()
        {
            lock (gate) return string.Join("\n", error);
        }

        internal void Cancel(TimeSpan gracePeriod)
        {
            if (gracePeriod <= TimeSpan.Zero)
            {
                KillQuietly();
                return;
            }

            // Never block the token's callback: the kill happens later, and only if the process is still running.
            Task.Delay(gracePeriod).ContinueWith(_ => KillQuietly(), TaskScheduler.Default);
        }

        private void Deliver(string line, Action<string> callback, List<string> captured)
        {
            if (line == null) return; // end of the stream

            if (callback == null)
            {
                lock (gate) captured.Add(line);
                return;
            }

            lock (gate)
            {
                if (callbackFailure != null) return;
            }

            try
            {
                callback(line);
            }
            catch (Exception exception)
            {
                lock (gate) callbackFailure ??= exception;
                KillQuietly();
            }
        }

        private void KillQuietly()
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already ended, or the run is over and the process object released.
            }
            catch (Win32Exception)
            {
            }
            catch (NotSupportedException)
            {
            }
        }
    }
}
