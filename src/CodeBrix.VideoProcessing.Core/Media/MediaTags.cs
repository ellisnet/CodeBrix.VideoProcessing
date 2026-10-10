using System;
using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Core.Media;

internal static class MediaTags
{
    internal static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // Copies into a case-insensitive dictionary, so "Title" and "title" find the same tag whatever the engine
    // reported; a null source becomes the shared empty instance.
    internal static IReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string> source)
    {
        if (source == null || source.Count == 0) return Empty;

        Dictionary<string, string> copy = new Dictionary<string, string>(source.Count, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> pair in source)
        {
            if (pair.Key != null) copy[pair.Key] = pair.Value;
        }

        return copy;
    }
}
