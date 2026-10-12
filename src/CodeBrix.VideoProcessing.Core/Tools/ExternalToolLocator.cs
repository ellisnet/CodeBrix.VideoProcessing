using System;
using System.Collections.Generic;
using System.IO;

namespace CodeBrix.VideoProcessing.Core.Tools;

/// <summary>
/// Finds an external tool the way every engine that reports <see cref="VideoProcessorCapabilities.ExternalTools" />
/// does: in the engine's folder(s) first, then on the PATH.
/// </summary>
/// <remarks>
/// <para>
/// IN A FOLDER: the file <c>&lt;folder&gt;/&lt;name&gt;</c>, and on Windows then <c>&lt;folder&gt;/&lt;name&gt;.exe</c>; a file that
/// exists is found.
/// </para>
/// <para>
/// ON THE PATH: each entry of the PATH environment variable in order (empty entries skipped); on Windows the name
/// with each PATHEXT extension (".COM;.EXE;.BAT;.CMD" when PATHEXT is not set), plus the bare name when it already
/// has an extension; elsewhere the bare name, which must be a file with an execute permission bit set.
/// </para>
/// <para>
/// A name that contains a directory separator is a path: it is checked as given (and with ".exe" on Windows), and
/// the folders and the PATH are not searched.
/// </para>
/// </remarks>
public static class ExternalToolLocator
{
    private const string DefaultPathExtensions = ".COM;.EXE;.BAT;.CMD";

    /// <summary>Finds a tool in one folder, then on the PATH.</summary>
    /// <param name="toolName">The tool's name without any extension (for example "ffmpeg").</param>
    /// <param name="binaryFolder">The folder to look in first; null or empty means the PATH only.</param>
    /// <returns>Where the tool is, or that it was not found and where it was looked for.</returns>
    /// <exception cref="ArgumentException"><paramref name="toolName" /> is null or empty.</exception>
    public static ExternalTool Locate(string toolName, string binaryFolder) =>
        Locate(toolName, string.IsNullOrEmpty(binaryFolder) ? Array.Empty<string>() : new[] { binaryFolder });

    /// <summary>Finds a tool in several folders, in order, then on the PATH.</summary>
    /// <param name="toolName">The tool's name without any extension (for example "ffmpeg").</param>
    /// <param name="folders">The folders to look in first, in order; null or empty entries are skipped.</param>
    /// <returns>Where the tool is, or that it was not found and where it was looked for.</returns>
    /// <exception cref="ArgumentException"><paramref name="toolName" /> is null or empty.</exception>
    public static ExternalTool Locate(string toolName, IEnumerable<string> folders) =>
        Locate(toolName, folders, Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows());

    /// <summary>
    /// Describes where a lookup looks, as text that completes "was looked for ...": "on the PATH",
    /// "in '&lt;a&gt;' and then on the PATH", or "in '&lt;a&gt;', then in '&lt;b&gt;', and then on the PATH".
    /// </summary>
    /// <param name="folders">The folders searched before the PATH; null or empty entries are skipped.</param>
    /// <returns>The description.</returns>
    public static string DescribeLookup(IEnumerable<string> folders)
    {
        List<string> used = UsedFolders(folders);
        if (used.Count == 0) return "on the PATH";
        if (used.Count == 1) return $"in '{used[0]}' and then on the PATH";

        var text = new System.Text.StringBuilder();
        for (int i = 0; i < used.Count; i++)
        {
            text.Append(i == 0 ? "in '" : "then in '").Append(used[i]).Append("', ");
        }

        return text.Append("and then on the PATH").ToString();
    }

    // The testable core: the PATH value and the platform are parameters, so tests never touch the process's PATH.
    internal static ExternalTool Locate(string toolName, IEnumerable<string> folders, string pathVariable, bool windows)
    {
        if (string.IsNullOrEmpty(toolName)) throw new ArgumentException("The tool name is required.", nameof(toolName));

        if (toolName.IndexOf('/') >= 0 || (windows && toolName.IndexOf('\\') >= 0))
        {
            string direct = FirstExisting(new[] { toolName, windows ? toolName + ".exe" : null });
            return new ExternalTool(toolName, direct, $"at '{toolName}'");
        }

        List<string> used = UsedFolders(folders);
        string lookedIn = DescribeLookup(used);

        foreach (string folder in used)
        {
            string found = FirstExisting(new[]
            {
                Path.Combine(folder, toolName),
                windows ? Path.Combine(folder, toolName + ".exe") : null
            });
            if (found != null) return new ExternalTool(toolName, found, lookedIn);
        }

        if (!string.IsNullOrEmpty(pathVariable))
        {
            char separator = windows ? ';' : Path.PathSeparator;
            List<string> names = PathCandidateNames(toolName, windows);
            foreach (string rawEntry in pathVariable.Split(separator))
            {
                string entry = rawEntry.Trim().Trim('"');
                if (entry.Length == 0) continue;

                foreach (string name in names)
                {
                    string candidate = Path.Combine(entry, name);
                    if (IsRunnable(candidate, windows)) return new ExternalTool(toolName, Path.GetFullPath(candidate), lookedIn);
                }
            }
        }

        return new ExternalTool(toolName, null, lookedIn);
    }

    private static List<string> UsedFolders(IEnumerable<string> folders)
    {
        var used = new List<string>();
        if (folders == null) return used;

        foreach (string folder in folders)
        {
            if (!string.IsNullOrEmpty(folder)) used.Add(folder);
        }

        return used;
    }

    private static List<string> PathCandidateNames(string toolName, bool windows)
    {
        var names = new List<string>();
        if (!windows)
        {
            names.Add(toolName);
            return names;
        }

        if (Path.HasExtension(toolName)) names.Add(toolName);

        string extensions = Environment.GetEnvironmentVariable("PATHEXT");
        if (string.IsNullOrEmpty(extensions)) extensions = DefaultPathExtensions;
        foreach (string extension in extensions.Split(';'))
        {
            string trimmed = extension.Trim();
            if (trimmed.Length > 0) names.Add(toolName + trimmed);
        }

        return names;
    }

    private static string FirstExisting(string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (candidate != null && File.Exists(candidate)) return Path.GetFullPath(candidate);
        }

        return null;
    }

    private static bool IsRunnable(string candidate, bool windows)
    {
        if (!File.Exists(candidate)) return false;
        if (windows || OperatingSystem.IsWindows()) return true;

        try
        {
            const UnixFileMode anyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (File.GetUnixFileMode(candidate) & anyExecute) != 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
