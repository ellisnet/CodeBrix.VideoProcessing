using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBrix.VideoProcessing.Arguments;
using CodeBrix.VideoProcessing.Instances;

namespace CodeBrix.VideoProcessing;

/// <summary>
///     The opt-in fallback from SVT-AV1 (<c>libsvtav1</c>) to libaom (<c>libaom-av1</c>), and the explanation given
///     when <c>libsvtav1</c> is missing. An addition of this port - there is nothing like it in FFMpegCore. See
///     <see cref="FFOptions.AllowAv1EncoderFallback" /> and AV1 ENCODER FALLBACK in MAINTAINER-README.txt.
/// </summary>
internal static class Av1EncoderFallback
{
    internal const string SvtAv1EncoderName = "libsvtav1";
    internal const string AomAv1EncoderName = "libaom-av1";

    //libaom-av1's -cpu-used runs 0 (slowest) to 8 (fastest). SVT-AV1's -preset runs 0 to 13, so the top of its range
    //has no libaom equivalent and is capped here.
    internal const int AomMaximumCpuUsed = 8;

    internal const string MissingSvtAv1Explanation =
        "The SVT-AV1 encoder library (libsvtav1) is not present on this machine, or the ffmpeg being used could not find it.";

    internal const string EnableFallbackSuggestion =
        "To encode AV1 without installing anything, opt in to the libaom-av1 fallback by setting " +
        "FFOptions.AllowAv1EncoderFallback = true (for example " +
        "GlobalFFOptions.Configure(options => options.AllowAv1EncoderFallback = true)), " +
        "or install an ffmpeg build that includes libsvtav1.";

    internal const string LibAomMissingProblem =
        "this ffmpeg has no libaom-av1 encoder either, so there was nothing to fall back to";

    private const string UnrecognisedRequestProblem =
        "libsvtav1 was not selected in a form the fallback can rewrite - WithVideoCodec(\"libsvtav1\"), " +
        "or a WithCustomArgument that is exactly \"-c:v libsvtav1\"";

    private static readonly Regex UnknownEncoderRegex = new(@"Unknown encoder '([^']+)'", RegexOptions.Compiled);
    private static readonly Regex EncoderListingRegex = new(@"^\s*[VASD\.][F\.][S\.][X\.][B\.][D\.]\s+(\S+)", RegexOptions.Compiled);
    private static readonly Regex CustomVideoCodecRegex = new(@"^\s*-((?:c|codec):v(?::\d+)?|vcodec)\s+(\S+)\s*$", RegexOptions.Compiled);
    private static readonly Regex CustomNumericPresetRegex = new(@"^\s*-preset(?::v(?::\d+)?)?\s+(-?\d+)\s*$", RegexOptions.Compiled);
    private static readonly Regex CustomConstantRateFactorRegex = new(@"^\s*-crf(?::v(?::\d+)?)?\s+\S+\s*$", RegexOptions.Compiled);
    private static readonly Regex CustomSvtAv1ParamsRegex = new(@"^\s*-svtav1-params(?:\s|$)", RegexOptions.Compiled);
    private static readonly Regex CustomVideoBitrateRegex = new(@"^\s*-(?:b:v(?::\d+)?|vb)(?:\s|$)", RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<string, IReadOnlyCollection<string>> EncoderCache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Rewrites, in place, every output of <paramref name="arguments" /> that selects <c>libsvtav1</c> so that it
    ///     uses <c>libaom-av1</c> instead - but only when <c>libsvtav1</c> is unavailable and <c>libaom-av1</c> is.
    /// </summary>
    /// <param name="arguments">The ordered input and output arguments of one command.</param>
    /// <param name="encoderIsAvailable">
    ///     Answers whether the ffmpeg that will run has an encoder. Only called when an output selects <c>libsvtav1</c>.
    /// </param>
    internal static Outcome Apply(List<IArgument> arguments, Func<string, bool> encoderIsAvailable)
    {
        var requesting = OutputSegments(arguments)
            .Where(segment => arguments.Skip(segment.Start).Take(segment.Length).Any(IsSvtAv1Codec))
            .ToList();

        if (requesting.Count == 0 || encoderIsAvailable(SvtAv1EncoderName))
        {
            return Outcome.NotNeeded;
        }

        if (!encoderIsAvailable(AomAv1EncoderName))
        {
            return Outcome.Unavailable(LibAomMissingProblem);
        }

        var notes = new List<string>
        {
            "libsvtav1 (SVT-AV1) is not available in this ffmpeg, so FFOptions.AllowAv1EncoderFallback encoded AV1 with libaom-av1 instead."
        };

        var rewritten = new List<IArgument>(arguments.Count + requesting.Count);
        var next = 0;
        foreach (var segment in requesting)
        {
            rewritten.AddRange(arguments.GetRange(next, segment.Start - next));
            rewritten.AddRange(RewriteSegment(arguments.GetRange(segment.Start, segment.Length), notes));
            next = segment.Start + segment.Length;
        }

        rewritten.AddRange(arguments.GetRange(next, arguments.Count - next));

        arguments.Clear();
        arguments.AddRange(rewritten);

        return Outcome.AppliedWith(notes);
    }

    /// <summary>
    ///     Whether the ffmpeg at <paramref name="ffmpegBinaryPath" /> has an encoder, from its <c>-encoders</c> listing.
    /// </summary>
    internal static bool IsEncoderAvailable(string ffmpegBinaryPath, string encoderName, bool useCache)
    {
        if (useCache && EncoderCache.TryGetValue(ffmpegBinaryPath, out var cached))
        {
            return cached.Contains(encoderName);
        }

        var result = Instance.Finish(ffmpegBinaryPath, "-hide_banner -encoders");
        var encoders = ParseEncoderListing(result.OutputData);

        //A listing that failed is not remembered, so a later run gets to ask again.
        if (useCache && result.ExitCode == 0)
        {
            EncoderCache.TryAdd(ffmpegBinaryPath, encoders);
        }

        return encoders.Contains(encoderName);
    }

    /// <summary>The encoder names in the output of <c>ffmpeg -encoders</c>.</summary>
    internal static IReadOnlyCollection<string> ParseEncoderListing(IEnumerable<string> lines)
    {
        var encoders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines ?? Enumerable.Empty<string>())
        {
            var match = EncoderListingRegex.Match(line ?? string.Empty);
            if (match.Success)
            {
                encoders.Add(match.Groups[1].Value);
            }
        }

        return encoders;
    }

    /// <summary>The first encoder ffmpeg's standard error reports as unknown, or null when there is none.</summary>
    internal static string FindUnknownEncoder(IEnumerable<string> errorData)
    {
        foreach (var line in errorData ?? Enumerable.Empty<string>())
        {
            var match = UnknownEncoderRegex.Match(line ?? string.Empty);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    /// <summary>The explanation put in front of ffmpeg's own error when <c>libsvtav1</c> is unknown.</summary>
    /// <param name="fallbackEnabled">Whether <see cref="FFOptions.AllowAv1EncoderFallback" /> was on for the run.</param>
    /// <param name="fallbackProblem">Why the fallback could not be used, when it was on; null when not known.</param>
    internal static string DescribeMissingSvtAv1(bool fallbackEnabled, string fallbackProblem)
    {
        if (!fallbackEnabled)
        {
            return MissingSvtAv1Explanation + " " + EnableFallbackSuggestion;
        }

        return MissingSvtAv1Explanation +
               " FFOptions.AllowAv1EncoderFallback is enabled, but the fallback could not be used: " +
               (fallbackProblem ?? UnrecognisedRequestProblem) +
               ". Install an ffmpeg build that includes libsvtav1 or libaom-av1.";
    }

    private static List<Segment> OutputSegments(List<IArgument> arguments)
    {
        //Each output's options come straight before its output argument, after the previous input or output.
        var segments = new List<Segment>();
        var start = 0;
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index] is IInputArgument)
            {
                start = index + 1;
            }
            else if (arguments[index] is IOutputArgument)
            {
                segments.Add(new Segment(start, index - start));
                start = index + 1;
            }
        }

        return segments;
    }

    private static List<IArgument> RewriteSegment(List<IArgument> segment, List<string> notes)
    {
        var hasVideoBitrate = segment.Any(IsVideoBitrate);
        var result = new List<IArgument>(segment.Count + 1);

        foreach (var argument in segment)
        {
            if (argument is VideoCodecArgument codecArgument && codecArgument.Codec == SvtAv1EncoderName)
            {
                result.Add(new VideoCodecArgument(AomAv1EncoderName));
                continue;
            }

            if (argument is CustomArgument customCodec && TryMatch(CustomVideoCodecRegex, customCodec, out var codecMatch) &&
                codecMatch.Groups[2].Value == SvtAv1EncoderName)
            {
                result.Add(new CustomArgument($"-{codecMatch.Groups[1].Value} {AomAv1EncoderName}"));
                continue;
            }

            if (TryGetNumericPreset(argument, out var preset))
            {
                var cpuUsed = Math.Clamp(preset, 0, AomMaximumCpuUsed);
                result.Add(new CustomArgument("-cpu-used " + cpuUsed.ToString(CultureInfo.InvariantCulture)));
                notes.Add(cpuUsed == preset
                    ? $"-preset {preset} became -cpu-used {cpuUsed}."
                    : $"-preset {preset} became -cpu-used {cpuUsed}, the nearest value libaom-av1 accepts.");
                continue;
            }

            if (IsSvtAv1Params(argument))
            {
                notes.Add($"\"{argument.Text}\" was dropped: those settings belong to SVT-AV1 and libaom-av1 does not accept them.");
                continue;
            }

            result.Add(argument);

            //libaom-av1 only treats -crf as a constant quality when the bit rate is pinned to zero.
            if (IsConstantRateFactor(argument) && !hasVideoBitrate)
            {
                result.Add(new CustomArgument("-b:v 0"));
                notes.Add("-b:v 0 was added after -crf, which libaom-av1 needs for the rate factor to set the quality.");
            }
        }

        return result;
    }

    private static bool IsSvtAv1Codec(IArgument argument)
    {
        return argument switch
        {
            VideoCodecArgument codecArgument => codecArgument.Codec == SvtAv1EncoderName,
            CustomArgument custom => TryMatch(CustomVideoCodecRegex, custom, out var match) && match.Groups[2].Value == SvtAv1EncoderName,
            _ => false
        };
    }

    private static bool TryGetNumericPreset(IArgument argument, out int preset)
    {
        switch (argument)
        {
            case NumericSpeedPresetArgument numeric:
                preset = numeric.Preset;
                return true;
            case CustomArgument custom when TryMatch(CustomNumericPresetRegex, custom, out var match):
                return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out preset);
            default:
                preset = 0;
                return false;
        }
    }

    private static bool IsSvtAv1Params(IArgument argument)
    {
        return argument switch
        {
            EncoderParamsArgument encoderParams => encoderParams.OptionName == "svtav1-params",
            CustomArgument custom => TryMatch(CustomSvtAv1ParamsRegex, custom, out _),
            _ => false
        };
    }

    private static bool IsConstantRateFactor(IArgument argument)
    {
        return argument is ConstantRateFactorArgument ||
               (argument is CustomArgument custom && TryMatch(CustomConstantRateFactorRegex, custom, out _));
    }

    private static bool IsVideoBitrate(IArgument argument)
    {
        return argument is VideoBitrateArgument ||
               (argument is CustomArgument custom && TryMatch(CustomVideoBitrateRegex, custom, out _));
    }

    private static bool TryMatch(Regex regex, CustomArgument custom, out Match match)
    {
        match = regex.Match(custom.Argument ?? string.Empty);
        return match.Success;
    }

    private readonly record struct Segment(int Start, int Length);

    /// <summary>What <see cref="Apply" /> did.</summary>
    internal sealed class Outcome
    {
        internal static readonly Outcome NotNeeded = new(false, null, Array.Empty<string>());

        private Outcome(bool applied, string problem, IReadOnlyList<string> notes)
        {
            Applied = applied;
            Problem = problem;
            Notes = notes;
        }

        /// <summary>True when at least one output was rewritten for libaom-av1.</summary>
        internal bool Applied { get; }

        /// <summary>Why the fallback was needed but could not be used; null otherwise.</summary>
        internal string Problem { get; }

        /// <summary>One sentence per change made; empty when nothing was changed.</summary>
        internal IReadOnlyList<string> Notes { get; }

        internal static Outcome Unavailable(string problem)
        {
            return new Outcome(false, problem, Array.Empty<string>());
        }

        internal static Outcome AppliedWith(IReadOnlyList<string> notes)
        {
            return new Outcome(true, null, notes);
        }
    }
}
