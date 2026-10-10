using System;
using CodeBrix.VideoProcessing.Core.Frames;

namespace CodeBrix.VideoProcessing.Core.Encoding;

/// <summary>What an encoder (<see cref="IVideoEncoderFactory.Capabilities" />) accepts and can do.</summary>
/// <remarks>
/// A layout and a depth are each checked on their own: an encoder that lists I420 and I444 and depths 8 and 10
/// accepts all four combinations (RGBA is 8-bit only whatever the depths say). Ask before you build a request, or
/// let <see cref="IVideoEncoderFactory.Create" /> refuse it - it calls <see cref="ThrowIfUnsupported" />.
/// </remarks>
public sealed class VideoEncoderCapabilities
{
    /// <summary>The frame layouts <see cref="IVideoEncoder.Encode" /> accepts.</summary>
    public VideoEncoderPixelLayouts PixelLayouts { get; init; }

    /// <summary>The sample bit depths <see cref="IVideoEncoder.Encode" /> accepts.</summary>
    public VideoEncoderBitDepths BitDepths { get; init; }

    /// <summary>True when the encoder can encode mathematically lossless (<see cref="VideoEncoderRequest.Lossless" />).</summary>
    public bool SupportsLossless { get; init; }

    /// <summary>
    /// True when the encoder honours the forceKeyFrame argument of <see cref="IVideoEncoder.Encode" />; when false,
    /// asking for a forced keyframe throws <see cref="NotSupportedException" />.
    /// </summary>
    public bool SupportsForcedKeyFrames { get; init; }

    /// <summary>The flag that stands for a frame layout.</summary>
    /// <param name="layout">The layout.</param>
    /// <returns>Its flag; <see cref="VideoEncoderPixelLayouts.None" /> for an unknown value.</returns>
    public static VideoEncoderPixelLayouts ToFlag(VideoPixelLayout layout) => layout switch
    {
        VideoPixelLayout.I420 => VideoEncoderPixelLayouts.I420,
        VideoPixelLayout.Nv12 => VideoEncoderPixelLayouts.Nv12,
        VideoPixelLayout.Rgba => VideoEncoderPixelLayouts.Rgba,
        VideoPixelLayout.I444 => VideoEncoderPixelLayouts.I444,
        VideoPixelLayout.Gray => VideoEncoderPixelLayouts.Gray,
        _ => VideoEncoderPixelLayouts.None
    };

    /// <summary>The flag that stands for a bit depth.</summary>
    /// <param name="bitDepth">8, 10 or 12.</param>
    /// <returns>Its flag; <see cref="VideoEncoderBitDepths.None" /> for any other value.</returns>
    public static VideoEncoderBitDepths ToFlag(int bitDepth) => bitDepth switch
    {
        8 => VideoEncoderBitDepths.Depth8,
        10 => VideoEncoderBitDepths.Depth10,
        12 => VideoEncoderBitDepths.Depth12,
        _ => VideoEncoderBitDepths.None
    };

    /// <summary>Reports whether the encoder accepts frames in a layout.</summary>
    /// <param name="layout">The layout.</param>
    /// <returns>True when its flag is in <see cref="PixelLayouts" />.</returns>
    public bool SupportsLayout(VideoPixelLayout layout)
    {
        VideoEncoderPixelLayouts flag = ToFlag(layout);
        return flag != VideoEncoderPixelLayouts.None && (PixelLayouts & flag) == flag;
    }

    /// <summary>Reports whether the encoder accepts frames of a bit depth.</summary>
    /// <param name="bitDepth">The bits per sample.</param>
    /// <returns>True when its flag is in <see cref="BitDepths" />.</returns>
    public bool SupportsBitDepth(int bitDepth)
    {
        VideoEncoderBitDepths flag = ToFlag(bitDepth);
        return flag != VideoEncoderBitDepths.None && (BitDepths & flag) == flag;
    }

    /// <summary>
    /// Throws the standard <see cref="NotSupportedException" /> unless the encoder accepts the request's layout and
    /// bit depth, and lossless coding when the request asks for it. Encoder implementers call this in
    /// <see cref="IVideoEncoderFactory.Create" /> after <see cref="VideoEncoderRequest.Validate" />.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> is null.</exception>
    /// <exception cref="NotSupportedException">The encoder cannot do what the request asks; the message says what.</exception>
    public void ThrowIfUnsupported(VideoEncoderRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (!SupportsLayout(request.PixelLayout))
            throw new NotSupportedException($"The encoder does not accept {request.PixelLayout} frames; it accepts {PixelLayouts}.");
        if (!SupportsBitDepth(request.BitDepth))
            throw new NotSupportedException($"The encoder does not accept {request.BitDepth}-bit frames; it accepts {BitDepths}.");
        if (request.Lossless && !SupportsLossless)
            throw new NotSupportedException("The encoder cannot encode losslessly.");
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"layouts {PixelLayouts}; depths {BitDepths}; lossless {(SupportsLossless ? "yes" : "no")}; forced keyframes {(SupportsForcedKeyFrames ? "yes" : "no")}";
}
