using CodeBrix.VideoProcessing.Core.Media;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// One track of a media file as the platform reports it (an android.media.MediaFormat from MediaExtractor), copied
/// into plain values - plus what the managed MP4 reader found for it. The engine fills it on the device; the mapping
/// to the Core contract (<see cref="MediaFormatMapping" />) works on it anywhere, which is what lets the host tests
/// exercise the mapping without a device.
/// </summary>
/// <remarks>Integer values the format did not carry are -1; strings are null; durations 0.</remarks>
internal sealed class TrackDescription
{
    /// <summary>The MediaExtractor track index.</summary>
    public int Index { get; set; }

    /// <summary>The MIME type ("video/avc", "audio/mp4a-latm", ...).</summary>
    public string Mime { get; set; }

    /// <summary>KEY_WIDTH, or -1.</summary>
    public int Width { get; set; } = -1;

    /// <summary>KEY_HEIGHT, or -1.</summary>
    public int Height { get; set; } = -1;

    /// <summary>KEY_DURATION in microseconds, or 0.</summary>
    public long DurationMicroseconds { get; set; }

    /// <summary>KEY_FRAME_RATE (an integer or a float in the format), or 0.</summary>
    public double FrameRateValue { get; set; }

    /// <summary>KEY_ROTATION (clockwise degrees), or -1.</summary>
    public int Rotation { get; set; } = -1;

    /// <summary>KEY_COLOR_RANGE, or -1.</summary>
    public int ColorRange { get; set; } = -1;

    /// <summary>KEY_COLOR_STANDARD, or -1.</summary>
    public int ColorStandard { get; set; } = -1;

    /// <summary>KEY_COLOR_TRANSFER, or -1.</summary>
    public int ColorTransfer { get; set; } = -1;

    /// <summary>KEY_PROFILE, or -1.</summary>
    public int Profile { get; set; } = -1;

    /// <summary>KEY_SAMPLE_RATE, or -1.</summary>
    public int SampleRate { get; set; } = -1;

    /// <summary>KEY_CHANNEL_COUNT, or -1.</summary>
    public int ChannelCount { get; set; } = -1;

    /// <summary>KEY_PCM_ENCODING (raw audio only), or -1.</summary>
    public int PcmEncoding { get; set; } = -1;

    /// <summary>KEY_LANGUAGE, or null.</summary>
    public string Language { get; set; }

    /// <summary>The codec-specific data buffers "csd-0" / "csd-1", or null.</summary>
    public byte[] Csd0 { get; set; }

    /// <summary>The second codec-specific data buffer, or null.</summary>
    public byte[] Csd1 { get; set; }

    /// <summary>The exact frame rate the managed MP4 reader derived from the track's sample table, or unspecified.</summary>
    public FrameRate ContainerFrameRate { get; set; }

    /// <summary>The sample (frame) count the managed MP4 reader found in the track's sample table, or 0.</summary>
    public long ContainerSampleCount { get; set; }

    /// <summary>True for a video track (the MIME starts with "video/").</summary>
    public bool IsVideo => Mime != null && Mime.StartsWith("video/", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>True for an audio track (the MIME starts with "audio/").</summary>
    public bool IsAudio => Mime != null && Mime.StartsWith("audio/", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True for a subtitle (timed text) track: the MIME starts with "text/" ("text/3gpp-tt", "text/vtt",
    /// "text/cea-608", ...) or is one of the "application/" subtitle types ("application/x-subrip",
    /// "application/ttml+xml"; see <see cref="SubtitleApplicationMimes" />).
    /// </summary>
    public bool IsSubtitle =>
        Mime != null &&
        (Mime.StartsWith("text/", System.StringComparison.OrdinalIgnoreCase) ||
         System.Array.Exists(SubtitleApplicationMimes, m => string.Equals(m, Mime, System.StringComparison.OrdinalIgnoreCase)));

    /// <summary>The subtitle MIME types that do not start with "text/".</summary>
    internal static readonly string[] SubtitleApplicationMimes =
    {
        "application/x-subrip",
        "application/ttml+xml",
    };
}
