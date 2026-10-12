using System;
using System.Collections.Generic;
using CodeBrix.VideoProcessing.Core.Media;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// Maps what Android reports about a media file (MediaExtractor track formats, MediaMetadataRetriever values, the
/// managed MP4 reader's sample-table timing) onto the Core contract's <see cref="MediaInfo" />, and the contract's
/// colour enums back onto MediaFormat constants for the encoder.
/// </summary>
/// <remarks>
/// Values the platform does not report are left at 0 / null / Unspecified, as the contract asks - never guessed.
/// Codec names use ffmpeg's vocabulary ("h264", "hevc", "aac", ...) so consumers see the same words from either
/// engine; a MIME type this table does not know is reported as the MIME type itself.
/// </remarks>
internal static class MediaFormatMapping
{
    private static readonly Dictionary<string, string> CodecNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["video/avc"] = "h264",
        ["video/hevc"] = "hevc",
        ["video/av01"] = "av1",
        ["video/x-vnd.on2.vp9"] = "vp9",
        ["video/x-vnd.on2.vp8"] = "vp8",
        ["video/mp4v-es"] = "mpeg4",
        ["video/3gpp"] = "h263",
        ["video/mpeg2"] = "mpeg2video",
        ["video/dolby-vision"] = "dvhe",
        ["audio/mp4a-latm"] = "aac",
        ["audio/opus"] = "opus",
        ["audio/vorbis"] = "vorbis",
        ["audio/flac"] = "flac",
        ["audio/mpeg"] = "mp3",
        ["audio/raw"] = "pcm",
        ["audio/3gpp"] = "amr_nb",
        ["audio/amr-wb"] = "amr_wb",
        ["audio/ac3"] = "ac3",
        ["audio/eac3"] = "eac3",
        ["audio/g711-alaw"] = "pcm_alaw",
        ["audio/g711-mlaw"] = "pcm_mulaw",
        ["text/3gpp-tt"] = "mov_text",
        ["text/vtt"] = "webvtt",
        ["application/x-subrip"] = "subrip",
        ["application/ttml+xml"] = "ttml",
        ["text/cea-608"] = "eia_608",
    };

    // Well-known rational rates a float KEY_FRAME_RATE is snapped to.
    private static readonly FrameRate[] NtscRates =
    {
        new FrameRate(24000, 1001),
        new FrameRate(30000, 1001),
        new FrameRate(48000, 1001),
        new FrameRate(60000, 1001),
        new FrameRate(120000, 1001),
    };

    /// <summary>The codec name for a MIME type, in ffmpeg's words; the MIME type itself when unknown; null for null.</summary>
    internal static string CodecNameFromMime(string mime)
    {
        if (string.IsNullOrEmpty(mime)) return null;
        return CodecNames.TryGetValue(mime, out string name) ? name : mime;
    }

    /// <summary>
    /// A frame rate from a floating-point frames-per-second value: snapped to an NTSC rate (n x 1000/1001) or a whole
    /// number when within 0.005 of one, else kept to a thousandth. Zero, negative or non-finite gives Unspecified.
    /// </summary>
    internal static FrameRate FrameRateFromValue(double framesPerSecond)
    {
        if (double.IsNaN(framesPerSecond) || double.IsInfinity(framesPerSecond) || framesPerSecond <= 0 || framesPerSecond > 1_000_000) return FrameRate.Unspecified;

        foreach (FrameRate rate in NtscRates)
        {
            if (Math.Abs(rate.FramesPerSecond - framesPerSecond) < 0.005) return rate;
        }

        double whole = Math.Round(framesPerSecond);
        if (whole >= 1 && Math.Abs(whole - framesPerSecond) < 0.005) return new FrameRate((int)whole, 1);

        int thousandths = (int)Math.Round(framesPerSecond * 1000);
        return thousandths > 0 ? new FrameRate(thousandths, 1000) : FrameRate.Unspecified;
    }

    /// <summary>The frame rate of a video track: the MP4 sample table's exact rate when known, else KEY_FRAME_RATE snapped.</summary>
    internal static FrameRate ChooseFrameRate(TrackDescription track) =>
        track.ContainerFrameRate.IsSpecified ? track.ContainerFrameRate : FrameRateFromValue(track.FrameRateValue);

    /// <summary>Normalises a rotation in degrees to 0, 90, 180 or 270 (the nearest quarter turn); 0 for an unknown (negative sentinel) value.</summary>
    internal static int NormaliseRotation(int degrees)
    {
        if (degrees == -1) return 0;
        int quarter = (int)Math.Round(degrees / 90.0, MidpointRounding.AwayFromZero);
        int normalised = ((quarter % 4) + 4) % 4;
        return normalised * 90;
    }

    /// <summary>KEY_COLOR_RANGE to the contract's range.</summary>
    internal static VideoColorRange ToColorRange(int range) => range switch
    {
        MediaFormatKeys.ColorRangeFull => VideoColorRange.Full,
        MediaFormatKeys.ColorRangeLimited => VideoColorRange.Limited,
        _ => VideoColorRange.Unspecified
    };

    /// <summary>KEY_COLOR_STANDARD to the contract's matrix (the standard names both the matrix and the primaries).</summary>
    internal static VideoColorMatrix ToColorMatrix(int standard) => standard switch
    {
        -1 => VideoColorMatrix.Unspecified,
        0 => VideoColorMatrix.Unspecified,
        MediaFormatKeys.ColorStandardBt709 => VideoColorMatrix.Bt709,
        MediaFormatKeys.ColorStandardBt601Pal => VideoColorMatrix.Bt601,
        MediaFormatKeys.ColorStandardBt601Ntsc => VideoColorMatrix.Bt601,
        MediaFormatKeys.ColorStandardBt2020 => VideoColorMatrix.Bt2020NonConstant,
        _ => VideoColorMatrix.Other
    };

    /// <summary>KEY_COLOR_STANDARD to the contract's primaries.</summary>
    internal static VideoColorPrimaries ToColorPrimaries(int standard) => standard switch
    {
        -1 => VideoColorPrimaries.Unspecified,
        0 => VideoColorPrimaries.Unspecified,
        MediaFormatKeys.ColorStandardBt709 => VideoColorPrimaries.Bt709,
        MediaFormatKeys.ColorStandardBt601Pal => VideoColorPrimaries.Bt601Pal,
        MediaFormatKeys.ColorStandardBt601Ntsc => VideoColorPrimaries.Bt601Ntsc,
        MediaFormatKeys.ColorStandardBt2020 => VideoColorPrimaries.Bt2020,
        _ => VideoColorPrimaries.Other
    };

    /// <summary>KEY_COLOR_TRANSFER to the contract's transfer.</summary>
    internal static VideoColorTransfer ToColorTransfer(int transfer) => transfer switch
    {
        -1 => VideoColorTransfer.Unspecified,
        0 => VideoColorTransfer.Unspecified,
        MediaFormatKeys.ColorTransferLinear => VideoColorTransfer.Linear,
        MediaFormatKeys.ColorTransferSdrVideo => VideoColorTransfer.Bt709,
        MediaFormatKeys.ColorTransferSt2084 => VideoColorTransfer.Pq,
        MediaFormatKeys.ColorTransferHlg => VideoColorTransfer.Hlg,
        _ => VideoColorTransfer.Other
    };

    /// <summary>The contract's range to KEY_COLOR_RANGE (Unspecified is written as limited, the contract's reading of it).</summary>
    internal static int ToMediaFormatColorRange(VideoColorRange range) =>
        range == VideoColorRange.Full ? MediaFormatKeys.ColorRangeFull : MediaFormatKeys.ColorRangeLimited;

    /// <summary>The contract's matrix to KEY_COLOR_STANDARD; -1 (leave the key unset) when it has no MediaFormat equivalent.</summary>
    internal static int ToMediaFormatColorStandard(VideoColorMatrix matrix) => matrix switch
    {
        VideoColorMatrix.Bt709 => MediaFormatKeys.ColorStandardBt709,
        VideoColorMatrix.Bt601 => MediaFormatKeys.ColorStandardBt601Ntsc,
        VideoColorMatrix.Bt2020NonConstant => MediaFormatKeys.ColorStandardBt2020,
        _ => -1
    };

    /// <summary>The transfer a standard's SDR video uses, written alongside the standard (-1 when the standard is unset).</summary>
    internal static int ToMediaFormatColorTransfer(VideoColorMatrix matrix) =>
        ToMediaFormatColorStandard(matrix) == -1 ? -1 : MediaFormatKeys.ColorTransferSdrVideo;

    /// <summary>
    /// The stored bit depth implied by a codec profile (KEY_PROFILE): 10 for the 10-bit profiles of AVC, HEVC, VP9 and
    /// AV1, 8 for their 8-bit 4:2:0 profiles (and always for VP8), 0 when the profile is unknown or does not fix the depth.
    /// </summary>
    internal static int BitDepthFromProfile(string mime, int profile)
    {
        if (mime == null) return 0;
        if (string.Equals(mime, "video/x-vnd.on2.vp8", StringComparison.OrdinalIgnoreCase)) return 8;
        if (profile <= 0) return 0;
        switch (mime.ToLowerInvariant())
        {
            case "video/avc":
                // Baseline 0x1, Main 0x2, Extended 0x4, High 0x8, High10 0x10, ConstrainedBaseline 0x10000, ConstrainedHigh 0x80000.
                if (profile == 0x10) return 10;
                if (profile == 0x1 || profile == 0x2 || profile == 0x4 || profile == 0x8 || profile == 0x10000 || profile == 0x80000) return 8;
                return 0;
            case "video/hevc":
                // Main 0x1, Main10 0x2, MainStill 0x4, Main10HDR10 0x1000, Main10HDR10Plus 0x2000.
                if (profile == 0x2 || profile == 0x1000 || profile == 0x2000) return 10;
                if (profile == 0x1 || profile == 0x4) return 8;
                return 0;
            case "video/x-vnd.on2.vp9":
                // Profile0 0x1, Profile1 0x2 (8-bit), Profile2 0x4, Profile3 0x8, HDR / HDR10Plus variants 0x1000..0x8000.
                if (profile == 0x1 || profile == 0x2) return 8;
                if (profile == 0x4 || profile == 0x8 || profile == 0x1000 || profile == 0x2000 || profile == 0x4000 || profile == 0x8000) return 10;
                return 0;
            case "video/av01":
                // Main8 0x1, Main10 0x2, Main10HDR10 0x1000, Main10HDR10Plus 0x2000.
                if (profile == 0x1) return 8;
                if (profile == 0x2 || profile == 0x1000 || profile == 0x2000) return 10;
                return 0;
            default:
                return 0;
        }
    }

    /// <summary>The chroma subsampling implied by a codec profile; Unknown when the profile does not fix it.</summary>
    internal static VideoChromaSubsampling ChromaFromProfile(string mime, int profile)
    {
        if (mime == null) return VideoChromaSubsampling.Unknown;
        string lower = mime.ToLowerInvariant();
        if (lower == "video/x-vnd.on2.vp8") return VideoChromaSubsampling.Yuv420;
        if (profile <= 0) return VideoChromaSubsampling.Unknown;

        switch (lower)
        {
            case "video/avc":
                if (profile == 0x20) return VideoChromaSubsampling.Yuv422;
                if (profile == 0x40) return VideoChromaSubsampling.Yuv444;
                return BitDepthFromProfile(mime, profile) > 0 ? VideoChromaSubsampling.Yuv420 : VideoChromaSubsampling.Unknown;
            case "video/x-vnd.on2.vp9":
                // Profiles 1 and 3 are 4:2:2 / 4:4:0 / 4:4:4 - the profile alone does not say which.
                return profile == 0x1 || profile == 0x4 || profile == 0x1000 || profile == 0x4000
                    ? VideoChromaSubsampling.Yuv420
                    : VideoChromaSubsampling.Unknown;
            default:
                return BitDepthFromProfile(mime, profile) > 0 ? VideoChromaSubsampling.Yuv420 : VideoChromaSubsampling.Unknown;
        }
    }

    /// <summary>ffmpeg's pixel-format name for a 4:2:0 picture of a known depth ("yuv420p" / "yuv420p10le"), else null.</summary>
    internal static string PixelFormatName(VideoChromaSubsampling chroma, int bitDepth)
    {
        if (chroma != VideoChromaSubsampling.Yuv420) return null;
        return bitDepth switch
        {
            8 => "yuv420p",
            10 => "yuv420p10le",
            _ => null
        };
    }

    /// <summary>Bits per stored sample of a raw PCM track (KEY_PCM_ENCODING; 16 when absent); 0 for compressed audio.</summary>
    internal static int AudioBitDepth(TrackDescription track)
    {
        if (!string.Equals(track.Mime, "audio/raw", StringComparison.OrdinalIgnoreCase)) return 0;
        return track.PcmEncoding switch
        {
            MediaFormatKeys.PcmEncoding8Bit => 8,
            MediaFormatKeys.PcmEncoding24BitPacked => 24,
            MediaFormatKeys.PcmEncoding32Bit => 32,
            MediaFormatKeys.PcmEncodingFloat => 32,
            _ => 16
        };
    }

    /// <summary>A video track as the contract's stream description.</summary>
    internal static VideoStreamInfo MapVideo(TrackDescription track, TimeSpan containerDuration)
    {
        FrameRate rate = ChooseFrameRate(track);
        TimeSpan duration = track.DurationMicroseconds > 0 ? FromMicroseconds(track.DurationMicroseconds) : containerDuration;
        int bitDepth = BitDepthFromProfile(track.Mime, track.Profile);
        VideoChromaSubsampling chroma = ChromaFromProfile(track.Mime, track.Profile);

        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(track.Language) && track.Language != "und") tags["language"] = track.Language;
        tags["mime"] = track.Mime;

        return new VideoStreamInfo
        {
            Index = track.Index,
            CodecName = CodecNameFromMime(track.Mime),
            Width = Math.Max(0, track.Width),
            Height = Math.Max(0, track.Height),
            FrameRate = rate,
            FrameCount = track.ContainerSampleCount > 0 ? track.ContainerSampleCount : 0,
            Duration = duration,
            BitDepth = bitDepth,
            PixelFormatName = PixelFormatName(chroma, bitDepth),
            ChromaSubsampling = chroma,
            HasAlpha = false,
            RotationDegrees = NormaliseRotation(track.Rotation),
            ColorRange = ToColorRange(track.ColorRange),
            ColorMatrix = ToColorMatrix(track.ColorStandard),
            ColorPrimaries = ToColorPrimaries(track.ColorStandard),
            ColorTransfer = ToColorTransfer(track.ColorTransfer),
            Tags = tags
        };
    }

    /// <summary>An audio track as the contract's stream description.</summary>
    internal static AudioStreamInfo MapAudio(TrackDescription track, TimeSpan containerDuration)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(track.Language) && track.Language != "und") tags["language"] = track.Language;
        tags["mime"] = track.Mime;

        return new AudioStreamInfo
        {
            Index = track.Index,
            CodecName = CodecNameFromMime(track.Mime),
            SampleRate = Math.Max(0, track.SampleRate),
            Channels = Math.Max(0, track.ChannelCount),
            BitDepth = AudioBitDepth(track),
            Duration = track.DurationMicroseconds > 0 ? FromMicroseconds(track.DurationMicroseconds) : containerDuration,
            Tags = tags
        };
    }

    /// <summary>
    /// One subtitle track: the MediaExtractor track index, the codec name in ffmpeg's words (the MIME type itself when
    /// unknown), the KEY_LANGUAGE (left out when absent or "und"), and the MIME type as the "mime" tag. The platform
    /// reports no track title, so Title is null.
    /// </summary>
    internal static SubtitleStreamInfo MapSubtitle(TrackDescription track)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string language = string.IsNullOrEmpty(track.Language) || track.Language == "und" ? null : track.Language;
        if (language != null) tags["language"] = language;
        tags["mime"] = track.Mime;

        return new SubtitleStreamInfo
        {
            Index = track.Index,
            CodecName = CodecNameFromMime(track.Mime),
            Language = language,
            Tags = tags
        };
    }

    /// <summary>
    /// The whole-file description: the container MIME, the duration (the retriever's, else the longest track), every
    /// video, audio and subtitle track in file order, no chapters, and the container tags.
    /// </summary>
    internal static MediaInfo BuildMediaInfo(string containerMime, long containerDurationMilliseconds, IReadOnlyList<TrackDescription> tracks, IReadOnlyDictionary<string, string> tags)
    {
        long longestTrack = 0;
        foreach (TrackDescription track in tracks)
        {
            if (track.DurationMicroseconds > longestTrack) longestTrack = track.DurationMicroseconds;
        }

        // The track durations are microsecond-exact; the retriever's is whole milliseconds. Prefer the exact one.
        TimeSpan duration = longestTrack > 0
            ? FromMicroseconds(longestTrack)
            : containerDurationMilliseconds > 0 ? TimeSpan.FromMilliseconds(containerDurationMilliseconds) : TimeSpan.Zero;

        var video = new List<VideoStreamInfo>();
        var audio = new List<AudioStreamInfo>();
        var subtitles = new List<SubtitleStreamInfo>();
        foreach (TrackDescription track in tracks)
        {
            if (track.IsVideo) video.Add(MapVideo(track, duration));
            else if (track.IsAudio) audio.Add(MapAudio(track, duration));
            else if (track.IsSubtitle) subtitles.Add(MapSubtitle(track));
        }

        // Chapters stay empty: MediaExtractor and MediaMetadataRetriever expose no chapter list.
        return new MediaInfo
        {
            ContainerFormat = containerMime,
            Duration = duration,
            VideoStreams = video,
            AudioStreams = audio,
            SubtitleStreams = subtitles,
            Tags = tags
        };
    }

    /// <summary>
    /// The container tags from MediaMetadataRetriever values: every non-empty value under its key, plus
    /// "creation_time" (ISO 8601, UTC) when the retriever's "date" value parses - the name ffmpeg reports it under.
    /// </summary>
    internal static Dictionary<string, string> BuildTags(IEnumerable<KeyValuePair<string, string>> retrieverValues)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> pair in retrieverValues)
        {
            if (string.IsNullOrWhiteSpace(pair.Value)) continue;
            tags[pair.Key] = pair.Value.Trim();
        }

        if (tags.TryGetValue("date", out string date))
        {
            if (TryParseRetrieverDate(date, out DateTimeOffset when))
                tags["creation_time"] = when.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
            else if (date.StartsWith("1904", StringComparison.Ordinal))
                tags.Remove("date"); // an unset MP4 time (zero seconds since 1904) is no date at all
        }

        return tags;
    }

    /// <summary>
    /// Parses MediaMetadataRetriever's METADATA_KEY_DATE ("20261011T120304.000Z" or "20261011T120304"); the all-zero
    /// date ("19040101T000000.000Z", an unset MP4 time) is treated as absent.
    /// </summary>
    internal static bool TryParseRetrieverDate(string text, out DateTimeOffset value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string trimmed = text.Trim();
        string[] formats = { "yyyyMMdd'T'HHmmss.fff'Z'", "yyyyMMdd'T'HHmmss'Z'", "yyyyMMdd'T'HHmmss.fff", "yyyyMMdd'T'HHmmss" };
        if (!DateTimeOffset.TryParseExact(trimmed, formats, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out value))
        {
            return false;
        }

        return value.Year > 1904;
    }

    /// <summary>Microseconds to a TimeSpan (exact: one tick is a tenth of a microsecond).</summary>
    internal static TimeSpan FromMicroseconds(long microseconds) => TimeSpan.FromTicks(microseconds * 10);

    /// <summary>A TimeSpan to whole microseconds (rounded down).</summary>
    internal static long ToMicroseconds(TimeSpan time) => time.Ticks / 10;
}
