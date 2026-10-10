namespace CodeBrix.VideoProcessing.Core.Media;

/// <summary>The matrix that converts between YUV samples and RGB (often called the "colour space").</summary>
public enum VideoColorMatrix
{
    /// <summary>Not known or not tagged. By convention, BT.601 for standard definition and BT.709 for HD.</summary>
    Unspecified = 0,

    /// <summary>ITU-R BT.601 (ffmpeg "bt470bg" / "smpte170m").</summary>
    Bt601 = 1,

    /// <summary>ITU-R BT.709 - HD video.</summary>
    Bt709 = 2,

    /// <summary>ITU-R BT.2020, non-constant luminance - UHD and HDR video.</summary>
    Bt2020NonConstant = 3,

    /// <summary>ITU-R BT.2020, constant luminance.</summary>
    Bt2020Constant = 4,

    /// <summary>No matrix: the samples are already RGB (ffmpeg "gbr" / "rgb").</summary>
    Rgb = 5,

    /// <summary>A matrix this contract does not name.</summary>
    Other = 6
}
