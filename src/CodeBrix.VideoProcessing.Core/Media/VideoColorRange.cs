namespace CodeBrix.VideoProcessing.Core.Media;

/// <summary>The range of the luma and chroma sample values of 8-bit (or deeper) YUV video.</summary>
public enum VideoColorRange
{
    /// <summary>Not known or not tagged. Treat it as <see cref="Limited" />, which is what almost all video uses.</summary>
    Unspecified = 0,

    /// <summary>"TV" / "studio" / "MPEG" range: luma 16..235, chroma 16..240 at 8 bits.</summary>
    Limited = 1,

    /// <summary>"PC" / "JPEG" range: every value 0..255 at 8 bits.</summary>
    Full = 2
}
