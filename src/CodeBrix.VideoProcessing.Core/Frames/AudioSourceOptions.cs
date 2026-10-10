using System;

namespace CodeBrix.VideoProcessing.Core.Frames;

/// <summary>What <see cref="IVideoProcessor.OpenAudio" /> should deliver.</summary>
public sealed class AudioSourceOptions
{
    /// <summary>The sample rate to deliver, resampling if needed; 0 (the default) keeps the source's.</summary>
    public int SampleRate { get; set; }

    /// <summary>The channel count to deliver, remixing if needed; 0 (the default) keeps the source's.</summary>
    public int Channels { get; set; }

    /// <summary>Where to start. Default zero.</summary>
    public TimeSpan StartTime { get; set; } = TimeSpan.Zero;

    /// <summary>Checks the options.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is out of range.</exception>
    public void Validate()
    {
        if (SampleRate != 0 && (SampleRate < 1000 || SampleRate > 768000))
            throw new ArgumentOutOfRangeException(nameof(SampleRate), SampleRate, "The sample rate must be 0 (keep the source's) or between 1000 and 768000.");
        if (Channels < 0 || Channels > 32)
            throw new ArgumentOutOfRangeException(nameof(Channels), Channels, "The channel count must be 0 (keep the source's) or between 1 and 32.");
        if (StartTime < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(StartTime), StartTime, "The start time cannot be negative.");
    }
}
