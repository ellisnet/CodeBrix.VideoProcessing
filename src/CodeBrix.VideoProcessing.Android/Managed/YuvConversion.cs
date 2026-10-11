using System;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// Managed 8-bit picture conversions between the contract's layouts: RGBA, NV12 and I420, plus a bilinear I420
/// scaler. Every I420 buffer here is TIGHTLY PACKED: Y (width x height), then U and V
/// (((width + 1) / 2) x ((height + 1) / 2) each).
/// </summary>
/// <remarks>
/// RGB / YUV conversions use the matrix and range given: BT.601, BT.709 or BT.2020 coefficients, limited (16..235 /
/// 16..240) or full (0..255) range. <see cref="VideoColorMatrix.Unspecified" /> (and the matrices with no YUV
/// coefficients) resolve by size the way players do - BT.601 up to 576 lines, BT.709 above
/// (<see cref="ResolveMatrix" />). RGBA to I420 averages each 2x2 block's RGB before converting it to chroma;
/// I420 to RGBA repeats each chroma sample over its 2x2 block (no chroma interpolation) and writes alpha 255.
/// </remarks>
internal static class YuvConversion
{
    /// <summary>The size of a tightly packed I420 picture.</summary>
    internal static int I420Size(int width, int height) => width * height + 2 * ChromaSize(width, height);

    /// <summary>The size of one tightly packed I420 chroma plane.</summary>
    internal static int ChromaSize(int width, int height) => ((width + 1) / 2) * ((height + 1) / 2);

    /// <summary>The matrix actually used for a conversion: the given one, or BT.601 / BT.709 by height when it has no coefficients.</summary>
    internal static VideoColorMatrix ResolveMatrix(VideoColorMatrix matrix, int height) => matrix switch
    {
        VideoColorMatrix.Bt601 => VideoColorMatrix.Bt601,
        VideoColorMatrix.Bt709 => VideoColorMatrix.Bt709,
        VideoColorMatrix.Bt2020NonConstant => VideoColorMatrix.Bt2020NonConstant,
        VideoColorMatrix.Bt2020Constant => VideoColorMatrix.Bt2020NonConstant,
        _ => height > 576 ? VideoColorMatrix.Bt709 : VideoColorMatrix.Bt601
    };

    private static void Coefficients(VideoColorMatrix matrix, out double kr, out double kb)
    {
        switch (matrix)
        {
            case VideoColorMatrix.Bt709:
                kr = 0.2126; kb = 0.0722;
                break;
            case VideoColorMatrix.Bt2020NonConstant:
                kr = 0.2627; kb = 0.0593;
                break;
            default:
                kr = 0.299; kb = 0.114;
                break;
        }
    }

    /// <summary>Converts an 8-bit RGBA plane to a tightly packed I420 buffer (alpha is ignored).</summary>
    /// <param name="rgba">The RGBA plane (4 * width bytes per row).</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="matrix">The YUV matrix to produce (see <see cref="ResolveMatrix" />).</param>
    /// <param name="range">Limited or full range (Unspecified means limited).</param>
    /// <param name="destination">At least <see cref="I420Size" /> bytes.</param>
    internal static void RgbaToI420(VideoPlane rgba, int width, int height, VideoColorMatrix matrix, VideoColorRange range, Span<byte> destination)
    {
        Coefficients(ResolveMatrix(matrix, height), out double kr, out double kb);
        double kg = 1 - kr - kb;
        bool full = range == VideoColorRange.Full;
        double yScale = full ? 255.0 : 219.0;
        double yOffset = full ? 0 : 16;
        double cScale = full ? 255.0 : 224.0;

        int chromaWidth = (width + 1) / 2;
        int chromaHeight = (height + 1) / 2;
        Span<byte> yPlane = destination.Slice(0, width * height);
        Span<byte> uPlane = destination.Slice(width * height, chromaWidth * chromaHeight);
        Span<byte> vPlane = destination.Slice(width * height + chromaWidth * chromaHeight, chromaWidth * chromaHeight);

        for (int row = 0; row < height; row++)
        {
            ReadOnlySpan<byte> source = rgba.GetRow(row);
            int yRow = row * width;
            for (int column = 0; column < width; column++)
            {
                int p = column * 4;
                double luma = (kr * source[p] + kg * source[p + 1] + kb * source[p + 2]) / 255.0;
                yPlane[yRow + column] = Clamp(yOffset + yScale * luma);
            }
        }

        for (int chromaRow = 0; chromaRow < chromaHeight; chromaRow++)
        {
            int row0 = chromaRow * 2;
            int row1 = Math.Min(row0 + 1, height - 1);
            ReadOnlySpan<byte> top = rgba.GetRow(row0);
            ReadOnlySpan<byte> bottom = rgba.GetRow(row1);
            for (int chromaColumn = 0; chromaColumn < chromaWidth; chromaColumn++)
            {
                int c0 = chromaColumn * 2 * 4;
                int c1 = Math.Min(chromaColumn * 2 + 1, width - 1) * 4;
                double r = (top[c0] + top[c1] + bottom[c0] + bottom[c1]) / (4.0 * 255.0);
                double g = (top[c0 + 1] + top[c1 + 1] + bottom[c0 + 1] + bottom[c1 + 1]) / (4.0 * 255.0);
                double b = (top[c0 + 2] + top[c1 + 2] + bottom[c0 + 2] + bottom[c1 + 2]) / (4.0 * 255.0);
                double luma = kr * r + kg * g + kb * b;
                double pb = (b - luma) / (2 * (1 - kb));
                double pr = (r - luma) / (2 * (1 - kr));
                int index = chromaRow * chromaWidth + chromaColumn;
                uPlane[index] = Clamp(128 + cScale * pb);
                vPlane[index] = Clamp(128 + cScale * pr);
            }
        }
    }

    /// <summary>Converts a tightly packed I420 buffer to a tightly packed RGBA buffer (alpha 255).</summary>
    /// <param name="i420">The I420 picture.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="matrix">The YUV matrix of the samples (see <see cref="ResolveMatrix" />).</param>
    /// <param name="range">Limited or full range (Unspecified means limited).</param>
    /// <param name="destination">At least 4 * width * height bytes.</param>
    internal static void I420ToRgba(ReadOnlySpan<byte> i420, int width, int height, VideoColorMatrix matrix, VideoColorRange range, Span<byte> destination)
    {
        Coefficients(ResolveMatrix(matrix, height), out double kr, out double kb);
        double kg = 1 - kr - kb;
        bool full = range == VideoColorRange.Full;
        double yScale = full ? 1.0 / 255.0 : 1.0 / 219.0;
        double yOffset = full ? 0 : 16;
        double cScale = full ? 1.0 / 255.0 : 1.0 / 224.0;

        int chromaWidth = (width + 1) / 2;
        int chromaSize = ChromaSize(width, height);
        ReadOnlySpan<byte> yPlane = i420.Slice(0, width * height);
        ReadOnlySpan<byte> uPlane = i420.Slice(width * height, chromaSize);
        ReadOnlySpan<byte> vPlane = i420.Slice(width * height + chromaSize, chromaSize);

        for (int row = 0; row < height; row++)
        {
            int chromaRow = (row / 2) * chromaWidth;
            for (int column = 0; column < width; column++)
            {
                double luma = (yPlane[row * width + column] - yOffset) * yScale;
                double pb = (uPlane[chromaRow + column / 2] - 128) * cScale;
                double pr = (vPlane[chromaRow + column / 2] - 128) * cScale;

                double r = luma + 2 * (1 - kr) * pr;
                double b = luma + 2 * (1 - kb) * pb;
                double g = (luma - kr * r - kb * b) / kg;

                int o = (row * width + column) * 4;
                destination[o] = Clamp(r * 255);
                destination[o + 1] = Clamp(g * 255);
                destination[o + 2] = Clamp(b * 255);
                destination[o + 3] = 255;
            }
        }
    }

    /// <summary>Converts an NV12 frame's planes to a tightly packed I420 buffer (de-interleaves the chroma).</summary>
    internal static void Nv12ToI420(VideoPlane y, VideoPlane uv, int width, int height, Span<byte> destination)
    {
        int chromaWidth = (width + 1) / 2;
        int chromaHeight = (height + 1) / 2;
        y.CopyTo(destination);
        Span<byte> u = destination.Slice(width * height, chromaWidth * chromaHeight);
        Span<byte> v = destination.Slice(width * height + chromaWidth * chromaHeight, chromaWidth * chromaHeight);
        for (int row = 0; row < chromaHeight; row++)
        {
            ReadOnlySpan<byte> source = uv.GetRow(row);
            for (int column = 0; column < chromaWidth; column++)
            {
                u[row * chromaWidth + column] = source[2 * column];
                v[row * chromaWidth + column] = source[2 * column + 1];
            }
        }
    }

    /// <summary>Converts a tightly packed I420 buffer to a tightly packed NV12 buffer (Y, then interleaved Cb Cr).</summary>
    internal static void I420ToNv12(ReadOnlySpan<byte> i420, int width, int height, Span<byte> destination)
    {
        int luma = width * height;
        int chromaSize = ChromaSize(width, height);
        i420.Slice(0, luma).CopyTo(destination);
        ReadOnlySpan<byte> u = i420.Slice(luma, chromaSize);
        ReadOnlySpan<byte> v = i420.Slice(luma + chromaSize, chromaSize);
        Span<byte> uv = destination.Slice(luma, 2 * chromaSize);
        for (int i = 0; i < chromaSize; i++)
        {
            uv[2 * i] = u[i];
            uv[2 * i + 1] = v[i];
        }
    }

    /// <summary>Copies an I420 frame's (possibly strided) planes into a tightly packed I420 buffer.</summary>
    internal static void PackI420(in VideoFrameData frame, Span<byte> destination)
    {
        int luma = frame.Width * frame.Height;
        int chromaSize = ChromaSize(frame.Width, frame.Height);
        frame.Y.CopyTo(destination);
        frame.U.CopyTo(destination.Slice(luma));
        frame.V.CopyTo(destination.Slice(luma + chromaSize));
    }

    /// <summary>Scales a tightly packed I420 picture to another size, bilinear per plane.</summary>
    internal static void ScaleI420(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, Span<byte> destination, int width, int height)
    {
        int sourceLuma = sourceWidth * sourceHeight;
        int sourceChroma = ChromaSize(sourceWidth, sourceHeight);
        int luma = width * height;
        int chroma = ChromaSize(width, height);
        int sourceChromaWidth = (sourceWidth + 1) / 2;
        int sourceChromaHeight = (sourceHeight + 1) / 2;
        int chromaWidth = (width + 1) / 2;
        int chromaHeight = (height + 1) / 2;

        ScalePlane(source.Slice(0, sourceLuma), sourceWidth, sourceHeight, destination.Slice(0, luma), width, height);
        ScalePlane(source.Slice(sourceLuma, sourceChroma), sourceChromaWidth, sourceChromaHeight, destination.Slice(luma, chroma), chromaWidth, chromaHeight);
        ScalePlane(source.Slice(sourceLuma + sourceChroma, sourceChroma), sourceChromaWidth, sourceChromaHeight, destination.Slice(luma + chroma, chroma), chromaWidth, chromaHeight);
    }

    // Bilinear with pixel centres aligned (the usual "half-pixel" mapping), edges clamped.
    private static void ScalePlane(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, Span<byte> destination, int width, int height)
    {
        if (sourceWidth == width && sourceHeight == height)
        {
            source.Slice(0, width * height).CopyTo(destination);
            return;
        }

        double xRatio = (double)sourceWidth / width;
        double yRatio = (double)sourceHeight / height;
        for (int row = 0; row < height; row++)
        {
            double sy = Math.Max(0, (row + 0.5) * yRatio - 0.5);
            int y0 = Math.Min((int)sy, sourceHeight - 1);
            int y1 = Math.Min(y0 + 1, sourceHeight - 1);
            double fy = sy - y0;
            for (int column = 0; column < width; column++)
            {
                double sx = Math.Max(0, (column + 0.5) * xRatio - 0.5);
                int x0 = Math.Min((int)sx, sourceWidth - 1);
                int x1 = Math.Min(x0 + 1, sourceWidth - 1);
                double fx = sx - x0;
                double top = source[y0 * sourceWidth + x0] * (1 - fx) + source[y0 * sourceWidth + x1] * fx;
                double bottom = source[y1 * sourceWidth + x0] * (1 - fx) + source[y1 * sourceWidth + x1] * fx;
                destination[row * width + column] = Clamp(top * (1 - fy) + bottom * fy);
            }
        }
    }

    private static byte Clamp(double value)
    {
        double rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded <= 0) return 0;
        if (rounded >= 255) return 255;
        return (byte)rounded;
    }
}
