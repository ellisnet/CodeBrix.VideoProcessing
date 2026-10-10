namespace CodeBrix.VideoProcessing.Core.Encoding;

/// <summary>
/// The codec ids the encoder registry (<see cref="VideoEncoders" />) is keyed by. Ids are short, lower-case strings;
/// the registry compares them ignoring case.
/// </summary>
public static class VideoCodecIds
{
    /// <summary>AV1 (AOMedia Video 1): "av1".</summary>
    public const string Av1 = "av1";

    /// <summary>H.264 / AVC: "h264".</summary>
    public const string H264 = "h264";

    /// <summary>H.265 / HEVC: "hevc".</summary>
    public const string Hevc = "hevc";
}
