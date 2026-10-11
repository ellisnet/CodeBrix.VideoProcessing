using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// The managed post-pass over a finished MP4: moves the index ("moov") in front of the media data ("FAST START" -
/// what lets playback begin before the whole file has arrived), and writes the title and creation-time tags that
/// Android's MediaMuxer has no API for.
/// </summary>
/// <remarks>
/// <para>
/// HOW: list the top-level boxes; read moov into a box tree; edit it; lay the file out again (moov before the first
/// mdat when moving, the original order otherwise; top-level "free" / "skip" padding is dropped when moving); then
/// patch every chunk offset (stco / co64) by how far the box holding that chunk moved. A 32-bit stco whose offsets
/// would pass 4 GiB is rewritten as a 64-bit co64, which grows moov and so the layout is computed again until it is
/// stable. The new file is written beside the old one and moved over it, so a failure leaves the original intact.
/// </para>
/// <para>
/// TAGS: the title goes in moov/udta/meta(hdlr "mdir")/ilst/"©nam" (the iTunes-style tag ffmpeg and Android's own
/// metadata reader both read; an existing ilst keeps its other tags); the creation time is written into the creation and modification times of mvhd, every
/// tkhd and every mdhd (seconds since 1904-01-01 UTC).
/// </para>
/// <para>
/// Fragmented files (with "moof" boxes) and files without moov or mdat are left untouched.
/// </para>
/// </remarks>
internal static class Mp4Rewriter
{
    private static readonly DateTimeOffset Epoch1904 = new DateTimeOffset(1904, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>What to do to the file.</summary>
    internal sealed class Options
    {
        /// <summary>Put moov before the first mdat.</summary>
        public bool MoveIndexToFront { get; set; }

        /// <summary>A title tag to add, or null.</summary>
        public string Title { get; set; }

        /// <summary>A creation time to write, or null.</summary>
        public DateTimeOffset? CreationTime { get; set; }
    }

    /// <summary>True when moov sits before the first mdat (or the file has no mdat).</summary>
    internal static bool IsIndexAtFront(string path)
    {
        using FileStream stream = File.OpenRead(path);
        List<Mp4TopLevelBox> boxes = Mp4File.ReadTopLevel(stream);
        int moov = Mp4File.IndexOf(boxes, "moov");
        int mdat = Mp4File.IndexOf(boxes, "mdat");
        return moov >= 0 && (mdat < 0 || moov < mdat);
    }

    /// <summary>Applies the options to a file in place.</summary>
    /// <returns>True when the file was rewritten; false when there was nothing to do or the file is not a plain MP4.</returns>
    internal static bool Rewrite(string path, Options options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        bool editTags = !string.IsNullOrEmpty(options.Title) || options.CreationTime.HasValue;

        List<Mp4TopLevelBox> boxes;
        byte[] moovBytes;
        using (FileStream source = File.OpenRead(path))
        {
            boxes = Mp4File.ReadTopLevel(source);
            int moovIndex = Mp4File.IndexOf(boxes, "moov");
            int mdatIndex = Mp4File.IndexOf(boxes, "mdat");
            if (moovIndex < 0 || mdatIndex < 0 || Mp4File.IndexOf(boxes, "moof") >= 0) return false;

            bool move = options.MoveIndexToFront && moovIndex > mdatIndex;
            if (!move && !editTags) return false;
            moovBytes = Mp4File.ReadBox(source, boxes[moovIndex]);
        }

        Mp4Node moov = Mp4Node.Parse(moovBytes);
        if (!string.IsNullOrEmpty(options.Title)) AddTitle(moov, options.Title);
        if (options.CreationTime.HasValue) SetCreationTime(moov, options.CreationTime.Value);

        int moovAt = Mp4File.IndexOf(boxes, "moov");
        bool moveIndex = options.MoveIndexToFront && moovAt > Mp4File.IndexOf(boxes, "mdat");
        List<int> order = Layout(boxes, moveIndex);
        PatchChunkOffsets(moov, boxes, order);

        string temporary = path + ".rewrite.tmp";
        try
        {
            using (FileStream source = File.OpenRead(path))
            using (FileStream destination = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[1 << 20];
                foreach (int index in order)
                {
                    Mp4TopLevelBox box = boxes[index];
                    if (box.Type == "moov")
                    {
                        destination.Write(moov.ToArray());
                        continue;
                    }

                    source.Position = box.Offset;
                    long remaining = box.Size;
                    while (remaining > 0)
                    {
                        int read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                        if (read == 0) throw new EndOfStreamException("The file ended inside a box.");
                        destination.Write(buffer, 0, read);
                        remaining -= read;
                    }
                }
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }

        return true;
    }

    /// <summary>
    /// The new order of the top-level boxes (indexes into <paramref name="boxes" />): moov before the first mdat when
    /// moving (dropping top-level free / skip padding), else the original order.
    /// </summary>
    internal static List<int> Layout(IReadOnlyList<Mp4TopLevelBox> boxes, bool moveIndexToFront)
    {
        var order = new List<int>();
        if (!moveIndexToFront)
        {
            for (int i = 0; i < boxes.Count; i++) order.Add(i);
            return order;
        }

        int moov = Mp4File.IndexOf(boxes, "moov");
        int firstMdat = Mp4File.IndexOf(boxes, "mdat");
        for (int i = 0; i < boxes.Count; i++)
        {
            if (i == moov || IsPadding(boxes[i].Type)) continue;
            if (i == firstMdat) order.Add(moov);
            order.Add(i);
        }

        return order;
    }

    /// <summary>
    /// Rewrites every chunk offset in moov for the new layout, upgrading stco to co64 where an offset no longer fits
    /// 32 bits, until the layout (which depends on moov's own size) is stable.
    /// </summary>
    internal static void PatchChunkOffsets(Mp4Node moov, IReadOnlyList<Mp4TopLevelBox> boxes, IReadOnlyList<int> order)
    {
        var tables = new List<Mp4Node>();
        foreach (Mp4Node table in moov.Descendants("stco")) tables.Add(table);
        foreach (Mp4Node table in moov.Descendants("co64")) tables.Add(table);
        var originals = new List<ulong[]>();
        foreach (Mp4Node table in tables) originals.Add((ulong[])table.ChunkOffsets.Clone());

        for (int pass = 0; pass < 4; pass++)
        {
            long moovSize = moov.Size;
            Dictionary<int, long> newOffsets = NewOffsets(boxes, order, moovSize);
            bool upgraded = false;

            for (int t = 0; t < tables.Count; t++)
            {
                ulong[] source = originals[t];
                ulong[] patched = new ulong[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    int holder = FindHolder(boxes, source[i]);
                    if (holder < 0 || boxes[holder].Type == "moov")
                        throw new InvalidDataException($"Chunk offset {source[i]} does not point into a media box.");
                    if (!newOffsets.TryGetValue(holder, out long moved))
                        throw new InvalidDataException($"Chunk offset {source[i]} points into a box the new layout drops.");
                    long value = (long)source[i] + (moved - boxes[holder].Offset);
                    patched[i] = (ulong)value;
                }

                tables[t].ChunkOffsets = patched;
                if (!tables[t].Use64BitOffsets && Array.Exists(patched, value => value > uint.MaxValue))
                {
                    tables[t].Use64BitOffsets = true;
                    upgraded = true;
                }
            }

            if (!upgraded && moov.Size == moovSize) return;
        }

        throw new InvalidDataException("The MP4 layout did not settle.");
    }

    private static Dictionary<int, long> NewOffsets(IReadOnlyList<Mp4TopLevelBox> boxes, IReadOnlyList<int> order, long moovSize)
    {
        var offsets = new Dictionary<int, long>();
        long position = 0;
        foreach (int index in order)
        {
            offsets[index] = position;
            position += boxes[index].Type == "moov" ? moovSize : boxes[index].Size;
        }

        return offsets;
    }

    private static int FindHolder(IReadOnlyList<Mp4TopLevelBox> boxes, ulong offset)
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            if ((ulong)boxes[i].Offset <= offset && offset < (ulong)boxes[i].End) return i;
        }

        return -1;
    }

    private static bool IsPadding(string type) => type == "free" || type == "skip";

    /// <summary>
    /// Sets moov/udta/meta(hdlr "mdir")/ilst/"©nam" to a UTF-8 title: creating udta, meta and ilst as needed, and
    /// replacing an existing "©nam" while keeping every other tag.
    /// </summary>
    internal static void AddTitle(Mp4Node moov, string title)
    {
        Mp4Node udta = moov.Child("udta");
        if (udta == null)
        {
            udta = Mp4Node.CreateContainer("udta");
            moov.Children.Add(udta);
        }

        byte[] text = Encoding.UTF8.GetBytes(title);
        byte[] dataPayload = new byte[8 + text.Length];
        BinaryPrimitives.WriteUInt32BigEndian(dataPayload, 1); // well-known type 1 = UTF-8
        text.CopyTo(dataPayload, 8);
        byte[] data = Mp4Node.CreateLeaf("data", dataPayload).Raw;
        byte[] name = Concat(Header("\u00A9nam", 8 + data.Length), data);

        int metaIndex = udta.Children.FindIndex(child => child.Type == "meta" && child.Raw != null);
        if (metaIndex >= 0)
        {
            // An existing meta (a full box: version / flags, then hdlr, ilst, ...): replace or add ©nam in its ilst.
            byte[] meta = udta.Children[metaIndex].Raw;
            if (meta.Length < 12) return;
            List<Mp4Node> metaChildren = Mp4Node.ParseChildren(meta.AsSpan(12));
            int ilstIndex = metaChildren.FindIndex(child => child.Type == "ilst");
            var items = new List<byte[]>();
            if (ilstIndex >= 0)
            {
                foreach (Mp4Node item in Mp4Node.ParseChildren(metaChildren[ilstIndex].Raw.AsSpan(8)))
                {
                    if (item.Type != "\u00A9nam") items.Add(item.ToArray());
                }
            }

            items.Insert(0, name);
            byte[] ilstPayload = items.SelectMany(item => item).ToArray();
            Mp4Node newIlst = Mp4Node.CreateLeaf("ilst", ilstPayload);
            if (ilstIndex >= 0) metaChildren[ilstIndex] = newIlst;
            else metaChildren.Add(newIlst);

            byte[] children = metaChildren.SelectMany(child => child.ToArray()).ToArray();
            byte[] metaPayload = new byte[4 + children.Length];
            Buffer.BlockCopy(meta, 8, metaPayload, 0, 4);
            children.CopyTo(metaPayload, 4);
            udta.Children[metaIndex] = Mp4Node.CreateLeaf("meta", metaPayload);
            return;
        }

        byte[] ilst = Concat(Header("ilst", 8 + name.Length), name);
        byte[] hdlrPayload = new byte[4 + 4 + 4 + 12 + 1];
        Encoding.Latin1.GetBytes("mdir", hdlrPayload.AsSpan(8, 4));
        Encoding.Latin1.GetBytes("appl", hdlrPayload.AsSpan(12, 4));
        byte[] hdlr = Mp4Node.CreateLeaf("hdlr", hdlrPayload).Raw;

        byte[] newMetaPayload = new byte[4 + hdlr.Length + ilst.Length]; // version / flags 0, then hdlr, ilst
        hdlr.CopyTo(newMetaPayload, 4);
        ilst.CopyTo(newMetaPayload, 4 + hdlr.Length);
        udta.Children.Add(Mp4Node.CreateLeaf("meta", newMetaPayload));
    }

    /// <summary>Writes a creation (and modification) time into mvhd, every tkhd and every mdhd.</summary>
    internal static void SetCreationTime(Mp4Node moov, DateTimeOffset when)
    {
        double seconds = Math.Floor((when.ToUniversalTime() - Epoch1904).TotalSeconds);
        ulong value = seconds <= 0 ? 0UL : (ulong)seconds;

        PatchTime(moov.Child("mvhd"), value);
        foreach (Mp4Node box in moov.Descendants("tkhd")) PatchTime(box, value);
        foreach (Mp4Node box in moov.Descendants("mdhd")) PatchTime(box, value);
    }

    private static void PatchTime(Mp4Node box, ulong value)
    {
        if (box?.Raw == null || box.Raw.Length < 20) return;
        byte[] raw = box.Raw;
        byte version = raw[8];
        if (version == 1)
        {
            if (raw.Length < 28) return;
            BinaryPrimitives.WriteUInt64BigEndian(raw.AsSpan(12), value);
            BinaryPrimitives.WriteUInt64BigEndian(raw.AsSpan(20), value);
        }
        else
        {
            uint value32 = value > uint.MaxValue ? uint.MaxValue : (uint)value;
            BinaryPrimitives.WriteUInt32BigEndian(raw.AsSpan(12), value32);
            BinaryPrimitives.WriteUInt32BigEndian(raw.AsSpan(16), value32);
        }
    }

    private static byte[] Header(string type, int size)
    {
        byte[] header = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)size);
        Encoding.Latin1.GetBytes(type, header.AsSpan(4, 4));
        return header;
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        byte[] result = new byte[a.Length + b.Length];
        a.CopyTo(result, 0);
        b.CopyTo(result, a.Length);
        return result;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
