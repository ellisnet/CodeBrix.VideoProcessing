using System;
using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Core.Media;

/// <summary>One chapter of a media file, as reported by <see cref="IVideoProcessor.Analyse" />.</summary>
/// <remarks>Engines that cannot read chapters report none; a chapter is never invented.</remarks>
public sealed class ChapterInfo
{
    private readonly IReadOnlyDictionary<string, string> tags = MediaTags.Empty;

    /// <summary>The chapter's position in the file's chapter list, counting from 0.</summary>
    public int Index { get; init; }

    /// <summary>Where the chapter starts, from the start of the file.</summary>
    public TimeSpan Start { get; init; }

    /// <summary>Where the chapter ends, from the start of the file.</summary>
    public TimeSpan End { get; init; }

    /// <summary>The chapter's title; null when it has none.</summary>
    public string Title { get; init; }

    /// <summary>The chapter-level tags the engine could read (at least "title" when there is one), case-insensitive.</summary>
    public IReadOnlyDictionary<string, string> Tags
    {
        get => tags;
        init => tags = MediaTags.Copy(value);
    }

    /// <summary>How long the chapter lasts: <see cref="End" /> minus <see cref="Start" />, never negative.</summary>
    public TimeSpan Duration => End > Start ? End - Start : TimeSpan.Zero;
}
