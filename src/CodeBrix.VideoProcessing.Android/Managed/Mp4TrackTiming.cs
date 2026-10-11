using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CodeBrix.VideoProcessing.Core.Media;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>What the managed MP4 reader found about one track's timing.</summary>
internal sealed class Mp4TrackTimingInfo
{
    /// <summary>The handler type ("vide", "soun", ...).</summary>
    public string Handler { get; set; }

    /// <summary>The media timescale (ticks per second).</summary>
    public long Timescale { get; set; }

    /// <summary>The media duration in timescale ticks.</summary>
    public long Duration { get; set; }

    /// <summary>The number of samples (frames for video).</summary>
    public long SampleCount { get; set; }

    /// <summary>The exact frame rate (constant sample delta) or the snapped average rate; unspecified when unknown.</summary>
    public FrameRate FrameRate { get; set; }
}

/// <summary>
/// Reads the sample tables of an MP4 / MOV file to get what MediaExtractor rounds away: the EXACT frame rate of a
/// video track as a rational (timescale / sample delta - so 30000/1001 stays 30000/1001) and its frame count.
/// </summary>
/// <remarks>
/// The rate comes from PRESENTATION times (decode times from stts plus composition offsets from ctts, sorted): it is
/// timescale / the DOMINANT step between neighbouring presentation times when at least half of the steps share it -
/// so a constant-rate track gives its exact rational even when its decode times are irregular around reordered
/// B-frames (MediaMuxer writes such tables). A track with no dominant step (true variable rate) gets its AVERAGE
/// rate, snapped like a float KEY_FRAME_RATE (<see cref="MediaFormatMapping.FrameRateFromValue" />). Anything
/// unreadable gives no entry, never an exception.
/// </remarks>
internal static class Mp4TrackTiming
{
    /// <summary>The timing of every track in file order; an empty list when the file is not a readable MP4.</summary>
    internal static List<Mp4TrackTimingInfo> Read(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            List<Mp4TopLevelBox> boxes = Mp4File.ReadTopLevel(stream);
            int moovIndex = Mp4File.IndexOf(boxes, "moov");
            if (moovIndex < 0) return new List<Mp4TrackTimingInfo>();
            return Read(Mp4Node.Parse(Mp4File.ReadBox(stream, boxes[moovIndex])));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException)
        {
            return new List<Mp4TrackTimingInfo>();
        }
    }

    /// <summary>The timing of every track of a parsed moov.</summary>
    internal static List<Mp4TrackTimingInfo> Read(Mp4Node moov)
    {
        var tracks = new List<Mp4TrackTimingInfo>();
        foreach (Mp4Node trak in moov.Children)
        {
            if (trak.Type != "trak") continue;
            Mp4Node mdia = trak.Child("mdia");
            Mp4Node stbl = mdia?.Child("minf")?.Child("stbl");
            if (mdia == null || stbl == null) continue;

            var info = new Mp4TrackTimingInfo { Handler = ReadHandler(mdia.Child("hdlr")) };
            ReadMediaHeader(mdia.Child("mdhd"), info);
            List<(long Count, long Delta)> stts = ReadTimeToSample(stbl.Child("stts"));
            long sizeCount = ReadSampleSizeCount(stbl.Child("stsz"));
            long sttsCount = 0;
            foreach ((long count, _) in stts) sttsCount += count;
            info.SampleCount = sizeCount > 0 ? sizeCount : sttsCount;
            info.FrameRate = RateFrom(stts, ReadCompositionOffsets(stbl.Child("ctts")), info.Timescale);
            tracks.Add(info);
        }

        return tracks;
    }

    /// <summary>The frame rate implied by a track's decode durations (stts) and composition offsets (ctts; may be empty).</summary>
    internal static FrameRate RateFrom(IReadOnlyList<(long Count, long Delta)> stts, IReadOnlyList<(long Count, long Offset)> ctts, long timescale)
    {
        if (timescale <= 0 || stts.Count == 0) return FrameRate.Unspecified;

        long total = 0;
        foreach ((long count, _) in stts) total += count;
        if (total <= 0 || total > 50_000_000) return FrameRate.Unspecified;

        // Presentation times: decode time (running sum of stts) + composition offset (ctts), sorted.
        long[] times = new long[total];
        long decode = 0;
        int sample = 0;
        foreach ((long count, long delta) in stts)
        {
            for (long i = 0; i < count; i++)
            {
                times[sample++] = decode;
                decode += delta;
            }
        }

        if (total == 1) return stts[0].Delta > 0 && stts[0].Delta <= int.MaxValue && timescale <= int.MaxValue ? new FrameRate((int)timescale, (int)stts[0].Delta) : FrameRate.Unspecified;

        sample = 0;
        foreach ((long count, long offset) in ctts)
        {
            for (long i = 0; i < count && sample < times.Length; i++) times[sample++] += offset;
        }

        Array.Sort(times);

        // The DOMINANT presentation step: the difference between neighbouring presentation times that occurs most.
        var countsByStep = new Dictionary<long, long>();
        for (int i = 1; i < times.Length; i++)
        {
            long step = times[i] - times[i - 1];
            if (step <= 0) continue;
            countsByStep.TryGetValue(step, out long sofar);
            countsByStep[step] = sofar + 1;
        }

        long dominant = 0;
        long dominantCount = 0;
        foreach (KeyValuePair<long, long> pair in countsByStep)
        {
            if (pair.Value > dominantCount || (pair.Value == dominantCount && pair.Key < dominant))
            {
                dominant = pair.Key;
                dominantCount = pair.Value;
            }
        }

        if (dominant > 0 && dominantCount * 2 >= times.Length - 1 && timescale <= int.MaxValue && dominant <= int.MaxValue)
        {
            return new FrameRate((int)timescale, (int)dominant);
        }

        double span = times[^1] - times[0];
        return span > 0 ? MediaFormatMapping.FrameRateFromValue((times.Length - 1) * (double)timescale / span) : FrameRate.Unspecified;
    }

    /// <summary>The frame rate implied by decode durations alone (no reordering).</summary>
    internal static FrameRate RateFrom(IReadOnlyList<(long Count, long Delta)> stts, long timescale) =>
        RateFrom(stts, Array.Empty<(long, long)>(), timescale);

    private static List<(long Count, long Offset)> ReadCompositionOffsets(Mp4Node ctts)
    {
        var entries = new List<(long, long)>();
        if (ctts?.Raw == null || ctts.Raw.Length < 16) return entries;
        ReadOnlySpan<byte> raw = ctts.Raw;
        uint count = BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(12));
        for (long i = 0; i < count && 16 + (i + 1) * 8 <= raw.Length; i++)
        {
            int o = 16 + (int)i * 8;
            // Read as signed in both versions (version 0 is unsigned on paper, but writers use it signed too).
            entries.Add((BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(o)), BinaryPrimitives.ReadInt32BigEndian(raw.Slice(o + 4))));
        }

        return entries;
    }

    private static string ReadHandler(Mp4Node hdlr)
    {
        if (hdlr?.Raw == null || hdlr.Raw.Length < 20) return null;
        return Encoding.Latin1.GetString(hdlr.Raw, 16, 4);
    }

    private static void ReadMediaHeader(Mp4Node mdhd, Mp4TrackTimingInfo info)
    {
        if (mdhd?.Raw == null || mdhd.Raw.Length < 32) return;
        ReadOnlySpan<byte> raw = mdhd.Raw;
        if (raw[8] == 1)
        {
            if (raw.Length < 44) return;
            info.Timescale = BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(28));
            info.Duration = (long)BinaryPrimitives.ReadUInt64BigEndian(raw.Slice(32));
        }
        else
        {
            info.Timescale = BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(20));
            info.Duration = BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(24));
        }
    }

    private static List<(long Count, long Delta)> ReadTimeToSample(Mp4Node stts)
    {
        var entries = new List<(long, long)>();
        if (stts?.Raw == null || stts.Raw.Length < 16) return entries;
        ReadOnlySpan<byte> raw = stts.Raw;
        uint count = BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(12));
        for (long i = 0; i < count && 16 + (i + 1) * 8 <= raw.Length; i++)
        {
            int o = 16 + (int)i * 8;
            entries.Add((BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(o)), BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(o + 4))));
        }

        return entries;
    }

    private static long ReadSampleSizeCount(Mp4Node stsz)
    {
        if (stsz?.Raw == null || stsz.Raw.Length < 20) return 0;
        return BinaryPrimitives.ReadUInt32BigEndian(stsz.Raw.AsSpan(16));
    }
}
