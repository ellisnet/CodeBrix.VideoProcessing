namespace CodeBrix.VideoProcessing.Core.Media;

/// <summary>The transfer characteristics (the "gamma" curve) of the samples.</summary>
public enum VideoColorTransfer
{
    /// <summary>Not known or not tagged.</summary>
    Unspecified = 0,

    /// <summary>ITU-R BT.709 (also used, unchanged, by BT.601 "smpte170m" and BT.2020 SDR).</summary>
    Bt709 = 1,

    /// <summary>The sRGB curve (ffmpeg "iec61966-2-1").</summary>
    Srgb = 2,

    /// <summary>Linear light.</summary>
    Linear = 3,

    /// <summary>SMPTE ST 2084 "PQ" - HDR10.</summary>
    Pq = 4,

    /// <summary>ARIB STD-B67 "HLG" - hybrid log-gamma HDR.</summary>
    Hlg = 5,

    /// <summary>A transfer curve this contract does not name.</summary>
    Other = 6
}
