using System;
using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Core.Output;

/// <summary>Join files end to end, for <see cref="IVideoProcessor.ConcatenateFiles" />.</summary>
/// <remarks>
/// The streams are copied, not re-encoded, so every input must have the same streams with the same codecs and
/// parameters (size, frame rate, pixel format, sample rate, channels) - for example clips written by the same
/// <see cref="IStandardVideoWriter" /> settings.
/// </remarks>
public sealed class ConcatenateRequest
{
    /// <summary>The files to join, in order.</summary>
    public IList<string> InputPaths { get; set; } = new List<string>();

    /// <summary>The file to write. An existing file is overwritten.</summary>
    public string OutputPath { get; set; }

    /// <summary>Checks the request.</summary>
    /// <exception cref="ArgumentException">A value is missing.</exception>
    public void Validate()
    {
        if (InputPaths == null || InputPaths.Count == 0) throw new ArgumentException("At least one input path is required.", nameof(InputPaths));
        for (int i = 0; i < InputPaths.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(InputPaths[i])) throw new ArgumentException($"Input path {i} is empty.", nameof(InputPaths));
        }

        if (string.IsNullOrWhiteSpace(OutputPath)) throw new ArgumentException("The output path is required.", nameof(OutputPath));
    }
}
