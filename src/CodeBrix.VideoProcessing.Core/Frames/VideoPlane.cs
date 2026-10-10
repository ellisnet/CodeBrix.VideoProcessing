using System;

namespace CodeBrix.VideoProcessing.Core.Frames;

/// <summary>One plane of a video frame: rows of samples, <see cref="Stride" /> bytes apart.</summary>
/// <remarks>
/// A plane is plain bytes: an 8-bit sample is one byte, and a 10- or 12-bit sample is two (unsigned 16-bit
/// little-endian, value in the low bits - see <see cref="VideoFrameData.BitDepth" />). Every size here is in BYTES.
/// <see cref="RowLength" /> is the number of meaningful BYTES in each row (for an RGBA plane, four per pixel; for a
/// 10- or 12-bit plane, two per sample);
/// <see cref="Stride" /> is the distance between the starts of two rows and may be larger (padding). The last row
/// need not be padded, so <see cref="Data" /> is at least <c>Stride * (Rows - 1) + RowLength</c> bytes long.
/// The default value is the EMPTY plane (<see cref="IsEmpty" />), used for planes a layout does not have.
/// </remarks>
public readonly struct VideoPlane
{
    /// <summary>Creates a plane over existing memory.</summary>
    /// <param name="data">The samples.</param>
    /// <param name="stride">Bytes from the start of one row to the start of the next; at least <paramref name="rowLength" />.</param>
    /// <param name="rowLength">Meaningful bytes per row; greater than zero.</param>
    /// <param name="rows">The number of rows; greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is out of range, or <paramref name="data" /> is too short.</exception>
    public VideoPlane(ReadOnlyMemory<byte> data, int stride, int rowLength, int rows)
    {
        if (rowLength <= 0) throw new ArgumentOutOfRangeException(nameof(rowLength), rowLength, "The row length must be greater than zero.");
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows), rows, "The row count must be greater than zero.");
        if (stride < rowLength) throw new ArgumentOutOfRangeException(nameof(stride), stride, $"The stride must be at least the row length ({rowLength}).");

        long required = (long)stride * (rows - 1) + rowLength;
        if (data.Length < required)
            throw new ArgumentOutOfRangeException(nameof(data), data.Length, $"The plane needs at least {required} bytes ({rows} rows of {rowLength} bytes, stride {stride}).");

        Data = data;
        Stride = stride;
        RowLength = rowLength;
        Rows = rows;
    }

    /// <summary>Creates a tightly packed plane (stride equals row length) over an array.</summary>
    /// <param name="data">The samples, at least <c>rowLength * rows</c> bytes.</param>
    /// <param name="rowLength">Bytes per row.</param>
    /// <param name="rows">The number of rows.</param>
    /// <returns>The plane.</returns>
    public static VideoPlane FromArray(byte[] data, int rowLength, int rows) =>
        new VideoPlane(data ?? throw new ArgumentNullException(nameof(data)), rowLength, rowLength, rows);

    /// <summary>The samples.</summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>Bytes from the start of one row to the start of the next.</summary>
    public int Stride { get; }

    /// <summary>Meaningful bytes per row.</summary>
    public int RowLength { get; }

    /// <summary>The number of rows.</summary>
    public int Rows { get; }

    /// <summary>True for the empty plane - the default value, standing for a plane the frame does not have.</summary>
    public bool IsEmpty => Rows == 0;

    /// <summary>The meaningful bytes of one row.</summary>
    /// <param name="row">The zero-based row.</param>
    /// <returns>A span of <see cref="RowLength" /> bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="row" /> is outside the plane.</exception>
    public ReadOnlySpan<byte> GetRow(int row)
    {
        if ((uint)row >= (uint)Rows) throw new ArgumentOutOfRangeException(nameof(row), row, $"The row must be between 0 and {Rows - 1}.");
        return Data.Span.Slice(row * Stride, RowLength);
    }

    /// <summary>Copies the plane into a new, tightly packed array (<c>RowLength * Rows</c> bytes).</summary>
    /// <returns>The copy; an empty array for the empty plane.</returns>
    public byte[] ToArray()
    {
        if (IsEmpty) return Array.Empty<byte>();

        byte[] copy = new byte[RowLength * Rows];
        CopyTo(copy);
        return copy;
    }

    /// <summary>Copies the plane, tightly packed, into a destination span.</summary>
    /// <param name="destination">At least <c>RowLength * Rows</c> bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is too short.</exception>
    public void CopyTo(Span<byte> destination)
    {
        if (IsEmpty) return;
        if (destination.Length < RowLength * Rows) throw new ArgumentException($"The destination needs at least {RowLength * Rows} bytes.", nameof(destination));

        ReadOnlySpan<byte> source = Data.Span;
        if (Stride == RowLength)
        {
            source.Slice(0, RowLength * Rows).CopyTo(destination);
            return;
        }

        for (int row = 0; row < Rows; row++)
        {
            source.Slice(row * Stride, RowLength).CopyTo(destination.Slice(row * RowLength, RowLength));
        }
    }
}
