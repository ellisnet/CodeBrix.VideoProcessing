using System;
using CodeBrix.VideoProcessing.Core.Media;

namespace CodeBrix.VideoProcessing.Core.Frames;

/// <summary>What an <see cref="IVideoFrameSource" /> delivers, and what it was made from.</summary>
public sealed class VideoSourceInfo
{
    /// <summary>The width of the delivered frames: the stored (coded) width, not rotated.</summary>
    public int Width { get; init; }

    /// <summary>The height of the delivered frames: the stored (coded) height, not rotated.</summary>
    public int Height { get; init; }

    /// <summary>The CONSTANT frame rate of the delivered frames; their timestamps are derived from it.</summary>
    public FrameRate FrameRate { get; init; }

    /// <summary>The duration of the source stream; <see cref="TimeSpan.Zero" /> when not known.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>The expected number of frames from the start of the stream (from the container, or duration x rate); 0 when not known.</summary>
    public long EstimatedFrameCount { get; init; }

    /// <summary>The layout of the delivered frames.</summary>
    public VideoPixelLayout PixelLayout { get; init; }

    /// <summary>
    /// Bits per sample of the delivered frames: 8, 10 or 12, as asked for by <see cref="VideoSourceOptions.BitDepth" />
    /// (RGBA is always 8). Equals <see cref="VideoFrameData.BitDepth" /> of every frame the source delivers.
    /// </summary>
    public int BitDepth { get; init; } = 8;

    /// <summary>
    /// Bits per sample of the SOURCE (0 when not known). Greater than <see cref="BitDepth" /> means the engine
    /// converted the pictures down - for example a 10-bit HDR source delivered as 8-bit. Less than
    /// <see cref="BitDepth" /> means the samples were widened (an 8-bit source delivered as 10-bit adds no detail).
    /// </summary>
    public int SourceBitDepth { get; init; }

    /// <summary>True when <see cref="SourceBitDepth" /> is greater than <see cref="BitDepth" />.</summary>
    public bool IsBitDepthReduced => SourceBitDepth > BitDepth;

    /// <summary>True when the source carries an alpha channel (whether or not it is delivered).</summary>
    public bool SourceHasAlpha { get; init; }

    /// <summary>True when the delivered frames carry alpha (an <see cref="VideoFrameData.A" /> plane, or RGBA).</summary>
    public bool HasAlpha { get; init; }

    /// <summary>The CLOCKWISE rotation (0, 90, 180, 270) a player applies to the delivered frames for display.</summary>
    public int RotationDegrees { get; init; }

    /// <summary>The sample range of the delivered YUV and Gray frames (the alpha plane is always full range).</summary>
    public VideoColorRange ColorRange { get; init; }

    /// <summary>The YUV-to-RGB matrix the delivered YUV frames should be interpreted with (not meaningful for Gray).</summary>
    public VideoColorMatrix ColorMatrix { get; init; }
}
