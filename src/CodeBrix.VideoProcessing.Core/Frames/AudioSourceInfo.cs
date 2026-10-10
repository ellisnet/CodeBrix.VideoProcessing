using System;

namespace CodeBrix.VideoProcessing.Core.Frames;

/// <summary>What an <see cref="IAudioSampleSource" /> delivers.</summary>
public sealed class AudioSourceInfo
{
    /// <summary>Samples per second per channel of the delivered audio.</summary>
    public int SampleRate { get; init; }

    /// <summary>The channel count of the delivered audio.</summary>
    public int Channels { get; init; }

    /// <summary>The duration of the source stream; <see cref="TimeSpan.Zero" /> when not known.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>The source's codec, in the engine's words; may be null.</summary>
    public string SourceCodecName { get; init; }
}
