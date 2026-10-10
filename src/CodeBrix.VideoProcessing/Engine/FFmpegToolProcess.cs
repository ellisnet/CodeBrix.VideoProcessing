using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Core;

namespace CodeBrix.VideoProcessing.Engine;

/// <summary>
/// One ffmpeg / ffprobe child process whose BINARY standard output is read directly.
/// </summary>
/// <remarks>
/// The engine's readers (frame source, sample source, snapshot) use this rather than an output named
/// pipe through <see cref="FFMpegArgumentProcessor" />: that processor disposes its output pipe the moment
/// ffmpeg exits, which can throw away the last frames still sitting unread in the pipe when the reader is
/// slower than ffmpeg. Reading stdout has no such race - the data stays readable after the process ends.
/// Arguments go through <see cref="ProcessStartInfo.ArgumentList" />, so paths need no quoting.
/// </remarks>
internal sealed class FFmpegToolProcess : IDisposable
{
    private const int StandardErrorLinesKept = 40;

    private readonly Process process;
    private readonly Queue<string> standardError = new Queue<string>();
    private readonly object errorGate = new object();
    private bool disposed;

    private FFmpegToolProcess(Process process)
    {
        this.process = process;
    }

    /// <summary>The binary standard output of the process.</summary>
    public Stream StandardOutput => process.StandardOutput.BaseStream;

    /// <summary>The exit code; only meaningful once <see cref="WaitForExit" /> has returned.</summary>
    public int ExitCode => process.ExitCode;

    /// <summary>The last lines the process wrote to standard error.</summary>
    public string ErrorText
    {
        get
        {
            lock (errorGate) return string.Join(Environment.NewLine, standardError);
        }
    }

    /// <summary>Starts a tool.</summary>
    /// <param name="fileName">The executable.</param>
    /// <param name="arguments">The arguments, one per item, unquoted.</param>
    /// <param name="workingDirectory">The working directory, or empty for the current one.</param>
    /// <returns>The running process.</returns>
    /// <exception cref="VideoProcessingException">The executable could not be started.</exception>
    public static FFmpegToolProcess Start(string fileName, IEnumerable<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = workingDirectory ?? string.Empty
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo };
        var tool = new FFmpegToolProcess(process);
        process.ErrorDataReceived += tool.OnErrorData;

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            process.Dispose();
            throw new VideoProcessingException(
                $"Could not start '{fileName}'. The ffmpeg engine needs the ffmpeg and ffprobe executables on the PATH, " +
                "or in the folder set by GlobalFFOptions / FFOptions.BinaryFolder.",
                exception);
        }

        process.BeginErrorReadLine();
        // Nothing is ever written to the tool's stdin; closing it means a tool that tries to read it sees EOF
        // instead of hanging.
        try
        {
            process.StandardInput.Close();
        }
        catch (IOException)
        {
        }

        return tool;
    }

    /// <summary>Reads until <paramref name="buffer" /> is full or the output ends.</summary>
    /// <param name="buffer">The destination.</param>
    /// <returns>The number of bytes read; less than the buffer length only at the end of the output.</returns>
    public int ReadFully(Span<byte> buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = StandardOutput.Read(buffer.Slice(total));
            if (read == 0) break;
            total += read;
        }

        return total;
    }

    /// <summary>Waits for the process to end (and for its standard error to be fully read).</summary>
    public void WaitForExit() => process.WaitForExit();

    /// <summary>Ends the process if it is still running. Never throws.</summary>
    public void Kill()
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }
    }

    /// <summary>Kills the process if needed and releases it.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        Kill();
        try
        {
            process.WaitForExit(5000);
        }
        catch (InvalidOperationException)
        {
        }

        process.Dispose();
    }

    private void OnErrorData(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data)) return;

        lock (errorGate)
        {
            standardError.Enqueue(e.Data);
            while (standardError.Count > StandardErrorLinesKept) standardError.Dequeue();
        }
    }
}
