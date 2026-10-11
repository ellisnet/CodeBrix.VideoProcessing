using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Media;
using MediaExtractor = global::Android.Media.MediaExtractor;
using MediaMetadataRetriever = global::Android.Media.MediaMetadataRetriever;
using MetadataKey = global::Android.Media.MetadataKey;
using JavaThrowable = global::Java.Lang.Throwable;

namespace CodeBrix.VideoProcessing.Android.Engine;

/// <summary>One analysed file: the contract's description plus the per-track platform details the engine reuses.</summary>
internal sealed class AnalysedMedia
{
    /// <summary>The contract's description.</summary>
    public MediaInfo Info { get; init; }

    /// <summary>Every track, in MediaExtractor order.</summary>
    public IReadOnlyList<TrackDescription> Tracks { get; init; }

    /// <summary>The first video track, or null.</summary>
    public TrackDescription FirstVideo => Tracks.Count == 0 ? null : FindFirst(true);

    /// <summary>The first audio track, or null.</summary>
    public TrackDescription FirstAudio => Tracks.Count == 0 ? null : FindFirst(false);

    private TrackDescription FindFirst(bool video)
    {
        foreach (TrackDescription track in Tracks)
        {
            if (video ? track.IsVideo : track.IsAudio) return track;
        }

        return null;
    }
}

/// <summary>
/// Analyse on Android: MediaExtractor for the tracks (and the managed MP4 reader for exact video timing), and
/// MediaMetadataRetriever for the container MIME type, duration and tags.
/// </summary>
internal static class AndroidMediaAnalyser
{
    private static readonly (MetadataKey Key, string Name)[] TagKeys =
    {
        (MetadataKey.Title, "title"),
        (MetadataKey.Date, "date"),
        (MetadataKey.Artist, "artist"),
        (MetadataKey.Album, "album"),
        (MetadataKey.Albumartist, "album_artist"),
        (MetadataKey.Author, "author"),
        (MetadataKey.Composer, "composer"),
        (MetadataKey.Writer, "writer"),
        (MetadataKey.Genre, "genre"),
        (MetadataKey.Year, "year"),
        (MetadataKey.Location, "location"),
        (MetadataKey.CdTrackNumber, "track"),
        (MetadataKey.DiscNumber, "disc"),
        (MetadataKey.Compilation, "compilation"),
    };

    /// <summary>Analyses a file.</summary>
    internal static AnalysedMedia Analyse(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MediaExtractor extractor = AndroidMedia.OpenExtractor(path);
        List<TrackDescription> tracks;
        try
        {
            tracks = AndroidMedia.ReadTracks(extractor, path);
        }
        catch (JavaThrowable exception)
        {
            throw new MediaFormatException($"Android's MediaExtractor cannot read the tracks of '{path}': {AndroidMedia.Describe(exception)}", exception);
        }
        finally
        {
            AndroidMedia.ReleaseQuietly(extractor);
        }

        cancellationToken.ThrowIfCancellationRequested();

        string containerMime = null;
        long durationMilliseconds = 0;
        var values = new List<KeyValuePair<string, string>>();
        var retriever = new MediaMetadataRetriever();
        try
        {
            retriever.SetDataSource(path);
            containerMime = retriever.ExtractMetadata(MetadataKey.Mimetype);
            long.TryParse(retriever.ExtractMetadata(MetadataKey.Duration), NumberStyles.Integer, CultureInfo.InvariantCulture, out durationMilliseconds);
            foreach ((MetadataKey key, string name) in TagKeys)
            {
                values.Add(new KeyValuePair<string, string>(name, retriever.ExtractMetadata(key)));
            }
        }
        catch (JavaThrowable)
        {
            // The retriever is only the source of container-level extras; MediaExtractor already read the file.
        }
        finally
        {
            try { retriever.Release(); } catch (JavaThrowable) { }
        }

        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, string> tags = MediaFormatMapping.BuildTags(values);
        return new AnalysedMedia
        {
            Info = MediaFormatMapping.BuildMediaInfo(containerMime, durationMilliseconds, tracks, tags),
            Tracks = tracks
        };
    }
}
