using System;

namespace CodeBrix.VideoProcessing.Core.Encoding;

/// <summary>
/// The frame layouts an encoder accepts (<see cref="VideoEncoderCapabilities.PixelLayouts" />): one flag per
/// <see cref="Frames.VideoPixelLayout" />.
/// </summary>
[Flags]
public enum VideoEncoderPixelLayouts
{
    /// <summary>No layout.</summary>
    None = 0,

    /// <summary><see cref="Frames.VideoPixelLayout.I420" /> - planar YUV 4:2:0.</summary>
    I420 = 1 << 0,

    /// <summary><see cref="Frames.VideoPixelLayout.Nv12" /> - semi-planar YUV 4:2:0.</summary>
    Nv12 = 1 << 1,

    /// <summary><see cref="Frames.VideoPixelLayout.Rgba" /> - packed 8-bit RGBA.</summary>
    Rgba = 1 << 2,

    /// <summary><see cref="Frames.VideoPixelLayout.I444" /> - planar YUV 4:4:4.</summary>
    I444 = 1 << 3,

    /// <summary><see cref="Frames.VideoPixelLayout.Gray" /> - luma only (a monochrome stream).</summary>
    Gray = 1 << 4
}
