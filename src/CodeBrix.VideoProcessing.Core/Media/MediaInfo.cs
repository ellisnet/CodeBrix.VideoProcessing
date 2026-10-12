using System;
using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Core.Media;

/// <summary>The description of a media file returned by <see cref="IVideoProcessor.Analyse" />: its container, duration, video, audio and subtitle streams, chapters and tags.</summary>
/// <remarks>
/// Engines build it with an object initializer. Collection properties are never null: an engine that sets null
/// gets an empty collection, and tag lookups are case-insensitive.
/// </remarks>
public sealed class MediaInfo
{
    private readonly IReadOnlyList<VideoStreamInfo> videoStreams = Array.Empty<VideoStreamInfo>();
    private readonly IReadOnlyList<AudioStreamInfo> audioStreams = Array.Empty<AudioStreamInfo>();
    private readonly IReadOnlyList<SubtitleStreamInfo> subtitleStreams = Array.Empty<SubtitleStreamInfo>();
    private readonly IReadOnlyList<ChapterInfo> chapters = Array.Empty<ChapterInfo>();
    private readonly IReadOnlyDictionary<string, string> tags = MediaTags.Empty;

    /// <summary>The container format, in the engine's words (for example "mov,mp4,m4a,3gp,3g2,mj2" or "video/mp4"); may be null.</summary>
    public string ContainerFormat { get; init; }

    /// <summary>The duration of the whole file; <see cref="TimeSpan.Zero" /> when not known.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Every video stream, in file order.</summary>
    public IReadOnlyList<VideoStreamInfo> VideoStreams
    {
        get => videoStreams;
        init => videoStreams = value ?? Array.Empty<VideoStreamInfo>();
    }

    /// <summary>Every audio stream, in file order.</summary>
    public IReadOnlyList<AudioStreamInfo> AudioStreams
    {
        get => audioStreams;
        init => audioStreams = value ?? Array.Empty<AudioStreamInfo>();
    }

    /// <summary>Every subtitle (text) stream, in file order; empty when the engine found none or cannot see them.</summary>
    public IReadOnlyList<SubtitleStreamInfo> SubtitleStreams
    {
        get => subtitleStreams;
        init => subtitleStreams = value ?? Array.Empty<SubtitleStreamInfo>();
    }

    /// <summary>Every chapter, in file order; empty when the file has none or the engine cannot read them.</summary>
    public IReadOnlyList<ChapterInfo> Chapters
    {
        get => chapters;
        init => chapters = value ?? Array.Empty<ChapterInfo>();
    }

    /// <summary>The container-level tags (title, creation_time, encoder, ...), case-insensitive.</summary>
    public IReadOnlyDictionary<string, string> Tags
    {
        get => tags;
        init => tags = MediaTags.Copy(value);
    }

    /// <summary>The first video stream, or null when there is none.</summary>
    public VideoStreamInfo PrimaryVideo => videoStreams.Count > 0 ? videoStreams[0] : null;

    /// <summary>The first audio stream, or null when there is none.</summary>
    public AudioStreamInfo PrimaryAudio => audioStreams.Count > 0 ? audioStreams[0] : null;

    /// <summary>True when the file has at least one video stream.</summary>
    public bool HasVideo => videoStreams.Count > 0;

    /// <summary>True when the file has at least one audio stream.</summary>
    public bool HasAudio => audioStreams.Count > 0;

    /// <summary>True when the file has at least one subtitle stream.</summary>
    public bool HasSubtitles => subtitleStreams.Count > 0;

    /// <summary>True when the file has at least one chapter.</summary>
    public bool HasChapters => chapters.Count > 0;
}
