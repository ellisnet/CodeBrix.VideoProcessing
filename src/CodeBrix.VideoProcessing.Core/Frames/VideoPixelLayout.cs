namespace CodeBrix.VideoProcessing.Core.Frames;

/// <summary>How the samples of a <see cref="VideoFrameData" /> are laid out in memory.</summary>
/// <remarks>
/// SAMPLE SIZE. The YUV and Gray layouts carry 8, 10 or 12 bits per sample (<see cref="VideoFrameData.BitDepth" />).
/// An 8-bit sample is one byte. A 10- or 12-bit sample is an unsigned 16-bit LITTLE-ENDIAN value with the sample in
/// the LOW bits (0..1023 or 0..4095) - the libaom / dav1d convention, not the most-significant-bit-aligned P010
/// convention. <see cref="Rgba" /> is always 8 bits. Plane row lengths are always counted in BYTES.
/// </remarks>
public enum VideoPixelLayout
{
    /// <summary>
    /// Planar YUV 4:2:0 - three planes: <see cref="VideoFrameData.Y" /> at full size, then
    /// <see cref="VideoFrameData.U" /> (Cb) and <see cref="VideoFrameData.V" /> (Cr) at half width and half height
    /// (rounded up). The layout every engine supports (at 8 bits).
    /// </summary>
    I420 = 0,

    /// <summary>
    /// Semi-planar YUV 4:2:0 - <see cref="VideoFrameData.Y" /> at full size, then ONE interleaved
    /// <see cref="VideoFrameData.UV" /> plane (Cb, Cr, Cb, Cr, ...) at half height. Optional: needs
    /// <see cref="VideoProcessorCapabilities.Nv12Input" />.
    /// </summary>
    Nv12 = 1,

    /// <summary>
    /// Packed 8-bit RGBA - ONE <see cref="VideoFrameData.Rgba" /> plane, four bytes per pixel in R, G, B, A order,
    /// straight (not premultiplied) alpha. Always 8 bits. Optional: needs
    /// <see cref="VideoProcessorCapabilities.RgbaInput" />.
    /// </summary>
    Rgba = 2,

    /// <summary>
    /// Planar YUV 4:4:4 - three planes, <see cref="VideoFrameData.Y" />, <see cref="VideoFrameData.U" /> (Cb) and
    /// <see cref="VideoFrameData.V" /> (Cr), ALL at full size. Optional for frame sources: needs
    /// <see cref="VideoProcessorCapabilities.I444FrameSource" />. The standard MP4 writer does not accept it.
    /// </summary>
    I444 = 3,

    /// <summary>
    /// Luma only (monochrome) - ONE <see cref="VideoFrameData.Y" /> plane at full size and no chroma. Optional for
    /// frame sources: needs <see cref="VideoProcessorCapabilities.GrayFrameSource" />. The standard MP4 writer does
    /// not accept it.
    /// </summary>
    Gray = 4
}
