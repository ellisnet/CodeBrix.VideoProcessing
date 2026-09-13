using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CodeBrix.VideoProcessing.Tests.Utilities;

/// <summary>
///     Reads the installed ffmpeg's <c>-encoders</c> listing directly, without going through the library code under
///     test, so a test can decide whether it applies to this machine - for example skip itself where
///     <c>libsvtav1</c> is present.
/// </summary>
public static class FFMpegEncoderList
{
    private static readonly Lazy<HashSet<string>> Encoders = new(Read);

    /// <summary>Whether the ffmpeg the tests run has the named encoder.</summary>
    public static bool Has(string encoderName)
    {
        return Encoders.Value.Contains(encoderName);
    }

    private static HashSet<string> Read()
    {
        var startInfo = new ProcessStartInfo(GlobalFFOptions.GetFFMpegBinaryPath(), "-hide_banner -encoders")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        var standardError = process.StandardError.ReadToEndAsync();
        var standardOutput = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        standardError.GetAwaiter().GetResult();

        //Each encoder line is six capability flags, the encoder name, then its description:
        //    " V....D libaom-av1           libaom AV1 (codec av1)"
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in standardOutput.Split('\n'))
        {
            var tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2 && tokens[0].Length == 6 && "VASD".IndexOf(tokens[0][0]) >= 0)
            {
                names.Add(tokens[1]);
            }
        }

        return names;
    }
}
