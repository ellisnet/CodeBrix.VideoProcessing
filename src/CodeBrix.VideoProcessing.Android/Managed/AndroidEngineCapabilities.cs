using CodeBrix.VideoProcessing.Core;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>The Android engine's capability flags, as a pure function of what the device's codec list offers.</summary>
/// <remarks>
/// Always set: RgbaInput, Nv12Input (managed conversions both ways), GrayFrameSource (the Y plane of the decoded 4:2:0
/// picture), Snapshot, DirectTranscode, DirectTrim, DirectConcatenate. HevcOutput only when the device has a
/// "video/hevc" ENCODER. Never set: AlphaFrameSource (Android's decoders expose no alpha), I444FrameSource (the
/// decoders deliver 4:2:0) and HighBitDepthFrameSource (10-bit output is device-dependent P010; a future addition).
/// </remarks>
internal static class AndroidEngineCapabilities
{
    /// <summary>The flags every Android device gets.</summary>
    internal const VideoProcessorCapabilities Always =
        VideoProcessorCapabilities.RgbaInput |
        VideoProcessorCapabilities.Nv12Input |
        VideoProcessorCapabilities.GrayFrameSource |
        VideoProcessorCapabilities.Snapshot |
        VideoProcessorCapabilities.DirectTranscode |
        VideoProcessorCapabilities.DirectTrim |
        VideoProcessorCapabilities.DirectConcatenate;

    /// <summary>The flags for a device.</summary>
    /// <param name="hasHevcEncoder">True when MediaCodecList has a video/hevc encoder.</param>
    internal static VideoProcessorCapabilities Compute(bool hasHevcEncoder) =>
        hasHevcEncoder ? Always | VideoProcessorCapabilities.HevcOutput : Always;
}
