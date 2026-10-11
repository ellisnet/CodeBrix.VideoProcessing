using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodeBrix.VideoProcessing.Android.Managed;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class Mp4RewriterTests
{
    // ---------------------------------------------------------------- hand-built files (no ffmpeg needed)

    private static byte[] Box(string type, params byte[][] payloads)
    {
        int size = 8 + payloads.Sum(p => p.Length);
        byte[] box = new byte[size];
        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)size);
        Encoding.Latin1.GetBytes(type, box.AsSpan(4, 4));
        int position = 8;
        foreach (byte[] payload in payloads)
        {
            payload.CopyTo(box, position);
            position += payload.Length;
        }

        return box;
    }

    private static byte[] ChunkOffsets(bool co64, params ulong[] offsets)
    {
        byte[] payload = new byte[8 + offsets.Length * (co64 ? 8 : 4)];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4), (uint)offsets.Length);
        for (int i = 0; i < offsets.Length; i++)
        {
            if (co64) BinaryPrimitives.WriteUInt64BigEndian(payload.AsSpan(8 + i * 8), offsets[i]);
            else BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8 + i * 4), (uint)offsets[i]);
        }

        return Box(co64 ? "co64" : "stco", payload);
    }

    private static byte[] Mvhd()
    {
        byte[] payload = new byte[100];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12), 1000);
        return Box("mvhd", payload);
    }

    private static byte[] Moov(params byte[][] offsetTables) =>
        Box("moov", Mvhd(), Box("trak", Box("mdia", Box("minf", Box("stbl", offsetTables)))));

    // ftyp, free, mdat (three distinct 16-byte chunks), moov LAST - the layout MediaMuxer leaves when its reserved
    // space was too small. Returns the file and the original chunk contents.
    private static (byte[] File, byte[][] Chunks) MoovLastFile(bool co64, bool secondTrack)
    {
        byte[] ftyp = Box("ftyp", Encoding.ASCII.GetBytes("isom\0\0\u0002\0isomiso2"));
        byte[] free = Box("free", new byte[40]);
        byte[][] chunks = Enumerable.Range(0, 3).Select(i => Enumerable.Repeat((byte)(0x41 + i), 16).ToArray()).ToArray();
        byte[] mdat = Box("mdat", chunks);
        long mdatPayload = ftyp.Length + free.Length + 8;
        ulong[] offsets = Enumerable.Range(0, 3).Select(i => (ulong)(mdatPayload + 16 * i)).ToArray();
        byte[] moov = secondTrack
            ? Box("moov", Mvhd(), Box("trak", Box("mdia", Box("minf", Box("stbl", ChunkOffsets(co64, offsets[0], offsets[1]))))),
                Box("trak", Box("mdia", Box("minf", Box("stbl", ChunkOffsets(!co64, offsets[2]))))))
            : Moov(ChunkOffsets(co64, offsets));
        return (ftyp.Concat(free).Concat(mdat).Concat(moov).ToArray(), chunks);
    }

    private static string Layout(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return string.Join(" ", Mp4File.ReadTopLevel(stream).Select(b => b.Type));
    }

    private static List<ulong> OffsetsIn(string path)
    {
        using FileStream stream = File.OpenRead(path);
        List<Mp4TopLevelBox> boxes = Mp4File.ReadTopLevel(stream);
        Mp4Node moov = Mp4Node.Parse(Mp4File.ReadBox(stream, boxes[Mp4File.IndexOf(boxes, "moov")]));
        return moov.Descendants("stco").Concat(moov.Descendants("co64")).SelectMany(t => t.ChunkOffsets).ToList();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void moving_the_index_to_the_front_keeps_every_chunk_offset_on_its_data(bool co64, bool twoTracks)
    {
        //Arrange
        string folder = HostTools.NewFolder();
        string path = Path.Combine(folder, "moov-last.mp4");
        (byte[] file, byte[][] chunks) = MoovLastFile(co64, twoTracks);
        File.WriteAllBytes(path, file);

        //Act
        bool rewritten = Mp4Rewriter.Rewrite(path, new Mp4Rewriter.Options { MoveIndexToFront = true });

        //Assert
        rewritten.Should().BeTrue();
        Layout(path).Should().Be("ftyp moov mdat");
        Mp4Rewriter.IsIndexAtFront(path).Should().BeTrue();
        byte[] result = File.ReadAllBytes(path);
        List<ulong> offsets = OffsetsIn(path);
        offsets.Count.Should().Be(3);
        for (int i = 0; i < 3; i++) result.AsSpan((int)offsets[i], 16).ToArray().Should().Equal(chunks[i]);
        result.Length.Should().Be(file.Length - 48);
        File.Exists(path + ".rewrite.tmp").Should().BeFalse();
        Directory.Delete(folder, true);
    }

    [Fact]
    public void nothing_is_rewritten_when_the_index_is_already_first_and_there_are_no_tags()
    {
        //Arrange
        string folder = HostTools.NewFolder();
        string path = Path.Combine(folder, "front.mp4");
        (byte[] file, _) = MoovLastFile(false, false);
        File.WriteAllBytes(path, file);
        Mp4Rewriter.Rewrite(path, new Mp4Rewriter.Options { MoveIndexToFront = true });
        byte[] before = File.ReadAllBytes(path);

        //Act
        bool rewritten = Mp4Rewriter.Rewrite(path, new Mp4Rewriter.Options { MoveIndexToFront = true });

        //Assert
        rewritten.Should().BeFalse();
        File.ReadAllBytes(path).Should().Equal(before);
        Directory.Delete(folder, true);
    }

    [Fact]
    public void tags_without_fast_start_keep_the_index_last_and_the_offsets_unchanged()
    {
        //Arrange
        string folder = HostTools.NewFolder();
        string path = Path.Combine(folder, "tags.mp4");
        (byte[] file, byte[][] chunks) = MoovLastFile(false, false);
        File.WriteAllBytes(path, file);

        //Act
        bool rewritten = Mp4Rewriter.Rewrite(path, new Mp4Rewriter.Options { Title = "x", CreationTime = DateTimeOffset.UnixEpoch });

        //Assert
        rewritten.Should().BeTrue();
        Layout(path).Should().Be("ftyp free mdat moov");
        byte[] result = File.ReadAllBytes(path);
        List<ulong> offsets = OffsetsIn(path);
        for (int i = 0; i < 3; i++) result.AsSpan((int)offsets[i], 16).ToArray().Should().Equal(chunks[i]);
        Directory.Delete(folder, true);
    }

    [Fact]
    public void offsets_that_pass_4_gib_are_upgraded_to_co64_and_the_layout_settles()
    {
        //Arrange - a fake layout: a huge mdat before moov, chunk offsets near the 32-bit limit.
        var boxes = new List<Mp4TopLevelBox>
        {
            new Mp4TopLevelBox("ftyp", 0, 24),
            new Mp4TopLevelBox("mdat", 24, 0x1_0000_0000L),
            new Mp4TopLevelBox("moov", 24 + 0x1_0000_0000L, 400)
        };
        Mp4Node moov = Mp4Node.Parse(Moov(ChunkOffsets(false, 32, 0xFFFF_FFF0)));
        List<int> order = Mp4Rewriter.Layout(boxes, moveIndexToFront: true);

        //Act
        Mp4Rewriter.PatchChunkOffsets(moov, boxes, order);

        //Assert
        order.Should().Equal(0, 2, 1);
        Mp4Node table = moov.Descendants("stco").Single();
        table.Use64BitOffsets.Should().BeTrue();
        ulong shift = (ulong)moov.Size;
        table.ChunkOffsets.Should().Equal(32 + shift, 0xFFFF_FFF0UL + shift);
        Mp4Node.Parse(moov.ToArray()).Descendants("co64").Single().ChunkOffsets.Should().Equal(table.ChunkOffsets);
    }

    [Fact]
    public void an_offset_outside_any_media_box_is_refused()
    {
        //Arrange
        var boxes = new List<Mp4TopLevelBox> { new Mp4TopLevelBox("ftyp", 0, 24), new Mp4TopLevelBox("mdat", 24, 100), new Mp4TopLevelBox("moov", 124, 300) };
        Mp4Node moov = Mp4Node.Parse(Moov(ChunkOffsets(false, 5000)));

        //Assert
        Assert.Throws<InvalidDataException>(() => Mp4Rewriter.PatchChunkOffsets(moov, boxes, Mp4Rewriter.Layout(boxes, true)));
    }

    [Fact]
    public void a_file_that_is_not_a_box_sequence_is_refused()
    {
        //Arrange
        string folder = HostTools.NewFolder();
        string path = Path.Combine(folder, "junk.mp4");
        File.WriteAllText(path, "this is plainly not an mp4 file");

        //Assert
        Assert.Throws<InvalidDataException>(() => Mp4Rewriter.Rewrite(path, new Mp4Rewriter.Options { MoveIndexToFront = true }));
        Directory.Delete(folder, true);
    }

    [Fact]
    public void the_title_box_tree_round_trips()
    {
        //Arrange
        Mp4Node moov = Mp4Node.Parse(Moov(ChunkOffsets(false, 0)));

        //Act
        Mp4Rewriter.AddTitle(moov, "Hello");
        Mp4Node reparsed = Mp4Node.Parse(moov.ToArray());

        //Assert
        byte[] meta = reparsed.Child("udta").Child("meta").Raw;
        Encoding.Latin1.GetString(meta).Should().Contain("mdir");
        Encoding.Latin1.GetString(meta).Should().Contain("©nam");
        Encoding.UTF8.GetString(meta).Should().EndWith("Hello");
    }

    // ---------------------------------------------------------------- real files from the host ffmpeg

    [Fact]
    public void a_real_moov_last_file_plays_identically_after_the_move()
    {
        Assert.SkipUnless(HostTools.FFmpegAvailable, "ffmpeg is not on PATH");

        //Arrange
        string folder = HostTools.NewFolder();
        string path = Path.Combine(folder, "real.mp4");
        HostTools.FFmpeg($"-f lavfi -i testsrc2=size=160x120:rate=25:duration=2 -f lavfi -i sine=frequency=500:duration=2 " +
                         $"-c:v libx264 -bf 2 -g 10 -pix_fmt yuv420p -c:a aac \"{path}\"");
        Layout(path).Should().EndWith("moov");
        string before = HostTools.VideoFrameHashes(path);

        //Act
        bool rewritten = Mp4Rewriter.Rewrite(path, new Mp4Rewriter.Options { MoveIndexToFront = true });

        //Assert
        rewritten.Should().BeTrue();
        Layout(path).Should().StartWith("ftyp moov");
        HostTools.VideoFrameHashes(path).Should().Be(before);
        HostTools.FFprobe($"-select_streams a -count_packets -show_entries stream=nb_read_packets -of csv=p=0 \"{path}\"").Trim().Should().NotBe("0");
        HostTools.FFmpeg($"-i \"{path}\" -f null -");
        Directory.Delete(folder, true);
    }

    [Fact]
    public void ffprobe_reads_the_title_and_creation_time_tags()
    {
        Assert.SkipUnless(HostTools.FFmpegAvailable, "ffmpeg is not on PATH");

        //Arrange
        string folder = HostTools.NewFolder();
        string path = Path.Combine(folder, "tags.mp4");
        HostTools.FFmpeg($"-f lavfi -i testsrc2=size=64x48:rate=10:duration=1 -c:v libx264 -pix_fmt yuv420p -map_metadata -1 \"{path}\"");
        string before = HostTools.VideoFrameHashes(path);

        //Act
        Mp4Rewriter.Rewrite(path, new Mp4Rewriter.Options
        {
            MoveIndexToFront = true,
            Title = "Gäste ♪ title",
            CreationTime = new DateTimeOffset(2026, 10, 11, 12, 34, 56, TimeSpan.Zero)
        });

        //Assert
        HostTools.FFprobe($"-show_entries format_tags=title -of default=nw=1:nk=1 \"{path}\"").Trim().Should().Be("Gäste ♪ title");
        HostTools.FFprobe($"-show_entries format_tags=creation_time -of default=nw=1:nk=1 \"{path}\"").Trim().Should().StartWith("2026-10-11T12:34:56");
        HostTools.VideoFrameHashes(path).Should().Be(before);
        Directory.Delete(folder, true);
    }

    [Fact]
    public void a_fragmented_file_is_left_alone()
    {
        Assert.SkipUnless(HostTools.FFmpegAvailable, "ffmpeg is not on PATH");

        //Arrange
        string folder = HostTools.NewFolder();
        string path = Path.Combine(folder, "fragmented.mp4");
        HostTools.FFmpeg($"-f lavfi -i testsrc2=size=64x48:rate=10:duration=1 -c:v libx264 -pix_fmt yuv420p -movflags frag_keyframe+empty_moov \"{path}\"");
        byte[] before = File.ReadAllBytes(path);

        //Act
        bool rewritten = Mp4Rewriter.Rewrite(path, new Mp4Rewriter.Options { MoveIndexToFront = true, Title = "x" });

        //Assert
        rewritten.Should().BeFalse();
        File.ReadAllBytes(path).Should().Equal(before);
        Directory.Delete(folder, true);
    }
}
