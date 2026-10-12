using System;
using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Core.Media;

/// <summary>One subtitle (text) stream of a media file, as reported by <see cref="IVideoProcessor.Analyse" />.</summary>
/// <remarks>
/// Engine-neutral: the engine reports what it can see of the stream and leaves the rest at null. No engine decodes
/// subtitles through the contract; this only says that the stream is there and what it is.
/// </remarks>
public sealed class SubtitleStreamInfo
{
    private readonly IReadOnlyDictionary<string, string> tags = MediaTags.Empty;

    /// <summary>The engine's index of the stream within the file (counting every stream type).</summary>
    public int Index { get; init; }

    /// <summary>
    /// The codec, in the engine's words - ffmpeg's vocabulary where the engine can map it (for example "mov_text",
    /// "webvtt", "subrip", "ass"); may be null.
    /// </summary>
    public string CodecName { get; init; }

    /// <summary>The stream's language as the file declares it (for example "eng" or "en"); null when not declared.</summary>
    public string Language { get; init; }

    /// <summary>The stream's title (its "title" tag); null when it has none.</summary>
    public string Title { get; init; }

    /// <summary>The stream-level tags (language, title, handler_name, ...), case-insensitive.</summary>
    public IReadOnlyDictionary<string, string> Tags
    {
        get => tags;
        init => tags = MediaTags.Copy(value);
    }
}
