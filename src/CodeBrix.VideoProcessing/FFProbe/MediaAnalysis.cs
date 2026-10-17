using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Builders.MetaData;
using CodeBrix.VideoProcessing.Core.Media;

namespace CodeBrix.VideoProcessing; //was previously: FFMpegCore;

internal class MediaAnalysis : IMediaAnalysis
{
    internal MediaAnalysis(FFProbeAnalysis analysis)
    {
        Format = ParseFormat(analysis.Format);
        var streams = analysis.Streams ?? new List<FFProbeStream>();
        Chapters = (analysis.Chapters ?? new List<Chapter>()).Select(c => ParseChapter(c)).ToList();
        VideoStreams = streams.Where(stream => stream.CodecType == "video").Select(ParseVideoStream).ToList();
        AudioStreams = streams.Where(stream => stream.CodecType == "audio").Select(ParseAudioStream).ToList();
        SubtitleStreams = streams.Where(stream => stream.CodecType == "subtitle").Select(ParseSubtitleStream).ToList();
        ErrorData = analysis.ErrorData;
    }

    public TimeSpan Duration => new[] { Format.Duration, PrimaryVideoStream?.Duration ?? TimeSpan.Zero, PrimaryAudioStream?.Duration ?? TimeSpan.Zero }.Max();

    public MediaFormat Format { get; }

    public List<ChapterData> Chapters { get; }

    public AudioStream PrimaryAudioStream => AudioStreams.OrderBy(stream => stream.Index).FirstOrDefault();
    public VideoStream PrimaryVideoStream => VideoStreams.OrderBy(stream => stream.Index).FirstOrDefault();
    public SubtitleStream PrimarySubtitleStream => SubtitleStreams.OrderBy(stream => stream.Index).FirstOrDefault();

    public List<VideoStream> VideoStreams { get; }
    public List<AudioStream> AudioStreams { get; }
    public List<SubtitleStream> SubtitleStreams { get; }
    public IReadOnlyList<string> ErrorData { get; }

    private MediaFormat ParseFormat(Format analysisFormat)
    {
        return new MediaFormat
        {
            Duration = MediaAnalysisUtils.ParseDuration(analysisFormat.Duration),
            StartTime = MediaAnalysisUtils.ParseDuration(analysisFormat.StartTime),
            FormatName = analysisFormat.FormatName,
            FormatLongName = analysisFormat.FormatLongName,
            StreamCount = analysisFormat.NbStreams,
            ProbeScore = analysisFormat.ProbeScore,
            BitRate = long.Parse(analysisFormat.BitRate ?? "0"),
            Tags = analysisFormat.Tags.ToCaseInsensitive()
        };
    }

    private string GetValue(string tagName, Dictionary<string, string> tags, string defaultValue)
    {
        return tags == null ? defaultValue : tags.TryGetValue(tagName, out var value) ? value : defaultValue;
    }

    private ChapterData ParseChapter(Chapter analysisChapter)
    {
        var title = GetValue("title", analysisChapter.Tags, "TitleValueNotSet");
        var start = MediaAnalysisUtils.ParseDuration(analysisChapter.StartTime);
        var end = MediaAnalysisUtils.ParseDuration(analysisChapter.EndTime);

        return new ChapterData(title, start, end);
    }

    private int? GetBitDepth(FFProbeStream stream)
    {
        var bitDepth = int.TryParse(stream.BitsPerRawSample, out var bprs) ? bprs : stream.BitsPerSample;
        return bitDepth == 0 ? null : bitDepth;
    }

    private VideoStream ParseVideoStream(FFProbeStream stream)
    {
        return new VideoStream
        {
            Index = stream.Index,
            AvgFrameRate = MediaAnalysisUtils.DivideRatio(MediaAnalysisUtils.ParseRatioDouble(stream.AvgFrameRate, '/')),
            BitRate = !string.IsNullOrEmpty(stream.BitRate) ? MediaAnalysisUtils.ParseLongInvariant(stream.BitRate) : default,
            BitsPerRawSample = !string.IsNullOrEmpty(stream.BitsPerRawSample) ? MediaAnalysisUtils.ParseIntInvariant(stream.BitsPerRawSample) : default,
            CodecName = stream.CodecName,
            CodecLongName = stream.CodecLongName,
            CodecTag = stream.CodecTag,
            CodecTagString = stream.CodecTagString,
            DisplayAspectRatio = MediaAnalysisUtils.ParseRatioInt(stream.DisplayAspectRatio, ':'),
            SampleAspectRatio = MediaAnalysisUtils.ParseRatioInt(stream.SampleAspectRatio, ':'),
            Duration = MediaAnalysisUtils.ParseDuration(stream.Duration),
            StartTime = MediaAnalysisUtils.ParseDuration(stream.StartTime),
            FrameRate = MediaAnalysisUtils.DivideRatio(MediaAnalysisUtils.ParseRatioDouble(stream.FrameRate, '/')),
            Height = stream.Height ?? 0,
            Width = stream.Width ?? 0,
            Profile = stream.Profile,
            PixelFormat = stream.PixelFormat,
            Level = stream.Level,
            ColorRange = stream.ColorRange,
            ColorSpace = stream.ColorSpace,
            ColorTransfer = stream.ColorTransfer,
            ColorPrimaries = stream.ColorPrimaries,
            Rotation = MediaAnalysisUtils.ParseRotation(stream),
            ExactFrameRate = MediaAnalysisUtils.ParseExactFrameRate(stream.AvgFrameRate, stream.FrameRate),
            RealFrameRate = MediaAnalysisUtils.ParseExactFrameRate(stream.FrameRate, null),
            FrameCount = MediaAnalysisUtils.ParseFrameCount(stream.NbFrames),
            DisplayMatrixRotation = MediaAnalysisUtils.ParseDisplayMatrixRotation(stream),
            IsDisplayMatrixMirrored = MediaAnalysisUtils.ParseDisplayMatrixMirrored(stream),
            Language = stream.GetLanguage(),
            Disposition = MediaAnalysisUtils.FormatDisposition(stream.Disposition),
            Tags = stream.Tags.ToCaseInsensitive(),
            BitDepth = GetBitDepth(stream)
        };
    }

    private AudioStream ParseAudioStream(FFProbeStream stream)
    {
        return new AudioStream
        {
            Index = stream.Index,
            BitRate = !string.IsNullOrEmpty(stream.BitRate) ? MediaAnalysisUtils.ParseLongInvariant(stream.BitRate) : default,
            CodecName = stream.CodecName,
            CodecLongName = stream.CodecLongName,
            CodecTag = stream.CodecTag,
            CodecTagString = stream.CodecTagString,
            Channels = stream.Channels ?? default,
            ChannelLayout = stream.ChannelLayout,
            Duration = MediaAnalysisUtils.ParseDuration(stream.Duration),
            StartTime = MediaAnalysisUtils.ParseDuration(stream.StartTime),
            SampleRateHz = !string.IsNullOrEmpty(stream.SampleRate) ? MediaAnalysisUtils.ParseIntInvariant(stream.SampleRate) : default,
            Profile = stream.Profile,
            Language = stream.GetLanguage(),
            Disposition = MediaAnalysisUtils.FormatDisposition(stream.Disposition),
            Tags = stream.Tags.ToCaseInsensitive(),
            BitDepth = GetBitDepth(stream)
        };
    }

    private SubtitleStream ParseSubtitleStream(FFProbeStream stream)
    {
        return new SubtitleStream
        {
            Index = stream.Index,
            BitRate = !string.IsNullOrEmpty(stream.BitRate) ? MediaAnalysisUtils.ParseLongInvariant(stream.BitRate) : default,
            CodecName = stream.CodecName,
            CodecLongName = stream.CodecLongName,
            Duration = MediaAnalysisUtils.ParseDuration(stream.Duration),
            StartTime = MediaAnalysisUtils.ParseDuration(stream.StartTime),
            Language = stream.GetLanguage(),
            Disposition = MediaAnalysisUtils.FormatDisposition(stream.Disposition),
            Tags = stream.Tags.ToCaseInsensitive()
        };
    }
}

public static class MediaAnalysisUtils
{
    private static readonly Regex DurationRegex = new(@"^(\d+):(\d{1,2}):(\d{1,2})(?:\.(\d+))?", RegexOptions.Compiled);

    internal static Dictionary<string, string> ToCaseInsensitive(this Dictionary<string, string> dictionary)
    {
        return dictionary?.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>();
    }

    public static double DivideRatio((double, double) ratio)
    {
        return ratio.Item1 / ratio.Item2;
    }

    public static (int, int) ParseRatioInt(string input, char separator)
    {
        if (string.IsNullOrEmpty(input))
        {
            return (0, 0);
        }

        var ratio = input.Split(separator);
        return (ParseIntInvariant(ratio[0]), ParseIntInvariant(ratio[1]));
    }

    public static (double, double) ParseRatioDouble(string input, char separator)
    {
        if (string.IsNullOrEmpty(input))
        {
            return (0, 0);
        }

        var ratio = input.Split(separator);
        return (ratio.Length > 0 ? ParseDoubleInvariant(ratio[0]) : 0, ratio.Length > 1 ? ParseDoubleInvariant(ratio[1]) : 0);
    }

    public static double ParseDoubleInvariant(string line)
    {
        return double.Parse(line, NumberStyles.Any, CultureInfo.InvariantCulture);
    }

    public static int ParseIntInvariant(string line)
    {
        return int.Parse(line, NumberStyles.Any, CultureInfo.InvariantCulture);
    }

    public static long ParseLongInvariant(string line)
    {
        return long.Parse(line, NumberStyles.Any, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses an ffprobe / ffmpeg time: sexagesimal "H:MM:SS.ffffff" (what <c>-sexagesimal</c> prints) or plain seconds
    /// ("3.003000"). The whole fraction is kept to the tick (100 ns), so ffprobe's microseconds survive exactly.
    /// </summary>
    /// <param name="duration">The text; null, empty, negative or unreadable text gives zero.</param>
    /// <returns>The time.</returns>
    public static TimeSpan ParseDuration(string duration)
    {
        if (string.IsNullOrEmpty(duration))
        {
            return TimeSpan.Zero;
        }

        var match = DurationRegex.Match(duration);
        if (match.Success)
        {
            var hours = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var minutes = long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var seconds = long.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            var ticks = hours * TimeSpan.TicksPerHour + minutes * TimeSpan.TicksPerMinute + seconds * TimeSpan.TicksPerSecond;
            return TimeSpan.FromTicks(ticks + FractionTicks(match.Groups[4].Value));
        }

        // Plain seconds, as ffprobe prints them without -sexagesimal.
        if (double.TryParse(duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var plainSeconds) &&
            plainSeconds > 0 && plainSeconds < TimeSpan.MaxValue.TotalSeconds)
        {
            return TimeSpan.FromTicks((long)Math.Round(plainSeconds * TimeSpan.TicksPerSecond));
        }

        return TimeSpan.Zero;
    }

    /// <summary>
    /// Parses an ffprobe frame rate ("30000/1001") exactly, falling back to a second one when the first is missing or
    /// not a positive rational ("0/0").
    /// </summary>
    /// <param name="preferred">The rate to use when it is readable.</param>
    /// <param name="fallback">The rate to use otherwise; may be null.</param>
    /// <returns>The reduced rate, or <see cref="FrameRate.Unspecified" /> when neither is readable.</returns>
    public static FrameRate ParseExactFrameRate(string preferred, string fallback)
    {
        if (FrameRate.TryParse(preferred, out var rate))
        {
            return rate;
        }

        return FrameRate.TryParse(fallback, out rate) ? rate : FrameRate.Unspecified;
    }

    /// <summary>Parses ffprobe's <c>nb_frames</c>.</summary>
    /// <param name="frameCount">The text; may be null.</param>
    /// <returns>The frame count, or null when it is missing or not a whole number.</returns>
    public static long? ParseFrameCount(string frameCount)
    {
        return long.TryParse(frameCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : null;
    }

    /// <summary>
    /// Reads the counter-clockwise rotation, in degrees, from a stream's display matrix side data: the "Display Matrix"
    /// entry, or failing that the first side data entry that carries a rotation.
    /// </summary>
    /// <param name="fFProbeStream">The ffprobe stream.</param>
    /// <returns>The rotation, or null when the stream has no display matrix rotation.</returns>
    public static double? ParseDisplayMatrixRotation(FFProbeStream fFProbeStream)
    {
        var sideData = fFProbeStream.SideData;
        if (sideData == null)
        {
            return null;
        }

        var entry = sideData.Find(item => item != null && item.ContainsKey("rotation") &&
                                          item.TryGetValue("side_data_type", out var type) && type?.ToString() == "Display Matrix")
                    ?? sideData.Find(item => item != null && item.ContainsKey("rotation"));
        if (entry == null || entry["rotation"] == null)
        {
            return null;
        }

        var rotation = entry["rotation"];
        if (rotation.TryGetValue(out double number))
        {
            return number;
        }

        return double.TryParse(rotation.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : null;
    }

    // ADDITION OF THIS PORT - feeds VideoSourceInfo.IsMirrored. True when the stream's "Display Matrix" side data
    // carries a matrix whose determinant is negative (the picture is mirrored, on top of any rotation); false when
    // there is no display matrix or its text cannot be read.
    internal static bool ParseDisplayMatrixMirrored(FFProbeStream fFProbeStream)
    {
        var entry = fFProbeStream.SideData?.Find(item => item != null &&
                                                         item.TryGetValue("side_data_type", out var type) && type?.ToString() == "Display Matrix");
        if (entry == null || !entry.TryGetValue("displaymatrix", out var text) || text == null)
        {
            return false;
        }

        var matrix = ParseDisplayMatrix(text.ToString());
        return matrix != null && IsMirroredDisplayMatrix(matrix);
    }

    // ffprobe prints the display matrix as three rows, each "<offset>:" followed by three integers (16.16 fixed point
    // for the first two columns, 2.30 for the third), for example
    //     "\n00000000:            0       65536           0\n00000001:       -65536 ...". Null when it is not nine integers.
    internal static long[] ParseDisplayMatrix(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var values = new List<long>(9);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = line.IndexOf(':');
            var row = colon >= 0 ? line.Substring(colon + 1) : line;
            foreach (var token in row.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    return null;
                }

                values.Add(value);
            }
        }

        return values.Count == 9 ? values.ToArray() : null;
    }

    // A display matrix (row-major, the 2x2 linear part in elements 0, 1, 3 and 4) mirrors the picture when that part's
    // determinant is negative: a rotation alone keeps it positive, one flip makes it negative (two flips are a
    // 180-degree rotation). The translation and the third column do not change the sign for a well-formed matrix.
    internal static bool IsMirroredDisplayMatrix(long[] matrix)
    {
        if (matrix == null || matrix.Length < 5)
        {
            return false;
        }

        return DisplayMatrixDeterminant(matrix) < 0;
    }

    // The determinant of the display matrix's 2x2 linear part, in (16.16)^2 units; computed in double so that
    // full-range 32-bit entries cannot overflow.
    internal static double DisplayMatrixDeterminant(long[] matrix)
    {
        return (double)matrix[0] * matrix[4] - (double)matrix[1] * matrix[3];
    }

    private static long FractionTicks(string digits)
    {
        if (string.IsNullOrEmpty(digits))
        {
            return 0;
        }

        // Seven digits are ticks; pad shorter fractions ("83" is 830 ms) and round longer ones.
        if (digits.Length <= 7)
        {
            return long.Parse(digits.PadRight(7, '0'), CultureInfo.InvariantCulture);
        }

        var ticks = long.Parse(digits.Substring(0, 7), CultureInfo.InvariantCulture);
        return digits[7] >= '5' ? ticks + 1 : ticks;
    }

    public static int ParseRotation(FFProbeStream fFProbeStream)
    {
        var displayMatrixSideData = fFProbeStream.SideData?.Find(item =>
            item.TryGetValue("side_data_type", out var rawSideDataType) && rawSideDataType.ToString() == "Display Matrix");

        if (displayMatrixSideData?.TryGetValue("rotation", out var rawRotation) ?? false)
        {
            return (int)float.Parse(rawRotation.ToString());
        }

        return (int)float.Parse(fFProbeStream.GetRotate() ?? "0");
    }

    public static Dictionary<string, bool> FormatDisposition(Dictionary<string, int> disposition)
    {
        if (disposition == null)
        {
            return null;
        }

        var result = new Dictionary<string, bool>(disposition.Count, StringComparer.Ordinal);

        foreach (var pair in disposition)
        {
            result.Add(pair.Key, ToBool(pair.Value));
        }

        static bool ToBool(int value)
        {
            return value switch
            {
                0 => false,
                1 => true,
                _ => throw new ArgumentOutOfRangeException(nameof(value),
                    $"Not expected disposition state value: {value}")
            };
        }

        return result;
    }
}
