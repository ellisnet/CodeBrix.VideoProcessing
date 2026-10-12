using System;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>How the frame source asks its decoder for pictures, decided from the requested and the source bit depth.</summary>
internal enum DecoderOutputMode
{
    /// <summary>8-bit frames: flexible YUV 4:2:0 output; P010 output (if a decoder sends it anyway) is cut to its top 8 bits.</summary>
    EightBit,

    /// <summary>10-bit frames from an 8-bit source: flexible YUV 4:2:0 output, every sample widened (value &lt;&lt; 2).</summary>
    WidenEightBit,

    /// <summary>10-bit frames from a 10-bit (or unknown-depth) source: P010 output REQUIRED, shifted down to the low bits.</summary>
    P010
}

/// <summary>
/// Managed 10-bit picture handling for the frame source: reading P010 (or 8-bit) planes into the contract's 10-bit
/// samples, and the 10-bit NV12 interleave. Every 10-bit buffer here is TIGHTLY PACKED I420 at two bytes per sample:
/// Y (width x height samples), then U and V (((width + 1) / 2) x ((height + 1) / 2) samples each); a sample is an
/// unsigned 16-bit LITTLE-ENDIAN value with the 10 bits in the LOW bits (the contract's convention).
/// </summary>
/// <remarks>
/// P010 (Android's COLOR_FormatYUVP010 / ImageFormat.YCBCR_P010) stores each 10-bit sample in the HIGH bits of a
/// little-endian 16-bit word (value &lt;&lt; 6, low 6 bits zero or garbage), so a P010 sample is shifted right by
/// <see cref="P010Shift" /> on copy. An 8-bit sample delivered at 10 bits is widened by shifting left by 2 (16 becomes
/// 64 and 235 becomes 940 - the limited-range levels line up exactly; 255 becomes 1020).
/// </remarks>
internal static class HighBitDepthConversion
{
    /// <summary>How far a P010 word is shifted right to give the 10-bit sample.</summary>
    internal const int P010Shift = 6;

    /// <summary>How far an 8-bit sample is shifted left to give a 10-bit one.</summary>
    internal const int WidenShift = 2;

    /// <summary>
    /// Decides how to decode for a request: <paramref name="requestedDepth" /> 8 keeps today's 8-bit path; 10 with a
    /// known 8-bit source widens the 8-bit pictures; 10 with a 10-bit or unknown-depth source requires P010 output.
    /// </summary>
    /// <param name="requestedDepth">VideoSourceOptions.BitDepth (8, 10 or 12).</param>
    /// <param name="sourceDepth">The stream's bit depth from its profile; 0 when unknown.</param>
    /// <exception cref="NotSupportedException">12 bits were asked for (Android decoders do not output 12-bit pictures) or the source is deeper than 10 bits.</exception>
    internal static DecoderOutputMode ChooseMode(int requestedDepth, int sourceDepth)
    {
        if (requestedDepth <= 8) return DecoderOutputMode.EightBit;
        CheckRequestedDepth(requestedDepth);

        if (sourceDepth > 10)
        {
            throw new NotSupportedException(
                $"The 'android-mediacodec' engine cannot decode a {sourceDepth}-bit stream at 10 bits exactly (Android's decoders output 8-bit or 10-bit pictures only).");
        }

        return sourceDepth == 8 ? DecoderOutputMode.WidenEightBit : DecoderOutputMode.P010;
    }

    /// <summary>
    /// True when the decoder should first be asked for P010 output: always for <see cref="DecoderOutputMode.P010" />,
    /// and for 8-bit frames from a KNOWN 10-bit source - some hardware decoders (the Pixel 4 XL's c2.qti.hevc.decoder)
    /// fail outright when a 10-bit stream is decoded to flexible 8-bit buffers, while their P010 output works and is
    /// cut to its top 8 bits exactly as an 8-bit cut of the stream. For the 8-bit case a decoder that will not output
    /// P010 is not an error: the source falls back to flexible output.
    /// </summary>
    /// <param name="mode">The chosen mode (<see cref="ChooseMode" />).</param>
    /// <param name="sourceDepth">The stream's bit depth from its profile; 0 when unknown.</param>
    internal static bool AsksForP010(DecoderOutputMode mode, int sourceDepth) =>
        mode == DecoderOutputMode.P010 || (mode == DecoderOutputMode.EightBit && sourceDepth == 10);

    /// <summary>Refuses a requested depth above 10 bits: Android's decoders output 8-bit or 10-bit (P010) pictures only.</summary>
    /// <param name="requestedDepth">VideoSourceOptions.BitDepth.</param>
    /// <exception cref="NotSupportedException"><paramref name="requestedDepth" /> is above 10.</exception>
    internal static void CheckRequestedDepth(int requestedDepth)
    {
        if (requestedDepth > 10)
        {
            throw new NotSupportedException(
                $"The 'android-mediacodec' engine cannot deliver {requestedDepth}-bit frames: Android's video decoders output 8-bit or 10-bit (P010) pictures only. Ask for BitDepth = 10 or 8.");
        }
    }

    /// <summary>The size of a tightly packed 10-bit I420 picture, in bytes.</summary>
    internal static int I420Size16(int width, int height) => 2 * YuvConversion.I420Size(width, height);

    /// <summary>
    /// Converts <paramref name="count" /> samples of one source row into 10-bit low-bits samples (two bytes each).
    /// </summary>
    /// <param name="source">The row, starting at its first sample.</param>
    /// <param name="pixelStride">Bytes from one sample to the next in <paramref name="source" />.</param>
    /// <param name="sourceBytesPerSample">2 for P010 (16-bit little-endian, value in the high 10 bits), 1 for 8-bit.</param>
    /// <param name="count">The number of samples.</param>
    /// <param name="destination">At least 2 * <paramref name="count" /> bytes.</param>
    internal static void ConvertRow(ReadOnlySpan<byte> source, int pixelStride, int sourceBytesPerSample, int count, Span<byte> destination)
    {
        if (sourceBytesPerSample == 2)
        {
            for (int i = 0; i < count; i++)
            {
                int at = i * pixelStride;
                int value = (source[at] | (source[at + 1] << 8)) >> P010Shift;
                destination[2 * i] = (byte)value;
                destination[2 * i + 1] = (byte)(value >> 8);
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                int value = source[i * pixelStride] << WidenShift;
                destination[2 * i] = (byte)value;
                destination[2 * i + 1] = (byte)(value >> 8);
            }
        }
    }

    /// <summary>
    /// Reads one plane - rows <paramref name="rowStride" /> bytes apart, samples <paramref name="pixelStride" /> bytes
    /// apart - from <paramref name="source" /> into a tightly packed 10-bit plane of width x height samples. Rows (or
    /// the end of the last row) beyond the end of the source are left as they are, as the platform may omit the
    /// padding after the last sample.
    /// </summary>
    /// <param name="source">The plane's bytes; <paramref name="sourceOffset" /> is its first sample (of row 0, column 0).</param>
    /// <param name="sourceOffset">Where the plane starts in <paramref name="source" />.</param>
    /// <param name="rowStride">Bytes from one row to the next.</param>
    /// <param name="pixelStride">Bytes from one sample to the next (2 for P010 luma, 4 for P010 interleaved chroma).</param>
    /// <param name="sourceBytesPerSample">2 for P010, 1 for 8-bit.</param>
    /// <param name="x0">The first column to read (the crop's left edge in this plane's samples).</param>
    /// <param name="y0">The first row to read (the crop's top edge in this plane's rows).</param>
    /// <param name="width">Samples per row to read.</param>
    /// <param name="height">Rows to read.</param>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="destinationOffset">Where the plane starts in <paramref name="destination" />.</param>
    /// <param name="destinationWidth">Samples per destination row (the frame's plane width; at least <paramref name="width" />).</param>
    internal static void ReadPlane(
        ReadOnlySpan<byte> source, int sourceOffset, int rowStride, int pixelStride, int sourceBytesPerSample,
        int x0, int y0, int width, int height, Span<byte> destination, int destinationOffset, int destinationWidth)
    {
        if (width <= 0 || height <= 0) return;
        int span = (width - 1) * pixelStride + sourceBytesPerSample;
        for (int row = 0; row < height; row++)
        {
            int at = sourceOffset + (y0 + row) * rowStride + x0 * pixelStride;
            int count = Math.Min(span, source.Length - at);
            if (count < sourceBytesPerSample) break;
            int columns = Math.Min(width, (count - sourceBytesPerSample) / pixelStride + 1);
            ConvertRow(source.Slice(at, count), pixelStride, sourceBytesPerSample, columns, destination.Slice(destinationOffset + 2 * row * destinationWidth, 2 * columns));
        }
    }

    /// <summary>
    /// Reads a P010 picture laid out in ONE buffer (a decoder that offers no Image): the Y plane at
    /// <paramref name="offset" />, then - <paramref name="stride" /> x <paramref name="sliceHeight" /> bytes later -
    /// the interleaved Cb/Cr plane at the same stride, into tightly packed 10-bit I420.
    /// </summary>
    /// <param name="buffer">The decoder's output bytes.</param>
    /// <param name="offset">Where the picture starts.</param>
    /// <param name="stride">Bytes per row (KEY_STRIDE); a value below 2 x the width is taken as a count of SAMPLES and doubled.</param>
    /// <param name="sliceHeight">Rows in the Y plane's allocation (KEY_SLICE_HEIGHT; at least the height).</param>
    /// <param name="left">The crop's left edge, in pixels.</param>
    /// <param name="top">The crop's top edge, in pixels.</param>
    /// <param name="width">The picture width.</param>
    /// <param name="height">The picture height.</param>
    /// <param name="destination">At least <see cref="I420Size16" /> bytes.</param>
    internal static void ReadP010Buffer(ReadOnlySpan<byte> buffer, int offset, int stride, int sliceHeight, int left, int top, int width, int height, Span<byte> destination)
    {
        if (stride < 2 * width) stride = stride >= width ? 2 * stride : 2 * width;
        if (sliceHeight < height) sliceHeight = height;
        int chromaWidth = (width + 1) / 2;
        int chromaHeight = (height + 1) / 2;
        int luma = width * height;
        int chroma = chromaWidth * chromaHeight;
        int chromaBase = offset + stride * sliceHeight;

        ReadPlane(buffer, offset, stride, 2, 2, left, top, width, height, destination, 0, width);
        ReadPlane(buffer, chromaBase, stride, 4, 2, left / 2, top / 2, chromaWidth, chromaHeight, destination, 2 * luma, chromaWidth);
        ReadPlane(buffer, chromaBase + 2, stride, 4, 2, left / 2, top / 2, chromaWidth, chromaHeight, destination, 2 * (luma + chroma), chromaWidth);
    }

    /// <summary>Widens a tightly packed 8-bit buffer (any layout) into 10-bit samples, value &lt;&lt; 2, two bytes each.</summary>
    /// <param name="source">The 8-bit samples.</param>
    /// <param name="destination">At least 2 x the source's length.</param>
    internal static void Widen(ReadOnlySpan<byte> source, Span<byte> destination) =>
        ConvertRow(source, 1, 1, source.Length, destination);

    /// <summary>
    /// Converts a tightly packed 10-bit I420 buffer to a tightly packed 10-bit NV12 buffer: Y, then Cb Cr interleaved
    /// SAMPLE by sample (two bytes each) - the P010 plane arrangement, with the contract's low-bits samples.
    /// </summary>
    /// <param name="i420">At least <see cref="I420Size16" /> bytes.</param>
    /// <param name="width">The picture width.</param>
    /// <param name="height">The picture height.</param>
    /// <param name="destination">At least <see cref="I420Size16" /> bytes.</param>
    internal static void I420ToNv12(ReadOnlySpan<byte> i420, int width, int height, Span<byte> destination)
    {
        int luma = 2 * width * height;
        int chromaSize = 2 * YuvConversion.ChromaSize(width, height);
        i420.Slice(0, luma).CopyTo(destination);
        ReadOnlySpan<byte> u = i420.Slice(luma, chromaSize);
        ReadOnlySpan<byte> v = i420.Slice(luma + chromaSize, chromaSize);
        Span<byte> uv = destination.Slice(luma, 2 * chromaSize);
        for (int i = 0; i < chromaSize; i += 2)
        {
            uv[2 * i] = u[i];
            uv[2 * i + 1] = u[i + 1];
            uv[2 * i + 2] = v[i];
            uv[2 * i + 3] = v[i + 1];
        }
    }
}
