using System;

namespace CodeBrix.VideoProcessing.Core.Output;

/// <summary>Cut a time range out of a file, for <see cref="IVideoProcessor.TrimFile" />.</summary>
public sealed class TrimRequest
{
    /// <summary>The file to read.</summary>
    public string InputPath { get; set; }

    /// <summary>The file to write. An existing file is overwritten.</summary>
    public string OutputPath { get; set; }

    /// <summary>The start of the range.</summary>
    public TimeSpan From { get; set; }

    /// <summary>The end of the range (exclusive); must be after <see cref="From" />.</summary>
    public TimeSpan To { get; set; }

    /// <summary>
    /// True: copy the coded streams without re-encoding - fast and lossless, but the cut starts at the keyframe at or
    /// before <see cref="From" />, so the output may begin slightly early. False (the default): re-encode for a
    /// frame-accurate cut, to the engine's standard profile (H.264 + AAC at the engine's default quality).
    /// </summary>
    public bool SnapToKeyframes { get; set; }

    /// <summary>Checks the request.</summary>
    /// <exception cref="ArgumentException">A value is missing or out of range.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InputPath)) throw new ArgumentException("The input path is required.", nameof(InputPath));
        if (string.IsNullOrWhiteSpace(OutputPath)) throw new ArgumentException("The output path is required.", nameof(OutputPath));
        if (From < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(From), From, "The start cannot be negative.");
        if (To <= From) throw new ArgumentOutOfRangeException(nameof(To), To, "The end must be after the start.");
    }
}
