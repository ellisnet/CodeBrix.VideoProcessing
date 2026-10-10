using System;

namespace CodeBrix.VideoProcessing.Core.Frames;

/// <summary>What <see cref="IVideoProcessor.OpenVideo" /> should deliver.</summary>
public sealed class VideoSourceOptions
{
    /// <summary>
    /// The layout of the delivered frames. <see cref="VideoPixelLayout.I420" /> (the default) is always available;
    /// NV12 needs <see cref="VideoProcessorCapabilities.Nv12Input" />, RGBA needs
    /// <see cref="VideoProcessorCapabilities.RgbaInput" />, I444 needs
    /// <see cref="VideoProcessorCapabilities.I444FrameSource" /> and Gray needs
    /// <see cref="VideoProcessorCapabilities.GrayFrameSource" />.
    /// </summary>
    public VideoPixelLayout PixelLayout { get; set; } = VideoPixelLayout.I420;

    /// <summary>
    /// The bits per sample of the delivered frames: 8 (the default), 10 or 12. Deeper than 8 needs
    /// <see cref="VideoProcessorCapabilities.HighBitDepthFrameSource" /> and is not allowed with
    /// <see cref="VideoPixelLayout.Rgba" />, which is always 8 bits. The engine converts the source to this depth
    /// either way (<see cref="VideoSourceInfo.SourceBitDepth" /> reports what the source had).
    /// </summary>
    public int BitDepth { get; set; } = 8;

    /// <summary>
    /// Deliver the source's alpha channel as the <see cref="VideoFrameData.A" /> plane (at the frame's bit depth) when
    /// it has one. Needs <see cref="VideoProcessorCapabilities.AlphaFrameSource" />; ignored for RGBA, which always
    /// carries alpha. Default false.
    /// </summary>
    public bool IncludeAlpha { get; set; }

    /// <summary>Where to start: the first frame delivered is the first that starts at or after this time. Default zero.</summary>
    public TimeSpan StartTime { get; set; } = TimeSpan.Zero;

    /// <summary>Checks the options.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A value is out of range: an unknown layout, a bit depth other than 8, 10 or 12, a bit depth above 8 with
    /// RGBA, or a negative start time.
    /// </exception>
    public void Validate()
    {
        if (!Enum.IsDefined(PixelLayout)) throw new ArgumentOutOfRangeException(nameof(PixelLayout), PixelLayout, "Unknown pixel layout.");
        if (BitDepth != 8 && BitDepth != 10 && BitDepth != 12)
            throw new ArgumentOutOfRangeException(nameof(BitDepth), BitDepth, "The bit depth must be 8, 10 or 12.");
        if (BitDepth > 8 && PixelLayout == VideoPixelLayout.Rgba)
            throw new ArgumentOutOfRangeException(nameof(BitDepth), BitDepth, "RGBA frames are always 8 bits; ask for a YUV or Gray layout to get deeper samples.");
        if (StartTime < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(StartTime), StartTime, "The start time cannot be negative.");
    }
}
