using System;
using System.Diagnostics;
using System.IO;

namespace CodeBrix.VideoProcessing.Android.Tests;

/// <summary>
/// The host ffmpeg / ffprobe, run directly (never through code under test), for making MP4 fixtures and inspecting
/// results; plus a per-test scratch folder. Tests that need ffmpeg skip themselves when it is not on PATH.
/// </summary>
internal static class HostTools
{
    private static readonly Lazy<bool> Present = new Lazy<bool>(() =>
    {
        try
        {
            Run("ffmpeg", "-hide_banner -version");
            Run("ffprobe", "-hide_banner -version");
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    });

    /// <summary>True when ffmpeg and ffprobe can be run.</summary>
    public static bool FFmpegAvailable => Present.Value;

    /// <summary>The bundled synthetic clip (see tests/Assets/PROVENANCE.txt).</summary>
    public static string Clip => Path.Combine(AppContext.BaseDirectory, "Assets", "clip-h264-aac.mp4");

    /// <summary>A new empty folder under the system temporary folder.</summary>
    public static string NewFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "cbvp-android-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Runs ffmpeg (quiet, overwriting).</summary>
    public static void FFmpeg(string arguments) => Run("ffmpeg", "-hide_banner -v error -y " + arguments);

    /// <summary>Runs ffprobe and returns its standard output.</summary>
    public static string FFprobe(string arguments) => Run("ffprobe", "-v error " + arguments);

    /// <summary>The per-frame MD5 lines of a file's first video stream (ffmpeg's framemd5 muxer), hashes only.</summary>
    public static string VideoFrameHashes(string path)
    {
        string text = Run("ffmpeg", $"-hide_banner -v error -i \"{path}\" -map 0:v:0 -f framemd5 -");
        var hashes = new System.Text.StringBuilder();
        foreach (string line in text.Split('\n'))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            string[] fields = line.Split(',');
            hashes.AppendLine(fields[^1].Trim());
        }

        return hashes.ToString();
    }

    private static string Run(string tool, string arguments)
    {
        var startInfo = new ProcessStartInfo(tool, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException(tool + " did not start.");
        System.Threading.Tasks.Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"{tool} {arguments} failed ({process.ExitCode}): {error.Result}");
        return output;
    }
}
