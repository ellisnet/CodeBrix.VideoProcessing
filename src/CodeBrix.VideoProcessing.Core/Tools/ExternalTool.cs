using System;

namespace CodeBrix.VideoProcessing.Core.Tools;

/// <summary>The outcome of looking for an external tool: where it is, or that it is not there and where it was looked for.</summary>
public sealed class ExternalTool
{
    /// <summary>Creates the outcome of a lookup.</summary>
    /// <param name="name">The tool's name as it was asked for (for example "ffmpeg").</param>
    /// <param name="path">The full path of the executable; null when it was not found.</param>
    /// <param name="lookedIn">Where it was looked for, as text for a message (for example "on the PATH").</param>
    /// <exception cref="ArgumentException"><paramref name="name" /> is null or empty.</exception>
    public ExternalTool(string name, string path, string lookedIn)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("The tool name is required.", nameof(name));

        Name = name;
        Path = string.IsNullOrEmpty(path) ? null : path;
        LookedIn = lookedIn ?? string.Empty;
    }

    /// <summary>The tool's name as it was asked for.</summary>
    public string Name { get; }

    /// <summary>True when the tool was found.</summary>
    public bool Found => Path != null;

    /// <summary>The full path of the executable; null when it was not found.</summary>
    public string Path { get; }

    /// <summary>
    /// Where the tool was looked for, as human text that completes "was looked for ..." - for example
    /// "in '/opt/ffmpeg/bin' and then on the PATH", or "on the PATH".
    /// </summary>
    public string LookedIn { get; }

    /// <summary>The path when found, else "&lt;name&gt; (not found, looked for &lt;LookedIn&gt;)".</summary>
    /// <returns>A description for logs.</returns>
    public override string ToString() => Found ? Path : $"{Name} (not found, looked for {LookedIn})";
}
