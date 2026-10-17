using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Builders.MetaData;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Exceptions;

namespace CodeBrix.VideoProcessing.Engine;

/// <summary>Probes a file through <see cref="FFProbe" /> and maps the friendly model onto the Core contract's <see cref="MediaInfo" />.</summary>
/// <remarks>
/// There is ONE probe path in the package: this class reads the friendly <see cref="IMediaAnalysis" /> model
/// (<see cref="FFProbe.AnalyseAsync(string, FFOptions, CancellationToken, string)" />) and never parses ffprobe's
/// JSON itself. The exact values the contract needs come from the model's exact properties -
/// <see cref="VideoStream.RealFrameRate" />, <see cref="VideoStream.ExactFrameRate" />, <see cref="VideoStream.FrameCount" />,
/// <see cref="VideoStream.DisplayMatrixRotation" /> and the tick-precise durations.
/// </remarks>
internal static class FFmpegProbe
{
    internal static async Task<MediaInfo> AnalyseAsync(string path, FFOptions options, CancellationToken cancellationToken) =>
        FromAnalysis(await ProbeAsync(path, options, cancellationToken).ConfigureAwait(false));

    // The friendly model of the file, with ffprobe's failures mapped onto the contract's exceptions. For callers that
    // need more than MediaInfo carries (the frame source's IsMirrored).
    internal static async Task<IMediaAnalysis> ProbeAsync(string path, FFOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("The path is required.", nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException("The media file does not exist.", path);

        IMediaAnalysis analysis;
        try
        {
            analysis = await FFProbe.AnalyseAsync(path, options, cancellationToken).ConfigureAwait(false);
        }
        catch (FormatNullException exception)
        {
            throw new MediaFormatException($"ffprobe's report on '{path}' has no format.", exception);
        }
        catch (FFProbeException exception)
        {
            throw new VideoProcessingException($"ffprobe could not be run: {exception.Message}", exception);
        }
        catch (Win32Exception exception)
        {
            throw new VideoProcessingException($"ffprobe could not be started: {exception.Message}", exception);
        }
        catch (FFMpegException exception)
        {
            throw new MediaFormatException($"ffprobe could not read '{path}': {exception.FFMpegErrorOutput}", exception);
        }
        catch (JsonException exception)
        {
            throw new MediaFormatException($"ffprobe's report on '{path}' could not be read.", exception);
        }

        return analysis;
    }

    // True when the video stream with this index has a mirroring display matrix (VideoSourceInfo.IsMirrored).
    internal static bool IsMirrored(IMediaAnalysis analysis, int streamIndex)
    {
        VideoStream stream = analysis?.VideoStreams?.Find(candidate => candidate.Index == streamIndex);
        return stream != null && stream.IsDisplayMatrixMirrored;
    }

    // Kept for unit tests over hand-written reports: it goes through the same friendly model.
    internal static MediaInfo Parse(string json) => FromAnalysis(FFProbe.ParseJson(json));

    internal static MediaInfo FromAnalysis(IMediaAnalysis analysis)
    {
        TimeSpan containerDuration = Positive(analysis.Format.Duration);

        return new MediaInfo
        {
            ContainerFormat = analysis.Format.FormatName,
            Duration = containerDuration,
            VideoStreams = analysis.VideoStreams
                .Where(stream => !IsAttachedPicture(stream))
                .Select(stream => MapVideo(stream, containerDuration))
                .ToList(),
            AudioStreams = analysis.AudioStreams.Select(stream => MapAudio(stream, containerDuration)).ToList(),
            SubtitleStreams = (analysis.SubtitleStreams ?? new List<SubtitleStream>()).Select(MapSubtitle).ToList(),
            Chapters = MapChapters(analysis.Chapters),
            Tags = analysis.Format.Tags
        };
    }

    // The friendly model's language is the stream's "language" tag; "und" (undetermined) is no language at all.
    private static SubtitleStreamInfo MapSubtitle(SubtitleStream stream)
    {
        Dictionary<string, string> tags = stream.Tags ?? new Dictionary<string, string>();
        tags.TryGetValue("title", out string title);
        string language = string.IsNullOrEmpty(stream.Language) || stream.Language == "und" ? null : stream.Language;

        return new SubtitleStreamInfo
        {
            Index = stream.Index,
            CodecName = stream.CodecName,
            Language = language,
            Title = string.IsNullOrEmpty(title) ? null : title,
            Tags = tags
        };
    }

    // The friendly model keeps a chapter's title (with the port's "TitleValueNotSet" placeholder when the chapter has
    // none), start and end, in file order; it does not keep the chapter's other tags, so Tags carries the title only.
    private static List<ChapterInfo> MapChapters(List<ChapterData> chapters)
    {
        var mapped = new List<ChapterInfo>();
        if (chapters == null) return mapped;

        for (int i = 0; i < chapters.Count; i++)
        {
            ChapterData chapter = chapters[i];
            string title = chapter.Title == UntitledChapter || string.IsNullOrEmpty(chapter.Title) ? null : chapter.Title;
            mapped.Add(new ChapterInfo
            {
                Index = i,
                Start = Positive(chapter.Start),
                End = Positive(chapter.End),
                Title = title,
                Tags = title == null ? null : new Dictionary<string, string> { ["title"] = title }
            });
        }

        return mapped;
    }

    // What the ported MediaAnalysis puts in ChapterData.Title when a chapter has no title tag.
    private const string UntitledChapter = "TitleValueNotSet";

    // ffprobe's choice of frame rate: r_frame_rate (the "real" base rate) unless it is missing or absurd - some
    // containers report 90000/1 or 1000/1 - in which case avg_frame_rate.
    internal static FrameRate ChooseFrameRate(string realRate, string averageRate)
    {
        FrameRate.TryParse(realRate, out FrameRate real);
        return ChooseFrameRate(real, FrameRate.TryParse(averageRate, out FrameRate average) ? average : real);
    }

    // averageOrReal is the friendly model's ExactFrameRate: avg_frame_rate, falling back to r_frame_rate.
    private static FrameRate ChooseFrameRate(FrameRate real, FrameRate averageOrReal)
    {
        if (real.IsSpecified && real.FramesPerSecond <= 1000) return real;
        return averageOrReal;
    }

    // Clockwise display rotation: the display matrix's angle is counter-clockwise, the legacy "rotate" tag is
    // already clockwise.
    private static int ClockwiseRotation(VideoStream stream)
    {
        if (stream.DisplayMatrixRotation.HasValue) return FFmpegNames.NormaliseQuarterTurn(-stream.DisplayMatrixRotation.Value);

        if (stream.Tags != null && stream.Tags.TryGetValue("rotate", out string rotate) &&
            double.TryParse(rotate, NumberStyles.Float, CultureInfo.InvariantCulture, out double clockwise))
        {
            return FFmpegNames.NormaliseQuarterTurn(clockwise);
        }

        return 0;
    }

    private static VideoStreamInfo MapVideo(VideoStream stream, TimeSpan containerDuration)
    {
        string pixelFormat = stream.PixelFormat;
        Dictionary<string, string> tags = stream.Tags ?? new Dictionary<string, string>();
        int bitDepth = stream.BitsPerRawSample;
        if (bitDepth <= 0) bitDepth = FFmpegNames.BitDepthOf(pixelFormat);

        bool alpha = FFmpegNames.HasAlpha(pixelFormat) ||
                     (tags.TryGetValue("alpha_mode", out string alphaMode) && alphaMode == "1");

        return new VideoStreamInfo
        {
            Index = stream.Index,
            CodecName = stream.CodecName,
            Width = stream.Width,
            Height = stream.Height,
            FrameRate = ChooseFrameRate(stream.RealFrameRate, stream.ExactFrameRate),
            FrameCount = stream.FrameCount ?? 0,
            Duration = StreamDuration(stream, tags, containerDuration),
            BitDepth = bitDepth,
            PixelFormatName = pixelFormat,
            ChromaSubsampling = FFmpegNames.ToChromaSubsampling(pixelFormat),
            HasAlpha = alpha,
            RotationDegrees = ClockwiseRotation(stream),
            ColorRange = FFmpegNames.ToColorRange(stream.ColorRange, pixelFormat),
            ColorMatrix = FFmpegNames.ToColorMatrix(stream.ColorSpace),
            ColorPrimaries = FFmpegNames.ToColorPrimaries(stream.ColorPrimaries),
            ColorTransfer = FFmpegNames.ToColorTransfer(stream.ColorTransfer),
            Tags = tags
        };
    }

    private static AudioStreamInfo MapAudio(AudioStream stream, TimeSpan containerDuration)
    {
        Dictionary<string, string> tags = stream.Tags ?? new Dictionary<string, string>();

        return new AudioStreamInfo
        {
            Index = stream.Index,
            CodecName = stream.CodecName,
            SampleRate = stream.SampleRateHz,
            Channels = stream.Channels,
            BitDepth = stream.BitDepth ?? 0,
            Duration = StreamDuration(stream, tags, containerDuration),
            Tags = tags
        };
    }

    // Matroska / WebM keep a stream's duration in a DURATION tag ("00:00:03.000000000"), not in the stream.
    private static TimeSpan StreamDuration(MediaStream stream, Dictionary<string, string> tags, TimeSpan containerDuration)
    {
        TimeSpan duration = Positive(stream.Duration);
        if (duration > TimeSpan.Zero) return duration;

        if (tags.TryGetValue("DURATION", out string tagged) &&
            TimeSpan.TryParse(tagged.Length > 16 ? tagged.Substring(0, 16) : tagged, CultureInfo.InvariantCulture, out TimeSpan parsed))
        {
            return parsed;
        }

        return containerDuration;
    }

    private static TimeSpan Positive(TimeSpan duration) => duration > TimeSpan.Zero ? duration : TimeSpan.Zero;

    private static bool IsAttachedPicture(VideoStream stream) =>
        stream.Disposition != null &&
        stream.Disposition.TryGetValue("attached_pic", out bool attached) &&
        attached;
}
