namespace CodeBrix.VideoProcessing.Core.Media;

/// <summary>How a stored video stream samples colour - a coarse description of its pixel format.</summary>
public enum VideoChromaSubsampling
{
    /// <summary>Not known.</summary>
    Unknown = 0,

    /// <summary>YUV 4:2:0 - chroma at half width and half height. Almost all distributed video.</summary>
    Yuv420 = 1,

    /// <summary>YUV 4:2:2 - chroma at half width, full height.</summary>
    Yuv422 = 2,

    /// <summary>YUV 4:4:4 - chroma at full resolution.</summary>
    Yuv444 = 3,

    /// <summary>Luma only (greyscale).</summary>
    Monochrome = 4,

    /// <summary>RGB samples rather than YUV.</summary>
    Rgb = 5,

    /// <summary>A layout this contract does not name (for example 4:1:1 or a palette).</summary>
    Other = 6
}
