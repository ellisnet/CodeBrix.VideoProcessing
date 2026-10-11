using System;
using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// Decides whether a set of files can be joined by COPYING their coded samples (no re-encode) - the rule for
/// ConcatenateFiles' fast path. Every input must have the same kinds of stream (a video track or not, an audio track
/// or not) and, stream by stream:
/// <list type="bullet">
///   <item>video: the same MIME type, width and height, and byte-identical codec-specific data (csd-0 and csd-1 - for
///   H.264 the SPS and PPS, for HEVC the VPS/SPS/PPS), which pins profile, level, chroma format and bit depth;</item>
///   <item>audio: the same MIME type, sample rate and channel count, and byte-identical csd-0 (for AAC the
///   AudioSpecificConfig).</item>
/// </list>
/// Anything else is joined by decoding and re-encoding instead.
/// </summary>
internal static class ConcatenationRule
{
    /// <summary>Returns true when every input matches the first one; otherwise false and why.</summary>
    /// <param name="inputs">Per input: its first video track (or null) and its first audio track (or null).</param>
    /// <param name="reason">Why the inputs cannot be copied, or null.</param>
    internal static bool CanCopy(IReadOnlyList<(TrackDescription Video, TrackDescription Audio)> inputs, out string reason)
    {
        reason = null;
        if (inputs == null || inputs.Count == 0)
        {
            reason = "There are no inputs.";
            return false;
        }

        (TrackDescription firstVideo, TrackDescription firstAudio) = inputs[0];
        if (firstVideo == null && firstAudio == null)
        {
            reason = "Input 0 has neither a video nor an audio track.";
            return false;
        }

        for (int i = 1; i < inputs.Count; i++)
        {
            (TrackDescription video, TrackDescription audio) = inputs[i];
            if ((video == null) != (firstVideo == null))
            {
                reason = $"Input {i} {(video == null ? "has no" : "has a")} video track and input 0 {(firstVideo == null ? "has none" : "has one")}.";
                return false;
            }

            if ((audio == null) != (firstAudio == null))
            {
                reason = $"Input {i} {(audio == null ? "has no" : "has an")} audio track and input 0 {(firstAudio == null ? "has none" : "has one")}.";
                return false;
            }

            if (video != null && !SameVideo(firstVideo, video, out string why))
            {
                reason = $"The video of input {i} differs from input 0: {why}.";
                return false;
            }

            if (audio != null && !SameAudio(firstAudio, audio, out string whyAudio))
            {
                reason = $"The audio of input {i} differs from input 0: {whyAudio}.";
                return false;
            }
        }

        return true;
    }

    private static bool SameVideo(TrackDescription a, TrackDescription b, out string why)
    {
        why = null;
        if (!string.Equals(a.Mime, b.Mime, StringComparison.OrdinalIgnoreCase)) { why = $"codec {a.Mime} vs {b.Mime}"; return false; }
        if (a.Width != b.Width || a.Height != b.Height) { why = $"size {a.Width}x{a.Height} vs {b.Width}x{b.Height}"; return false; }
        if (!SameBytes(a.Csd0, b.Csd0) || !SameBytes(a.Csd1, b.Csd1)) { why = "different codec configuration (csd-0 / csd-1)"; return false; }
        return true;
    }

    private static bool SameAudio(TrackDescription a, TrackDescription b, out string why)
    {
        why = null;
        if (!string.Equals(a.Mime, b.Mime, StringComparison.OrdinalIgnoreCase)) { why = $"codec {a.Mime} vs {b.Mime}"; return false; }
        if (a.SampleRate != b.SampleRate) { why = $"sample rate {a.SampleRate} vs {b.SampleRate}"; return false; }
        if (a.ChannelCount != b.ChannelCount) { why = $"channels {a.ChannelCount} vs {b.ChannelCount}"; return false; }
        if (!SameBytes(a.Csd0, b.Csd0)) { why = "different codec configuration (csd-0)"; return false; }
        return true;
    }

    private static bool SameBytes(byte[] a, byte[] b)
    {
        if (a == null || a.Length == 0) return b == null || b.Length == 0;
        if (b == null) return false;
        return a.AsSpan().SequenceEqual(b);
    }
}
