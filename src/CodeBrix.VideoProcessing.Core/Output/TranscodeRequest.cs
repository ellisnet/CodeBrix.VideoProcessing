using System;

namespace CodeBrix.VideoProcessing.Core.Output;

/// <summary>A whole-file re-encode, for <see cref="IVideoProcessor.TranscodeFile" />.</summary>
public sealed class TranscodeRequest
{
    /// <summary>The file to read.</summary>
    public string InputPath { get; set; }

    /// <summary>
    /// The output settings. "Keep the source's value" is allowed here: Width and Height both 0 keep the size, an
    /// unspecified FrameRate keeps the rate, AudioSampleRate / AudioChannels 0 keep the audio format, and
    /// <see cref="StandardVideoRequest.HasAudio" /> says whether the source's sound is kept (re-encoded to AAC).
    /// <see cref="StandardVideoRequest.InputPixelLayout" /> is not used. The pictures keep their stored
    /// orientation and the source's rotation metadata is carried over; a non-zero
    /// <see cref="StandardVideoRequest.RotationDegrees" /> replaces it.
    /// </summary>
    public StandardVideoRequest Output { get; set; } = new StandardVideoRequest { HasAudio = true, AudioSampleRate = 0, AudioChannels = 0 };

    /// <summary>Checks the request.</summary>
    /// <exception cref="ArgumentException">A value is missing or out of range.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InputPath)) throw new ArgumentException("The input path is required.", nameof(InputPath));
        if (Output == null) throw new ArgumentNullException(nameof(Output));
        Output.ValidateCore(allowKeep: true);
    }
}
