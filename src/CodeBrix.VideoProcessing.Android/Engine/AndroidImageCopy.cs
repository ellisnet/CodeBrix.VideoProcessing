using System;
using System.Buffers;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core;
using global::Android.Graphics;
using global::Android.Media;
using ByteBuffer = global::Java.Nio.ByteBuffer;

namespace CodeBrix.VideoProcessing.Android.Engine;

/// <summary>
/// Copies pictures between codec buffers and tightly packed managed I420: through the Image API (any plane layout -
/// planar, semi-planar, with or without row padding, 8-bit YUV_420_888 or 16-bit P010 which is cut to its top 8 bits),
/// or through a raw ByteBuffer for the two classic layouts (COLOR_FormatYUV420Planar / SemiPlanar) when a codec offers
/// no Image.
/// </summary>
internal static class AndroidImageCopy
{
    /// <summary>Reads the visible picture of a decoder output Image into a tightly packed I420 buffer of width x height.</summary>
    internal static void ReadI420(Image image, int width, int height, byte[] destination)
    {
        Image.Plane[] planes = image.GetPlanes();
        if (planes == null || planes.Length < 3) throw new VideoProcessingException($"The decoder's output image has {planes?.Length ?? 0} planes; YUV 4:2:0 needs 3.");

        int bytesPerSample = image.Format == ImageFormatType.YcbcrP010 ? 2 : 1;
        Rect crop = image.CropRect;
        int left = crop?.Left ?? 0;
        int top = crop?.Top ?? 0;
        int visibleWidth = Math.Min(width, (crop != null && crop.Width() > 0 ? crop.Width() : image.Width));
        int visibleHeight = Math.Min(height, (crop != null && crop.Height() > 0 ? crop.Height() : image.Height));

        int chromaWidth = (width + 1) / 2;
        int luma = width * height;
        int chroma = chromaWidth * ((height + 1) / 2);
        ReadPlane(planes[0], left, top, visibleWidth, visibleHeight, bytesPerSample, destination, 0, width);
        ReadPlane(planes[1], left / 2, top / 2, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, bytesPerSample, destination, luma, chromaWidth);
        ReadPlane(planes[2], left / 2, top / 2, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, bytesPerSample, destination, luma + chroma, chromaWidth);
    }

    /// <summary>Writes a tightly packed I420 picture into an encoder input Image.</summary>
    internal static void WriteI420(ReadOnlySpan<byte> i420, int width, int height, Image image)
    {
        Image.Plane[] planes = image.GetPlanes();
        if (planes == null || planes.Length < 3) throw new VideoProcessingException($"The encoder's input image has {planes?.Length ?? 0} planes; YUV 4:2:0 needs 3.");

        int chromaWidth = (width + 1) / 2;
        int chromaHeight = (height + 1) / 2;
        int luma = width * height;
        int chroma = chromaWidth * chromaHeight;
        WritePlane(i420.Slice(0, luma), width, height, planes[0]);
        WritePlane(i420.Slice(luma, chroma), chromaWidth, chromaHeight, planes[1]);
        WritePlane(i420.Slice(luma + chroma, chroma), chromaWidth, chromaHeight, planes[2]);
    }

    /// <summary>
    /// Reads a decoder output ByteBuffer laid out as COLOR_FormatYUV420Planar (19) or SemiPlanar (21), with the
    /// format's stride and slice height, into tightly packed I420.
    /// </summary>
    internal static void ReadI420FromBuffer(ByteBuffer buffer, int offset, MediaFormat format, int width, int height, byte[] destination)
    {
        int colorFormat = AndroidMedia.GetInt(format, MediaFormatKeys.ColorFormat);
        int stride = AndroidMedia.GetInt(format, MediaFormatKeys.Stride, width);
        int sliceHeight = AndroidMedia.GetInt(format, MediaFormatKeys.SliceHeight, height);
        if (stride < width) stride = width;
        if (sliceHeight < height) sliceHeight = height;
        int left = Math.Max(0, AndroidMedia.GetInt(format, MediaFormatKeys.CropLeft, 0));
        int top = Math.Max(0, AndroidMedia.GetInt(format, MediaFormatKeys.CropTop, 0));

        int chromaWidth = (width + 1) / 2;
        int chromaHeight = (height + 1) / 2;
        int luma = width * height;
        int chroma = chromaWidth * chromaHeight;

        for (int row = 0; row < height; row++)
        {
            AndroidMedia.CopyFrom(buffer, offset + (top + row) * stride + left, destination, row * width, width);
        }

        int chromaBase = offset + stride * sliceHeight;
        if (colorFormat == MediaFormatKeys.ColorFormatYuv420Planar)
        {
            int chromaStride = stride / 2;
            int vBase = chromaBase + chromaStride * (sliceHeight / 2);
            for (int row = 0; row < chromaHeight; row++)
            {
                AndroidMedia.CopyFrom(buffer, chromaBase + (top / 2 + row) * chromaStride + left / 2, destination, luma + row * chromaWidth, chromaWidth);
                AndroidMedia.CopyFrom(buffer, vBase + (top / 2 + row) * chromaStride + left / 2, destination, luma + chroma + row * chromaWidth, chromaWidth);
            }
        }
        else if (colorFormat == MediaFormatKeys.ColorFormatYuv420SemiPlanar)
        {
            byte[] row2 = ArrayPool<byte>.Shared.Rent(2 * chromaWidth);
            try
            {
                for (int row = 0; row < chromaHeight; row++)
                {
                    AndroidMedia.CopyFrom(buffer, chromaBase + (top / 2 + row) * stride + (left / 2) * 2, row2, 0, 2 * chromaWidth);
                    for (int column = 0; column < chromaWidth; column++)
                    {
                        destination[luma + row * chromaWidth + column] = row2[2 * column];
                        destination[luma + chroma + row * chromaWidth + column] = row2[2 * column + 1];
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(row2);
            }
        }
        else
        {
            throw new VideoProcessingException($"The decoder offers no Image and its color format 0x{colorFormat:X} is not one the engine can read from a buffer.");
        }
    }

    private static void ReadPlane(Image.Plane plane, int x0, int y0, int width, int height, int bytesPerSample, byte[] destination, int destinationOffset, int destinationRow)
    {
        ByteBuffer buffer = plane.Buffer;
        int rowStride = plane.RowStride;
        int pixelStride = plane.PixelStride;
        int start = buffer.Position();
        int limit = buffer.Limit();
        int span = (width - 1) * pixelStride + bytesPerSample;

        if (pixelStride == 1 && bytesPerSample == 1)
        {
            for (int row = 0; row < height; row++)
            {
                int at = start + (y0 + row) * rowStride + x0;
                int count = Math.Min(width, limit - at);
                if (count <= 0) break;
                AndroidMedia.CopyFrom(buffer, at, destination, destinationOffset + row * destinationRow, count);
            }

            return;
        }

        byte[] scratch = ArrayPool<byte>.Shared.Rent(span);
        try
        {
            int high = bytesPerSample - 1; // little-endian 16-bit (P010): the top 8 bits are the high byte
            for (int row = 0; row < height; row++)
            {
                int at = start + (y0 + row) * rowStride + x0 * pixelStride;
                int count = Math.Min(span, limit - at);
                if (count <= 0) break;
                AndroidMedia.CopyFrom(buffer, at, scratch, 0, count);
                int columns = Math.Min(width, (count - bytesPerSample) / pixelStride + 1);
                int o = destinationOffset + row * destinationRow;
                for (int column = 0; column < columns; column++) destination[o + column] = scratch[column * pixelStride + high];
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
    }

    // Writes one plane; where samples are not adjacent (pixel stride above 1, e.g. semi-planar chroma) each row is
    // read, merged and written back so the bytes between our samples (the other chroma plane's) are preserved.
    private static void WritePlane(ReadOnlySpan<byte> source, int width, int height, Image.Plane plane)
    {
        ByteBuffer buffer = plane.Buffer;
        int rowStride = plane.RowStride;
        int pixelStride = plane.PixelStride;
        int start = buffer.Position();
        int limit = buffer.Limit();
        byte[] row = ArrayPool<byte>.Shared.Rent(Math.Max(1, (width - 1) * pixelStride + 1));
        try
        {
            for (int y = 0; y < height; y++)
            {
                int at = start + y * rowStride;
                int span = Math.Min((width - 1) * pixelStride + 1, limit - at);
                if (span <= 0) break;
                ReadOnlySpan<byte> sourceRow = source.Slice(y * width, width);
                if (pixelStride == 1)
                {
                    sourceRow.Slice(0, span).CopyTo(row);
                }
                else
                {
                    AndroidMedia.CopyFrom(buffer, at, row, 0, span);
                    for (int x = 0; x < width && x * pixelStride < span; x++) row[x * pixelStride] = sourceRow[x];
                }

                AndroidMedia.CopyTo(row, 0, span, buffer, at);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(row);
        }
    }
}
