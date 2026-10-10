using System;

namespace CodeBrix.VideoProcessing.Core.Frames;

/// <summary>
/// One decoded (or to-be-encoded) video picture: its size, layout, bit depth, planes and position in the stream.
/// </summary>
/// <remarks>
/// <para>
/// Create one with <see cref="CreateI420" />, <see cref="CreateNv12" />, <see cref="CreateI444" />,
/// <see cref="CreateGray" /> or <see cref="CreateRgba" />; each checks that the planes have exactly the sizes the
/// layout and bit depth need.
/// </para>
/// <para>
/// BIT DEPTH. <see cref="BitDepth" /> is 8, 10 or 12 for the YUV and Gray layouts, and always 8 for
/// <see cref="VideoPixelLayout.Rgba" />. An 8-bit sample is one byte. A 10- or 12-bit sample is an unsigned 16-bit
/// LITTLE-ENDIAN value with the sample in the LOW bits (the libaom / dav1d convention) - so
/// <see cref="BytesPerSample" /> is 2 and every plane's <see cref="VideoPlane.RowLength" /> (always in BYTES) is twice
/// its width in samples.
/// </para>
/// <para>
/// PLANES BY LAYOUT. <see cref="VideoPixelLayout.I420" /> and <see cref="VideoPixelLayout.I444" />: <see cref="Y" />,
/// <see cref="U" />, <see cref="V" />. <see cref="VideoPixelLayout.Nv12" />: <see cref="Y" />, <see cref="UV" />.
/// <see cref="VideoPixelLayout.Gray" />: <see cref="Y" /> only. <see cref="VideoPixelLayout.Rgba" />:
/// <see cref="Rgba" />. A plane the layout does not have is the empty plane. The YUV and Gray layouts may also carry
/// <see cref="A" />: a full-size, FULL-range (0 = transparent, maximum = opaque), straight (not premultiplied) alpha
/// plane at the SAME bit depth as the frame (maximum 255, 1023 or 4095).
/// </para>
/// <para>
/// OWNERSHIP. A frame does not own anything by itself - it is a view over memory. A frame returned by
/// <see cref="IVideoFrameSource.TryReadFrame" /> is valid only until the next call to TryReadFrame, Seek or Dispose
/// on that source (engines reuse their buffers); call <see cref="Copy" /> to keep it longer. A frame handed to
/// <see cref="Output.IStandardVideoWriter.WriteFrame" /> or <see cref="Encoding.IVideoEncoder.Encode" /> may be reused
/// by the caller as soon as that call returns.
/// </para>
/// </remarks>
public readonly struct VideoFrameData
{
    private readonly VideoPlane plane0;
    private readonly VideoPlane plane1;
    private readonly VideoPlane plane2;
    private readonly VideoPlane alpha;
    private readonly int bitDepth;

    private VideoFrameData(
        int width,
        int height,
        VideoPixelLayout layout,
        int bitDepth,
        VideoPlane plane0,
        VideoPlane plane1,
        VideoPlane plane2,
        VideoPlane alpha,
        TimeSpan timestamp,
        long frameIndex)
    {
        Width = width;
        Height = height;
        Layout = layout;
        this.bitDepth = bitDepth;
        this.plane0 = plane0;
        this.plane1 = plane1;
        this.plane2 = plane2;
        this.alpha = alpha;
        Timestamp = timestamp;
        FrameIndex = frameIndex;
    }

    /// <summary>Creates an <see cref="VideoPixelLayout.I420" /> (planar YUV 4:2:0) frame.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="y">The luma plane: <paramref name="width" /> samples by <paramref name="height" /> rows.</param>
    /// <param name="u">The Cb plane: <c>(width + 1) / 2</c> samples by <c>(height + 1) / 2</c> rows.</param>
    /// <param name="v">The Cr plane: the same size as <paramref name="u" />.</param>
    /// <param name="timestamp">The presentation time.</param>
    /// <param name="frameIndex">The zero-based index of the frame in its stream.</param>
    /// <param name="a">An optional alpha plane, the same size as <paramref name="y" />; the empty plane for none.</param>
    /// <param name="bitDepth">Bits per sample: 8 (one byte per sample, the default), 10 or 12 (two bytes per sample, little-endian, low bits).</param>
    /// <returns>The frame.</returns>
    /// <exception cref="ArgumentException">A plane has the wrong size.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size, position or <paramref name="bitDepth" /> is out of range.</exception>
    public static VideoFrameData CreateI420(int width, int height, VideoPlane y, VideoPlane u, VideoPlane v, TimeSpan timestamp, long frameIndex, VideoPlane a = default, int bitDepth = 8)
    {
        int bytes = CheckFrame(width, height, timestamp, frameIndex, bitDepth);
        int chromaWidth = (width + 1) / 2;
        int chromaHeight = (height + 1) / 2;
        CheckPlane(y, width * bytes, height, nameof(y));
        CheckPlane(u, chromaWidth * bytes, chromaHeight, nameof(u));
        CheckPlane(v, chromaWidth * bytes, chromaHeight, nameof(v));
        if (!a.IsEmpty) CheckPlane(a, width * bytes, height, nameof(a));

        return new VideoFrameData(width, height, VideoPixelLayout.I420, bitDepth, y, u, v, a, timestamp, frameIndex);
    }

    /// <summary>Creates an <see cref="VideoPixelLayout.Nv12" /> (semi-planar YUV 4:2:0) frame.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="y">The luma plane: <paramref name="width" /> samples by <paramref name="height" /> rows.</param>
    /// <param name="uv">The interleaved chroma plane: <c>2 * ((width + 1) / 2)</c> samples by <c>(height + 1) / 2</c> rows.</param>
    /// <param name="timestamp">The presentation time.</param>
    /// <param name="frameIndex">The zero-based index of the frame in its stream.</param>
    /// <param name="a">An optional alpha plane, the same size as <paramref name="y" />; the empty plane for none.</param>
    /// <param name="bitDepth">Bits per sample: 8 (the default), 10 or 12 (two bytes per sample, little-endian, LOW bits - not P010's high bits).</param>
    /// <returns>The frame.</returns>
    /// <exception cref="ArgumentException">A plane has the wrong size.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size, position or <paramref name="bitDepth" /> is out of range.</exception>
    public static VideoFrameData CreateNv12(int width, int height, VideoPlane y, VideoPlane uv, TimeSpan timestamp, long frameIndex, VideoPlane a = default, int bitDepth = 8)
    {
        int bytes = CheckFrame(width, height, timestamp, frameIndex, bitDepth);
        CheckPlane(y, width * bytes, height, nameof(y));
        CheckPlane(uv, 2 * ((width + 1) / 2) * bytes, (height + 1) / 2, nameof(uv));
        if (!a.IsEmpty) CheckPlane(a, width * bytes, height, nameof(a));

        return new VideoFrameData(width, height, VideoPixelLayout.Nv12, bitDepth, y, uv, default, a, timestamp, frameIndex);
    }

    /// <summary>Creates an <see cref="VideoPixelLayout.I444" /> (planar YUV 4:4:4) frame.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="y">The luma plane: <paramref name="width" /> samples by <paramref name="height" /> rows.</param>
    /// <param name="u">The Cb plane: the same size as <paramref name="y" />.</param>
    /// <param name="v">The Cr plane: the same size as <paramref name="y" />.</param>
    /// <param name="timestamp">The presentation time.</param>
    /// <param name="frameIndex">The zero-based index of the frame in its stream.</param>
    /// <param name="a">An optional alpha plane, the same size as <paramref name="y" />; the empty plane for none.</param>
    /// <param name="bitDepth">Bits per sample: 8 (the default), 10 or 12 (two bytes per sample, little-endian, low bits).</param>
    /// <returns>The frame.</returns>
    /// <exception cref="ArgumentException">A plane has the wrong size.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size, position or <paramref name="bitDepth" /> is out of range.</exception>
    public static VideoFrameData CreateI444(int width, int height, VideoPlane y, VideoPlane u, VideoPlane v, TimeSpan timestamp, long frameIndex, VideoPlane a = default, int bitDepth = 8)
    {
        int bytes = CheckFrame(width, height, timestamp, frameIndex, bitDepth);
        CheckPlane(y, width * bytes, height, nameof(y));
        CheckPlane(u, width * bytes, height, nameof(u));
        CheckPlane(v, width * bytes, height, nameof(v));
        if (!a.IsEmpty) CheckPlane(a, width * bytes, height, nameof(a));

        return new VideoFrameData(width, height, VideoPixelLayout.I444, bitDepth, y, u, v, a, timestamp, frameIndex);
    }

    /// <summary>Creates a <see cref="VideoPixelLayout.Gray" /> (luma only) frame.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="y">The luma plane: <paramref name="width" /> samples by <paramref name="height" /> rows.</param>
    /// <param name="timestamp">The presentation time.</param>
    /// <param name="frameIndex">The zero-based index of the frame in its stream.</param>
    /// <param name="a">An optional alpha plane, the same size as <paramref name="y" />; the empty plane for none.</param>
    /// <param name="bitDepth">Bits per sample: 8 (the default), 10 or 12 (two bytes per sample, little-endian, low bits).</param>
    /// <returns>The frame.</returns>
    /// <exception cref="ArgumentException">A plane has the wrong size.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size, position or <paramref name="bitDepth" /> is out of range.</exception>
    public static VideoFrameData CreateGray(int width, int height, VideoPlane y, TimeSpan timestamp, long frameIndex, VideoPlane a = default, int bitDepth = 8)
    {
        int bytes = CheckFrame(width, height, timestamp, frameIndex, bitDepth);
        CheckPlane(y, width * bytes, height, nameof(y));
        if (!a.IsEmpty) CheckPlane(a, width * bytes, height, nameof(a));

        return new VideoFrameData(width, height, VideoPixelLayout.Gray, bitDepth, y, default, default, a, timestamp, frameIndex);
    }

    /// <summary>Creates an <see cref="VideoPixelLayout.Rgba" /> frame (always 8 bits per sample).</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="rgba">The packed plane: <c>4 * width</c> bytes by <paramref name="height" /> rows, straight alpha.</param>
    /// <param name="timestamp">The presentation time.</param>
    /// <param name="frameIndex">The zero-based index of the frame in its stream.</param>
    /// <returns>The frame.</returns>
    /// <exception cref="ArgumentException">The plane has the wrong size.</exception>
    public static VideoFrameData CreateRgba(int width, int height, VideoPlane rgba, TimeSpan timestamp, long frameIndex)
    {
        CheckFrame(width, height, timestamp, frameIndex, 8);
        if (width > int.MaxValue / 4) throw new ArgumentOutOfRangeException(nameof(width), width, "The width is too large for an RGBA plane.");
        CheckPlane(rgba, 4 * width, height, nameof(rgba));

        return new VideoFrameData(width, height, VideoPixelLayout.Rgba, 8, rgba, default, default, default, timestamp, frameIndex);
    }

    /// <summary>Width in pixels; 0 for the default (empty) frame.</summary>
    public int Width { get; }

    /// <summary>Height in pixels; 0 for the default (empty) frame.</summary>
    public int Height { get; }

    /// <summary>How the samples are laid out.</summary>
    public VideoPixelLayout Layout { get; }

    /// <summary>Bits per sample: 8, 10 or 12 (always 8 for RGBA); 0 for the default (empty) frame.</summary>
    public int BitDepth => IsEmpty ? 0 : bitDepth;

    /// <summary>
    /// Bytes per sample: 1 at 8 bits, 2 at 10 or 12 bits (unsigned 16-bit little-endian, value in the low bits);
    /// 0 for the default (empty) frame.
    /// </summary>
    public int BytesPerSample => IsEmpty ? 0 : BytesFor(bitDepth);

    /// <summary>The presentation time of the frame.</summary>
    public TimeSpan Timestamp { get; }

    /// <summary>The zero-based index of the frame in its stream.</summary>
    public long FrameIndex { get; }

    /// <summary>True for <c>default(VideoFrameData)</c>, which holds no picture.</summary>
    public bool IsEmpty => Width == 0;

    /// <summary>The luma plane (I420, NV12, I444, Gray); empty for RGBA.</summary>
    public VideoPlane Y => Layout == VideoPixelLayout.Rgba ? default : plane0;

    /// <summary>The Cb plane (I420 and I444 only).</summary>
    public VideoPlane U => Layout == VideoPixelLayout.I420 || Layout == VideoPixelLayout.I444 ? plane1 : default;

    /// <summary>The Cr plane (I420 and I444 only).</summary>
    public VideoPlane V => Layout == VideoPixelLayout.I420 || Layout == VideoPixelLayout.I444 ? plane2 : default;

    /// <summary>The interleaved Cb/Cr plane (NV12 only).</summary>
    public VideoPlane UV => Layout == VideoPixelLayout.Nv12 ? plane1 : default;

    /// <summary>The packed R, G, B, A plane (RGBA only).</summary>
    public VideoPlane Rgba => Layout == VideoPixelLayout.Rgba ? plane0 : default;

    /// <summary>
    /// The optional alpha plane of a YUV or Gray frame: full size, full range, straight alpha, at the frame's
    /// <see cref="BitDepth" />. Empty when absent.
    /// </summary>
    public VideoPlane A => alpha;

    /// <summary>True when the frame carries transparency: an <see cref="A" /> plane, or the RGBA layout.</summary>
    public bool HasAlpha => !alpha.IsEmpty || (Layout == VideoPixelLayout.Rgba && !IsEmpty);

    /// <summary>Makes a deep copy that owns its (tightly packed) memory, so it outlives the source's buffers.</summary>
    /// <returns>The copy, with the same layout, bit depth and position; the default frame copies to the default frame.</returns>
    public VideoFrameData Copy()
    {
        if (IsEmpty) return default;

        return new VideoFrameData(
            Width,
            Height,
            Layout,
            bitDepth,
            CopyPlane(plane0),
            CopyPlane(plane1),
            CopyPlane(plane2),
            CopyPlane(alpha),
            Timestamp,
            FrameIndex);
    }

    /// <summary>Returns the same picture with a different timestamp and index.</summary>
    /// <param name="timestamp">The new presentation time.</param>
    /// <param name="frameIndex">The new zero-based frame index.</param>
    /// <returns>A frame sharing this frame's planes (and its layout and bit depth).</returns>
    /// <exception cref="InvalidOperationException">The frame is empty.</exception>
    public VideoFrameData WithPosition(TimeSpan timestamp, long frameIndex)
    {
        if (IsEmpty) throw new InvalidOperationException("The empty frame has no position.");
        CheckFrame(Width, Height, timestamp, frameIndex, bitDepth);
        return new VideoFrameData(Width, Height, Layout, bitDepth, plane0, plane1, plane2, alpha, timestamp, frameIndex);
    }

    /// <inheritdoc />
    public override string ToString() =>
        IsEmpty
            ? "(empty frame)"
            : $"{Layout} {Width}x{Height} {bitDepth}-bit{(HasAlpha ? " +alpha" : string.Empty)} #{FrameIndex} @ {Timestamp}";

    private static int BytesFor(int depth) => depth > 8 ? 2 : 1;

    private static VideoPlane CopyPlane(VideoPlane plane) =>
        plane.IsEmpty ? default : VideoPlane.FromArray(plane.ToArray(), plane.RowLength, plane.Rows);

    // Checks the frame values and returns the bytes per sample for the depth.
    private static int CheckFrame(int width, int height, TimeSpan timestamp, long frameIndex, int bitDepth)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "The width must be greater than zero.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, "The height must be greater than zero.");
        if (timestamp < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timestamp), timestamp, "The timestamp cannot be negative.");
        if (frameIndex < 0) throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, "The frame index cannot be negative.");
        if (bitDepth != 8 && bitDepth != 10 && bitDepth != 12)
            throw new ArgumentOutOfRangeException(nameof(bitDepth), bitDepth, "The bit depth must be 8, 10 or 12.");

        int bytes = BytesFor(bitDepth);
        // NV12 needs 2 * width bytes per sample size; keep every row length a valid int.
        if (width > int.MaxValue / (2 * bytes)) throw new ArgumentOutOfRangeException(nameof(width), width, "The width is too large for this bit depth.");
        return bytes;
    }

    private static void CheckPlane(VideoPlane plane, int rowLength, int rows, string name)
    {
        if (plane.IsEmpty) throw new ArgumentException($"The {name} plane is missing.", name);
        if (plane.RowLength != rowLength || plane.Rows != rows)
        {
            throw new ArgumentException(
                $"The {name} plane is {plane.RowLength} bytes x {plane.Rows} rows; this frame needs {rowLength} bytes x {rows} rows.",
                name);
        }
    }
}
