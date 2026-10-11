using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>A top-level box of an ISO base media (MP4) file: its type and where it sits.</summary>
internal readonly struct Mp4TopLevelBox
{
    internal Mp4TopLevelBox(string type, long offset, long size)
    {
        Type = type;
        Offset = offset;
        Size = size;
    }

    /// <summary>The four-character type (Latin-1, so "©nam"-style types survive).</summary>
    internal string Type { get; }

    /// <summary>The file offset of the box header.</summary>
    internal long Offset { get; }

    /// <summary>The whole box size, header included.</summary>
    internal long Size { get; }

    /// <summary>The offset just past the box.</summary>
    internal long End => Offset + Size;

    /// <inheritdoc />
    public override string ToString() => $"{Type} @{Offset} ({Size} bytes)";
}

/// <summary>
/// One box of an in-memory MP4 box tree. The boxes on the path to the sample tables (moov, trak, mdia, minf, stbl,
/// udta) are parsed into children; chunk-offset tables (stco / co64) are parsed into values; every other box is kept
/// as its raw bytes and written back unchanged.
/// </summary>
internal sealed class Mp4Node
{
    private static readonly HashSet<string> Containers = new HashSet<string>(StringComparer.Ordinal)
    {
        "moov", "trak", "mdia", "minf", "stbl", "udta", "edts", "dinf", "mvex"
    };

    private Mp4Node(string type)
    {
        Type = type;
    }

    /// <summary>The four-character type.</summary>
    internal string Type { get; }

    /// <summary>The children of a container box; null for a leaf.</summary>
    internal List<Mp4Node> Children { get; private set; }

    /// <summary>The whole box (header included) of a leaf that is kept as-is; null for containers and offset tables.</summary>
    internal byte[] Raw { get; set; }

    /// <summary>For stco / co64: the chunk offsets.</summary>
    internal ulong[] ChunkOffsets { get; set; }

    /// <summary>For stco / co64: the version and flags word.</summary>
    internal uint VersionAndFlags { get; set; }

    /// <summary>True for a chunk-offset table (stco or co64).</summary>
    internal bool IsChunkOffsetTable => ChunkOffsets != null;

    /// <summary>True when an offset table must be written as 64-bit co64.</summary>
    internal bool Use64BitOffsets { get; set; }

    /// <summary>Creates a container node with no children.</summary>
    internal static Mp4Node CreateContainer(string type) => new Mp4Node(type) { Children = new List<Mp4Node>() };

    /// <summary>Creates a leaf node from its payload (the header is added).</summary>
    internal static Mp4Node CreateLeaf(string type, ReadOnlySpan<byte> payload)
    {
        byte[] raw = new byte[8 + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(raw, (uint)raw.Length);
        Encoding.Latin1.GetBytes(type, raw.AsSpan(4, 4));
        payload.CopyTo(raw.AsSpan(8));
        return new Mp4Node(type) { Raw = raw };
    }

    /// <summary>Parses one whole box (header included) into a node.</summary>
    internal static Mp4Node Parse(ReadOnlySpan<byte> box)
    {
        ReadHeader(box, out string type, out long size, out int header);
        if (size != box.Length) throw new InvalidDataException($"The '{type}' box claims {size} bytes but {box.Length} were given.");

        if (Containers.Contains(type))
        {
            var node = CreateContainer(type);
            node.Children.AddRange(ParseChildren(box.Slice(header)));
            return node;
        }

        if (type == "stco" || type == "co64")
        {
            ReadOnlySpan<byte> payload = box.Slice(header);
            if (payload.Length < 8) throw new InvalidDataException($"The '{type}' box is truncated.");
            uint versionAndFlags = BinaryPrimitives.ReadUInt32BigEndian(payload);
            uint count = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4));
            int width = type == "co64" ? 8 : 4;
            if ((long)count * width > payload.Length - 8) throw new InvalidDataException($"The '{type}' box is truncated.");
            ulong[] offsets = new ulong[count];
            for (int i = 0; i < offsets.Length; i++)
            {
                offsets[i] = width == 8
                    ? BinaryPrimitives.ReadUInt64BigEndian(payload.Slice(8 + i * 8))
                    : BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(8 + i * 4));
            }

            return new Mp4Node(type) { ChunkOffsets = offsets, VersionAndFlags = versionAndFlags, Use64BitOffsets = width == 8 };
        }

        return new Mp4Node(type) { Raw = box.ToArray() };
    }

    /// <summary>Parses consecutive boxes filling a span.</summary>
    internal static List<Mp4Node> ParseChildren(ReadOnlySpan<byte> data)
    {
        var children = new List<Mp4Node>();
        int position = 0;
        while (position + 8 <= data.Length)
        {
            ReadHeader(data.Slice(position), out _, out long size, out _);
            if (size == 0) size = data.Length - position;
            if (size < 8 || position + size > data.Length) throw new InvalidDataException("A box overruns its parent.");
            children.Add(Parse(data.Slice(position, (int)size)));
            position += (int)size;
        }

        return children;
    }

    /// <summary>Reads a box header: type, total size (0 = to the end of the parent) and header length.</summary>
    internal static void ReadHeader(ReadOnlySpan<byte> data, out string type, out long size, out int headerLength)
    {
        if (data.Length < 8) throw new InvalidDataException("A box header is truncated.");
        uint size32 = BinaryPrimitives.ReadUInt32BigEndian(data);
        type = Encoding.Latin1.GetString(data.Slice(4, 4));
        headerLength = 8;
        size = size32;
        if (size32 == 1)
        {
            if (data.Length < 16) throw new InvalidDataException("A 64-bit box header is truncated.");
            size = (long)BinaryPrimitives.ReadUInt64BigEndian(data.Slice(8));
            headerLength = 16;
        }
        else if (size32 == 0 && data.Length >= 8)
        {
            size = data.Length;
        }
    }

    /// <summary>The first child of a type, or null.</summary>
    internal Mp4Node Child(string type)
    {
        if (Children == null) return null;
        foreach (Mp4Node child in Children)
        {
            if (child.Type == type) return child;
        }

        return null;
    }

    /// <summary>Every descendant (depth first) of a type.</summary>
    internal IEnumerable<Mp4Node> Descendants(string type)
    {
        if (Children == null) yield break;
        foreach (Mp4Node child in Children)
        {
            if (child.Type == type) yield return child;
            foreach (Mp4Node nested in child.Descendants(type)) yield return nested;
        }
    }

    /// <summary>The serialized size of the box.</summary>
    internal long Size
    {
        get
        {
            if (Raw != null) return Raw.Length;
            if (IsChunkOffsetTable) return 8 + 8 + (long)ChunkOffsets.Length * (Use64BitOffsets ? 8 : 4);
            long size = 8;
            foreach (Mp4Node child in Children) size += child.Size;
            return size;
        }
    }

    /// <summary>Serializes the box (32-bit size headers).</summary>
    internal byte[] ToArray()
    {
        long size = Size;
        if (size > uint.MaxValue) throw new InvalidDataException($"The '{Type}' box is too large to write.");
        byte[] buffer = new byte[size];
        WriteTo(buffer);
        return buffer;
    }

    private int WriteTo(Span<byte> destination)
    {
        if (Raw != null)
        {
            Raw.CopyTo(destination);
            return Raw.Length;
        }

        int size = (int)Size;
        BinaryPrimitives.WriteUInt32BigEndian(destination, (uint)size);
        if (IsChunkOffsetTable)
        {
            Encoding.Latin1.GetBytes(Use64BitOffsets ? "co64" : "stco", destination.Slice(4, 4));
            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(8), VersionAndFlags);
            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(12), (uint)ChunkOffsets.Length);
            for (int i = 0; i < ChunkOffsets.Length; i++)
            {
                if (Use64BitOffsets) BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(16 + i * 8), ChunkOffsets[i]);
                else BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(16 + i * 4), checked((uint)ChunkOffsets[i]));
            }

            return size;
        }

        Encoding.Latin1.GetBytes(Type, destination.Slice(4, 4));
        int position = 8;
        foreach (Mp4Node child in Children) position += child.WriteTo(destination.Slice(position));
        return size;
    }
}

/// <summary>Reads the top-level box list of an MP4 file.</summary>
internal static class Mp4File
{
    /// <summary>Lists the top-level boxes. Throws <see cref="InvalidDataException" /> for a file that is not a box sequence.</summary>
    internal static List<Mp4TopLevelBox> ReadTopLevel(Stream stream)
    {
        var boxes = new List<Mp4TopLevelBox>();
        long length = stream.Length;
        long position = 0;
        Span<byte> header = stackalloc byte[16];
        while (position + 8 <= length)
        {
            stream.Position = position;
            int read = ReadAtMost(stream, header);
            if (read < 8) break;
            uint size32 = BinaryPrimitives.ReadUInt32BigEndian(header);
            string type = Encoding.Latin1.GetString(header.Slice(4, 4));
            long size = size32;
            if (size32 == 1)
            {
                if (read < 16) throw new InvalidDataException("A 64-bit box header is truncated.");
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(header.Slice(8));
            }
            else if (size32 == 0)
            {
                size = length - position;
            }

            if (size < 8 || position + size > length) throw new InvalidDataException($"The top-level '{type}' box at {position} overruns the file.");
            boxes.Add(new Mp4TopLevelBox(type, position, size));
            position += size;
        }

        return boxes;
    }

    /// <summary>Reads a whole box into memory.</summary>
    internal static byte[] ReadBox(Stream stream, Mp4TopLevelBox box, long limit = 512L * 1024 * 1024)
    {
        if (box.Size > limit) throw new InvalidDataException($"The '{box.Type}' box is too large ({box.Size} bytes).");
        byte[] buffer = new byte[box.Size];
        stream.Position = box.Offset;
        stream.ReadExactly(buffer);
        return buffer;
    }

    /// <summary>The index of the first top-level box of a type, or -1.</summary>
    internal static int IndexOf(IReadOnlyList<Mp4TopLevelBox> boxes, string type)
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            if (boxes[i].Type == type) return i;
        }

        return -1;
    }

    private static int ReadAtMost(Stream stream, Span<byte> buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer.Slice(total));
            if (read == 0) break;
            total += read;
        }

        return total;
    }
}
