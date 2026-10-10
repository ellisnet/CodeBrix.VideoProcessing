using System;
using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Core.Media;

/// <summary>One audio stream of a media file, as reported by <see cref="IVideoProcessor.Analyse" />.</summary>
/// <remarks>Values the engine cannot determine are left at zero / null.</remarks>
public sealed class AudioStreamInfo
{
    private readonly IReadOnlyDictionary<string, string> tags = MediaTags.Empty;

    /// <summary>The engine's index of the stream within the file (counting every stream type).</summary>
    public int Index { get; init; }

    /// <summary>The codec, in the engine's words (for example "aac", "opus", "flac").</summary>
    public string CodecName { get; init; }

    /// <summary>Samples per second per channel; 0 when not known.</summary>
    public int SampleRate { get; init; }

    /// <summary>The number of channels; 0 when not known.</summary>
    public int Channels { get; init; }

    /// <summary>Bits per stored sample, for codecs that have one (PCM, FLAC); 0 when not known or not meaningful.</summary>
    public int BitDepth { get; init; }

    /// <summary>The duration of the stream; <see cref="TimeSpan.Zero" /> when not known.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>The stream-level tags (language, handler_name, ...), case-insensitive.</summary>
    public IReadOnlyDictionary<string, string> Tags
    {
        get => tags;
        init => tags = MediaTags.Copy(value);
    }
}
