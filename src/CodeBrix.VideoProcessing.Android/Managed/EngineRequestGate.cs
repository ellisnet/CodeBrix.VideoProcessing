using System;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Output;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// The engine's argument and capability checks, kept free of Android types so the host tests can drive every
/// refusal: each method validates the request with the contract's own Validate() and then calls
/// <see cref="VideoProcessorExtensions.RequireCapability" /> for every optional feature the request touches.
/// </summary>
internal static class EngineRequestGate
{
    /// <summary>Checks a path argument the way the contract asks (null or empty is an ArgumentException).</summary>
    internal static void CheckPath(string path, string name)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("The path is required.", name);
    }

    /// <summary>Validates OpenVideo options (null means the defaults) and checks every capability they need.</summary>
    internal static VideoSourceOptions CheckSourceOptions(IVideoProcessor processor, VideoSourceOptions options)
    {
        options ??= new VideoSourceOptions();
        options.Validate();
        if (options.PixelLayout == VideoPixelLayout.Nv12) processor.RequireCapability(VideoProcessorCapabilities.Nv12Input, "NV12 frames");
        if (options.PixelLayout == VideoPixelLayout.Rgba) processor.RequireCapability(VideoProcessorCapabilities.RgbaInput, "RGBA frames");
        if (options.PixelLayout == VideoPixelLayout.I444) processor.RequireCapability(VideoProcessorCapabilities.I444FrameSource, "I444 frames");
        if (options.PixelLayout == VideoPixelLayout.Gray) processor.RequireCapability(VideoProcessorCapabilities.GrayFrameSource, "Gray frames");
        if (options.BitDepth > 8)
        {
            processor.RequireCapability(VideoProcessorCapabilities.HighBitDepthFrameSource, $"{options.BitDepth}-bit frames");
            HighBitDepthConversion.CheckRequestedDepth(options.BitDepth);
        }

        if (options.IncludeAlpha && options.PixelLayout != VideoPixelLayout.Rgba)
            processor.RequireCapability(VideoProcessorCapabilities.AlphaFrameSource, "alpha planes (VideoSourceOptions.IncludeAlpha)");
        return options;
    }

    /// <summary>Validates OpenAudio options (null means the defaults).</summary>
    internal static AudioSourceOptions CheckAudioOptions(AudioSourceOptions options)
    {
        options ??= new AudioSourceOptions();
        options.Validate();
        return options;
    }

    /// <summary>Validates a CreateWriter request and checks the capabilities its profile and input layout need.</summary>
    internal static void CheckWriterRequest(IVideoProcessor processor, StandardVideoRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.Validate();
        CheckProfile(processor, request.Profile);
        if (request.InputPixelLayout == VideoPixelLayout.Nv12) processor.RequireCapability(VideoProcessorCapabilities.Nv12Input, "NV12 input frames");
        if (request.InputPixelLayout == VideoPixelLayout.Rgba) processor.RequireCapability(VideoProcessorCapabilities.RgbaInput, "RGBA input frames");
    }

    /// <summary>Checks the HEVC capability for an HEVC profile.</summary>
    internal static void CheckProfile(IVideoProcessor processor, StandardVideoProfile profile)
    {
        if (profile == StandardVideoProfile.Mp4HevcAac) processor.RequireCapability(VideoProcessorCapabilities.HevcOutput, "HEVC output");
    }

    /// <summary>Checks a TranscodeFile call.</summary>
    internal static void CheckTranscode(IVideoProcessor processor, TranscodeRequest request)
    {
        processor.RequireCapability(VideoProcessorCapabilities.DirectTranscode, "TranscodeFile");
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.Validate();
        CheckProfile(processor, request.Output.Profile);
    }

    /// <summary>Checks a TrimFile call.</summary>
    internal static void CheckTrim(IVideoProcessor processor, TrimRequest request)
    {
        processor.RequireCapability(VideoProcessorCapabilities.DirectTrim, "TrimFile");
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.Validate();
    }

    /// <summary>Checks a ConcatenateFiles call.</summary>
    internal static void CheckConcatenate(IVideoProcessor processor, ConcatenateRequest request)
    {
        processor.RequireCapability(VideoProcessorCapabilities.DirectConcatenate, "ConcatenateFiles");
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.Validate();
    }

    /// <summary>Checks a Snapshot call.</summary>
    internal static void CheckSnapshot(IVideoProcessor processor, string path, TimeSpan at)
    {
        processor.RequireCapability(VideoProcessorCapabilities.Snapshot, "Snapshot");
        CheckPath(path, nameof(path));
        if (at < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(at), at, "The position cannot be negative.");
    }

    /// <summary>Checks one frame handed to a writer: its size, layout and bit depth must match the request.</summary>
    internal static void CheckWriterFrame(StandardVideoRequest request, in VideoFrameData frame)
    {
        if (frame.IsEmpty) throw new ArgumentException("The frame is empty.", nameof(frame));
        if (frame.Width != request.Width || frame.Height != request.Height)
            throw new ArgumentException($"The frame is {frame.Width}x{frame.Height}; this writer was created for {request.Width}x{request.Height}.", nameof(frame));
        if (frame.Layout != request.InputPixelLayout)
            throw new ArgumentException($"The frame is {frame.Layout}; this writer was created for {request.InputPixelLayout} frames.", nameof(frame));
        if (frame.BitDepth != 8)
            throw new ArgumentException($"The frame is {frame.BitDepth}-bit; the standard video writer takes 8-bit frames only.", nameof(frame));
    }
}
