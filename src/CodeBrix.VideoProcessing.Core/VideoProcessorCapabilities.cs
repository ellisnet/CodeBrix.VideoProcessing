using System;

namespace CodeBrix.VideoProcessing.Core;

/// <summary>
/// What a video processor engine can do beyond the mandatory core of the contract.
/// </summary>
/// <remarks>
/// <para>
/// Every engine MUST support <see cref="IVideoProcessor.Analyse" />, <see cref="IVideoProcessor.OpenVideo" />
/// delivering 8-bit <see cref="Frames.VideoPixelLayout.I420" /> frames, <see cref="IVideoProcessor.OpenAudio" />,
/// and <see cref="IVideoProcessor.CreateWriter" /> for <see cref="Output.StandardVideoProfile.Mp4H264Aac" /> fed
/// with 8-bit I420 frames. Everything else is optional and announced here; an operation whose flag is missing
/// throws <see cref="NotSupportedException" />.
/// </para>
/// <para>
/// Ask before you call: <c>processor.Supports(VideoProcessorCapabilities.DirectTrim)</c> (see
/// <see cref="VideoProcessorExtensions.Supports" />).
/// </para>
/// </remarks>
[Flags]
public enum VideoProcessorCapabilities
{
    /// <summary>Only the mandatory core of the contract.</summary>
    None = 0,

    /// <summary>
    /// <see cref="IVideoProcessor.OpenVideo" /> can deliver an alpha plane (<see cref="Frames.VideoFrameData.A" />)
    /// when <see cref="Frames.VideoSourceOptions.IncludeAlpha" /> is set and the source carries alpha.
    /// </summary>
    AlphaFrameSource = 1 << 0,

    /// <summary>
    /// <see cref="IVideoProcessor.CreateWriter" /> accepts <see cref="Frames.VideoPixelLayout.Nv12" /> frames, and
    /// <see cref="IVideoProcessor.OpenVideo" /> can deliver them.
    /// </summary>
    Nv12Input = 1 << 1,

    /// <summary>
    /// <see cref="IVideoProcessor.CreateWriter" /> accepts <see cref="Frames.VideoPixelLayout.Rgba" /> frames, and
    /// <see cref="IVideoProcessor.OpenVideo" /> can deliver them.
    /// </summary>
    RgbaInput = 1 << 2,

    /// <summary>
    /// <see cref="IVideoProcessor.CreateWriter" /> and <see cref="IVideoProcessor.TranscodeFile" /> can produce
    /// <see cref="Output.StandardVideoProfile.Mp4HevcAac" />.
    /// </summary>
    HevcOutput = 1 << 3,

    /// <summary><see cref="IVideoProcessor.TranscodeFile" /> is available.</summary>
    DirectTranscode = 1 << 4,

    /// <summary><see cref="IVideoProcessor.TrimFile" /> is available.</summary>
    DirectTrim = 1 << 5,

    /// <summary><see cref="IVideoProcessor.ConcatenateFiles" /> is available.</summary>
    DirectConcatenate = 1 << 6,

    /// <summary><see cref="IVideoProcessor.Snapshot" /> is available.</summary>
    Snapshot = 1 << 7,

    /// <summary>
    /// <see cref="IVideoProcessor.OpenVideo" /> can deliver <see cref="Frames.VideoPixelLayout.I444" /> frames from
    /// <see cref="Frames.IVideoFrameSource.TryReadFrame" />. A frame-source capability only: the standard MP4 writer
    /// never accepts I444.
    /// </summary>
    I444FrameSource = 1 << 8,

    /// <summary>
    /// <see cref="IVideoProcessor.OpenVideo" /> can deliver <see cref="Frames.VideoPixelLayout.Gray" /> (luma only)
    /// frames from <see cref="Frames.IVideoFrameSource.TryReadFrame" />. A frame-source capability only: the standard
    /// MP4 writer never accepts Gray.
    /// </summary>
    GrayFrameSource = 1 << 9,

    /// <summary>
    /// <see cref="IVideoProcessor.OpenVideo" /> can deliver 10- and 12-bit frames
    /// (<see cref="Frames.VideoSourceOptions.BitDepth" />) in its YUV and Gray layouts. A frame-source capability
    /// only: the standard MP4 writer is 8-bit. An engine may still refuse a particular layout / depth / alpha
    /// combination it cannot produce exactly, with a <see cref="NotSupportedException" /> naming it.
    /// </summary>
    HighBitDepthFrameSource = 1 << 10
}
