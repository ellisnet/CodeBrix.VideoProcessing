using System;
using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Core.Media;

/// <summary>One video stream of a media file, as reported by <see cref="IVideoProcessor.Analyse" />.</summary>
/// <remarks>Values the engine cannot determine are left at zero / null / Unspecified.</remarks>
public sealed class VideoStreamInfo
{
    private readonly IReadOnlyDictionary<string, string> tags = MediaTags.Empty;

    /// <summary>The engine's index of the stream within the file (counting every stream type).</summary>
    public int Index { get; init; }

    /// <summary>The codec, in the engine's words (for example "h264", "hevc", "av1", "vp9").</summary>
    public string CodecName { get; init; }

    /// <summary>The stored (coded) width in pixels - before any display rotation.</summary>
    public int Width { get; init; }

    /// <summary>The stored (coded) height in pixels - before any display rotation.</summary>
    public int Height { get; init; }

    /// <summary>The frame rate; <see cref="FrameRate.Unspecified" /> when not known.</summary>
    public FrameRate FrameRate { get; init; }

    /// <summary>The number of frames, when the container records it; 0 when not known.</summary>
    public long FrameCount { get; init; }

    /// <summary>The duration of the stream; <see cref="TimeSpan.Zero" /> when not known.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Bits per sample of the stored pictures (8, 10, 12, ...); 0 when not known.</summary>
    public int BitDepth { get; init; }

    /// <summary>The engine's name for the stored pixel format (for example "yuv420p10le" or "yuva420p"); may be null.</summary>
    public string PixelFormatName { get; init; }

    /// <summary>How the stored pictures sample colour.</summary>
    public VideoChromaSubsampling ChromaSubsampling { get; init; }

    /// <summary>True when the stored pictures carry an alpha (transparency) channel.</summary>
    public bool HasAlpha { get; init; }

    /// <summary>
    /// The CLOCKWISE rotation, 0, 90, 180 or 270 degrees, that a player applies to the stored picture for display.
    /// </summary>
    public int RotationDegrees { get; init; }

    /// <summary>The sample range of the stored pictures.</summary>
    public VideoColorRange ColorRange { get; init; }

    /// <summary>The YUV-to-RGB matrix of the stored pictures.</summary>
    public VideoColorMatrix ColorMatrix { get; init; }

    /// <summary>The colour primaries of the stored pictures.</summary>
    public VideoColorPrimaries ColorPrimaries { get; init; }

    /// <summary>The transfer characteristics of the stored pictures.</summary>
    public VideoColorTransfer ColorTransfer { get; init; }

    /// <summary>The stream-level tags (language, handler_name, ...), case-insensitive.</summary>
    public IReadOnlyDictionary<string, string> Tags
    {
        get => tags;
        init => tags = MediaTags.Copy(value);
    }

    /// <summary>The width a viewer sees: <see cref="Width" />, or <see cref="Height" /> when rotated by 90 or 270 degrees.</summary>
    public int DisplayWidth => RotationDegrees == 90 || RotationDegrees == 270 ? Height : Width;

    /// <summary>The height a viewer sees: <see cref="Height" />, or <see cref="Width" /> when rotated by 90 or 270 degrees.</summary>
    public int DisplayHeight => RotationDegrees == 90 || RotationDegrees == 270 ? Width : Height;
}
