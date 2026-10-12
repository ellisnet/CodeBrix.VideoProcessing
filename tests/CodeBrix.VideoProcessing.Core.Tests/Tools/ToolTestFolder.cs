using System;
using System.IO;

namespace CodeBrix.VideoProcessing.Core.Tests.Tools;

/// <summary>A fresh folder under the system temp folder for one test's scripts and files; deleted on dispose.</summary>
internal sealed class ToolTestFolder : IDisposable
{
    public ToolTestFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cbvp-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Writes a file (LF line endings), optionally with execute permission (ignored on Windows).</summary>
    public string Write(string relativePath, string content, bool executable = false)
    {
        string full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
        File.WriteAllText(full, content.Replace("\r\n", "\n"));
        if (!OperatingSystem.IsWindows())
        {
            UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            if (executable) mode |= UnixFileMode.UserExecute;
            File.SetUnixFileMode(full, mode);
        }

        return full;
    }

    /// <summary>Creates a sub-folder and returns its full path.</summary>
    public string Folder(string relativePath)
    {
        string full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(full);
        return full;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
