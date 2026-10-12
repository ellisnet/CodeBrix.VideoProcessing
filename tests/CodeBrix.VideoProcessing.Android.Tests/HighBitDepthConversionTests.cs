using System;
using System.Collections.Generic;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core.Frames;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class HighBitDepthConversionTests
{
    // Writes a 10-bit value as a P010 word (value in the high 10 bits) with deliberate garbage in the low 6 bits, so a
    // copy that forgets the shift (or keeps the low bits) is caught.
    private static void PutP010(byte[] buffer, int at, int value)
    {
        int word = (value << 6) | ((value * 7 + 5) & 0x3F);
        buffer[at] = (byte)word;
        buffer[at + 1] = (byte)(word >> 8);
    }

    private static int Sample(byte[] buffer, int index) => buffer[2 * index] | (buffer[2 * index + 1] << 8);

    private static int Sample(ReadOnlySpan<byte> row, int index) => row[2 * index] | (row[2 * index + 1] << 8);

    // Known per-position patterns, distinct per plane, using the whole 10-bit range.
    private static int YValue(int x, int y) => (x * 97 + y * 389 + 3) % 1024;

    private static int UValue(int x, int y) => (x * 131 + y * 17 + 600) % 1024;

    private static int VValue(int x, int y) => (x * 53 + y * 211 + 77) % 1024;

    // A P010 picture in ONE buffer: Y rows `stride` bytes apart over `sliceHeight` rows, then interleaved Cb Cr rows at
    // the same stride. The pattern is placed at (left, top) inside a larger coded area.
    private static byte[] P010Picture(int width, int height, int stride, int sliceHeight, int left, int top, int offset)
    {
        int codedChromaRows = (sliceHeight + 1) / 2;
        byte[] buffer = new byte[offset + stride * sliceHeight + stride * codedChromaRows];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) PutP010(buffer, offset + (top + y) * stride + 2 * (left + x), YValue(x, y));
        }

        int chromaBase = offset + stride * sliceHeight;
        for (int y = 0; y < (height + 1) / 2; y++)
        {
            for (int x = 0; x < (width + 1) / 2; x++)
            {
                int at = chromaBase + (top / 2 + y) * stride + 4 * (left / 2 + x);
                PutP010(buffer, at, UValue(x, y));
                PutP010(buffer, at + 2, VValue(x, y));
            }
        }

        return buffer;
    }

    private static void ShouldHoldThePattern(byte[] i420, int width, int height)
    {
        int chromaWidth = (width + 1) / 2;
        int chromaHeight = (height + 1) / 2;
        int luma = width * height;
        int chroma = chromaWidth * chromaHeight;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) Sample(i420, y * width + x).Should().Be(YValue(x, y));
        }

        for (int y = 0; y < chromaHeight; y++)
        {
            for (int x = 0; x < chromaWidth; x++)
            {
                Sample(i420, luma + y * chromaWidth + x).Should().Be(UValue(x, y));
                Sample(i420, luma + chroma + y * chromaWidth + x).Should().Be(VValue(x, y));
            }
        }
    }

    [Theory]
    [InlineData(8, 0, "EightBit")]
    [InlineData(8, 10, "EightBit")]
    [InlineData(10, 8, "WidenEightBit")]
    [InlineData(10, 10, "P010")]
    [InlineData(10, 0, "P010")]
    public void the_decoder_mode_follows_the_requested_and_source_depth(int requested, int source, string expected)
        => HighBitDepthConversion.ChooseMode(requested, source).ToString().Should().Be(expected);

    [Theory]
    [InlineData(8, 10, true)]
    [InlineData(8, 8, false)]
    [InlineData(8, 0, false)]
    [InlineData(10, 10, true)]
    [InlineData(10, 0, true)]
    [InlineData(10, 8, false)]
    public void p010_output_is_asked_for_ten_bit_frames_and_for_eight_bit_frames_of_a_ten_bit_stream(int requested, int source, bool expected)
        => HighBitDepthConversion.AsksForP010(HighBitDepthConversion.ChooseMode(requested, source), source).Should().Be(expected);

    [Fact]
    public void twelve_bits_are_refused_by_the_mode_choice()
    {
        //Assert
        Assert.Throws<NotSupportedException>(() => HighBitDepthConversion.ChooseMode(12, 10)).Message.Should().Contain("12-bit");
        Assert.Throws<NotSupportedException>(() => HighBitDepthConversion.ChooseMode(10, 12)).Message.Should().Contain("12-bit stream");
    }

    [Fact]
    public void a_p010_word_becomes_the_low_bits_sample()
    {
        //Arrange - 0, 1, 512, 1023 as P010 words with garbage in the low 6 bits
        int[] values = { 0, 1, 512, 1023 };
        byte[] row = new byte[2 * values.Length];
        for (int i = 0; i < values.Length; i++) PutP010(row, 2 * i, values[i]);
        byte[] result = new byte[2 * values.Length];

        //Act
        HighBitDepthConversion.ConvertRow(row, 2, 2, values.Length, result);

        //Assert - little-endian, value in the LOW 10 bits, top 6 bits zero
        result.Should().Equal(0x00, 0x00, 0x01, 0x00, 0x00, 0x02, 0xFF, 0x03);
    }

    [Fact]
    public void an_eight_bit_sample_is_widened_by_two_bits()
    {
        //Arrange
        byte[] source = { 0, 16, 128, 235, 255 };
        byte[] result = new byte[10];

        //Act
        HighBitDepthConversion.Widen(source, result);

        //Assert
        for (int i = 0; i < source.Length; i++) Sample(result, i).Should().Be(source[i] << 2);
        Sample(result, 1).Should().Be(64);
        Sample(result, 3).Should().Be(940);
    }

    [Fact]
    public void a_planar_p010_plane_with_row_padding_and_an_odd_width_is_read_exactly()
    {
        //Arrange - 5 x 3 samples, rows 16 bytes apart (6 bytes of padding), starting 7 bytes into the buffer
        const int width = 5, height = 3, stride = 16, offset = 7;
        byte[] source = new byte[offset + stride * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) PutP010(source, offset + y * stride + 2 * x, YValue(x, y));
            for (int pad = 2 * width; pad < stride && offset + y * stride + pad < source.Length; pad++) source[offset + y * stride + pad] = 0xEE;
        }

        byte[] result = new byte[2 * width * height];

        //Act
        HighBitDepthConversion.ReadPlane(source, offset, stride, 2, 2, 0, 0, width, height, result, 0, width);

        //Assert
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) Sample(result, y * width + x).Should().Be(YValue(x, y));
        }
    }

    [Fact]
    public void semi_planar_p010_chroma_is_split_into_cb_and_cr()
    {
        //Arrange - 3 chroma samples per row, Cb Cr interleaved (pixel stride 4), rows 20 bytes apart
        const int width = 3, height = 2, stride = 20;
        byte[] source = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                PutP010(source, y * stride + 4 * x, UValue(x, y));
                PutP010(source, y * stride + 4 * x + 2, VValue(x, y));
            }
        }

        byte[] u = new byte[2 * width * height];
        byte[] v = new byte[2 * width * height];

        //Act - the platform's U plane starts at the first Cb, its V plane two bytes later
        HighBitDepthConversion.ReadPlane(source, 0, stride, 4, 2, 0, 0, width, height, u, 0, width);
        HighBitDepthConversion.ReadPlane(source, 2, stride, 4, 2, 0, 0, width, height, v, 0, width);

        //Assert
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Sample(u, y * width + x).Should().Be(UValue(x, y));
                Sample(v, y * width + x).Should().Be(VValue(x, y));
            }
        }
    }

    [Fact]
    public void a_last_row_without_its_padding_is_still_read()
    {
        //Arrange - the buffer ends right after the last sample of the last row (Image planes often do)
        const int width = 3, height = 2, stride = 12;
        byte[] source = new byte[stride * (height - 1) + 2 * width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) PutP010(source, y * stride + 2 * x, YValue(x, y));
        }

        byte[] result = new byte[2 * width * height];

        //Act
        HighBitDepthConversion.ReadPlane(source, 0, stride, 2, 2, 0, 0, width, height, result, 0, width);

        //Assert
        Sample(result, width * height - 1).Should().Be(YValue(width - 1, height - 1));
    }

    [Fact]
    public void an_eight_bit_semi_planar_plane_is_widened_on_read()
    {
        //Arrange - 8-bit interleaved chroma (pixel stride 2), the 8-bit-source path at 10 bits
        byte[] source = { 16, 99, 128, 99, 240, 99 };
        byte[] result = new byte[6];

        //Act
        HighBitDepthConversion.ReadPlane(source, 0, 6, 2, 1, 0, 0, 3, 1, result, 0, 3);

        //Assert
        Sample(result, 0).Should().Be(64);
        Sample(result, 1).Should().Be(512);
        Sample(result, 2).Should().Be(960);
    }

    [Theory]
    [InlineData(4, 2, 8, 2, 0, 0, 0)]
    [InlineData(5, 3, 16, 4, 0, 0, 0)]
    [InlineData(7, 5, 32, 8, 2, 2, 0)]
    [InlineData(5, 3, 24, 6, 0, 0, 13)]
    public void a_p010_buffer_is_read_into_ten_bit_i420(int width, int height, int stride, int sliceHeight, int left, int top, int offset)
    {
        //Arrange
        byte[] buffer = P010Picture(width, height, stride, sliceHeight, left, top, offset);
        byte[] i420 = new byte[HighBitDepthConversion.I420Size16(width, height)];

        //Act
        HighBitDepthConversion.ReadP010Buffer(buffer, offset, stride, sliceHeight, left, top, width, height, i420);

        //Assert
        ShouldHoldThePattern(i420, width, height);
    }

    [Fact]
    public void a_p010_stride_given_in_samples_is_doubled()
    {
        //Arrange - a stride of 8 SAMPLES (16 bytes) reported as 8
        const int width = 6, height = 4;
        byte[] buffer = P010Picture(width, height, 16, height, 0, 0, 0);
        byte[] i420 = new byte[HighBitDepthConversion.I420Size16(width, height)];

        //Act
        HighBitDepthConversion.ReadP010Buffer(buffer, 0, 8, height, 0, 0, width, height, i420);

        //Assert
        ShouldHoldThePattern(i420, width, height);
    }

    [Fact]
    public void gray_from_p010_is_the_ten_bit_luma_plane()
    {
        //Arrange
        const int width = 5, height = 3;
        byte[] i420 = new byte[HighBitDepthConversion.I420Size16(width, height)];
        HighBitDepthConversion.ReadP010Buffer(P010Picture(width, height, 12, height, 0, 0, 0), 0, 12, height, 0, 0, width, height, i420);

        //Act - the frame source's Gray frame: the first 2 * width * height bytes
        VideoFrameData gray = VideoFrameData.CreateGray(width, height,
            new VideoPlane(new ReadOnlyMemory<byte>(i420, 0, 2 * width * height), 2 * width, 2 * width, height), TimeSpan.Zero, 0, default, 10);

        //Assert
        gray.BitDepth.Should().Be(10);
        gray.BytesPerSample.Should().Be(2);
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<byte> row = gray.Y.GetRow(y);
            for (int x = 0; x < width; x++) Sample(row, x).Should().Be(YValue(x, y));
        }
    }

    [Fact]
    public void ten_bit_nv12_keeps_cb_and_cr_interleaved_sample_by_sample()
    {
        //Arrange - an odd-sized picture
        const int width = 7, height = 5;
        int chromaWidth = (width + 1) / 2, chromaHeight = (height + 1) / 2;
        byte[] i420 = new byte[HighBitDepthConversion.I420Size16(width, height)];
        HighBitDepthConversion.ReadP010Buffer(P010Picture(width, height, 16, height, 0, 0, 0), 0, 16, height, 0, 0, width, height, i420);
        byte[] nv12 = new byte[i420.Length];

        //Act
        HighBitDepthConversion.I420ToNv12(i420, width, height, nv12);
        int luma = 2 * width * height;
        VideoFrameData frame = VideoFrameData.CreateNv12(width, height,
            new VideoPlane(new ReadOnlyMemory<byte>(nv12, 0, luma), 2 * width, 2 * width, height),
            new VideoPlane(new ReadOnlyMemory<byte>(nv12, luma, 4 * chromaWidth * chromaHeight), 4 * chromaWidth, 4 * chromaWidth, chromaHeight),
            TimeSpan.Zero, 0, default, 10);

        //Assert
        frame.Y.ToArray().Should().Equal(new ReadOnlySpan<byte>(i420, 0, luma).ToArray());
        for (int y = 0; y < chromaHeight; y++)
        {
            ReadOnlySpan<byte> row = frame.UV.GetRow(y);
            for (int x = 0; x < chromaWidth; x++)
            {
                Sample(row, 2 * x).Should().Be(UValue(x, y));
                Sample(row, 2 * x + 1).Should().Be(VValue(x, y));
            }
        }
    }

    [Fact]
    public void the_ten_bit_buffer_splits_into_a_valid_i420_frame()
    {
        //Arrange
        const int width = 5, height = 3;
        int chromaRow = 2 * ((width + 1) / 2);
        int chroma = chromaRow * ((height + 1) / 2);
        var memory = new ReadOnlyMemory<byte>(new byte[HighBitDepthConversion.I420Size16(width, height)]);

        //Act
        VideoFrameData frame = VideoFrameData.CreateI420(width, height,
            new VideoPlane(memory.Slice(0, 2 * width * height), 2 * width, 2 * width, height),
            new VideoPlane(memory.Slice(2 * width * height, chroma), chromaRow, chromaRow, (height + 1) / 2),
            new VideoPlane(memory.Slice(2 * width * height + chroma, chroma), chromaRow, chromaRow, (height + 1) / 2),
            TimeSpan.Zero, 0, default, 10);

        //Assert
        frame.BitDepth.Should().Be(10);
        HighBitDepthConversion.I420Size16(width, height).Should().Be(2 * (15 + 2 * 6));
    }

    [Fact]
    public void the_synthetic_clip_ramp_needs_ten_bits_and_reads_back_exactly_through_p010()
    {
        Assert.SkipUnless(HostTools.FFmpegAvailable, "ffmpeg is not on PATH");

        //Arrange - the host decode of frame 0 as yuv420p10le (the reference) and as p010le (what a decoder hands over)
        const int width = 320, height = 180;
        byte[] reference = HostTools.FirstFrame(HostTools.Clip10, "yuv420p10le");
        byte[] p010 = HostTools.FirstFrame(HostTools.Clip10, "p010le");
        byte[] i420 = new byte[HighBitDepthConversion.I420Size16(width, height)];

        //Act
        HighBitDepthConversion.ReadP010Buffer(p010, 0, 2 * width, height, 0, 0, width, height, i420);

        //Assert - bit-exact against the reference, and the ramp resolves 960 levels (an 8-bit cut would give 240)
        i420.Should().Equal(reference);
        var levels = new HashSet<int>();
        var cut = new HashSet<int>();
        foreach (int row in new[] { 30, 90, 150 })
        {
            for (int x = 0; x < width; x++)
            {
                int value = Sample(i420, row * width + x);
                levels.Add(value);
                cut.Add(value >> 2);
            }
        }

        levels.Count.Should().Be(960);
        cut.Count.Should().Be(240);
        Sample(i420, 30 * width).Should().Be(64);
        Sample(i420, 150 * width + width - 1).Should().Be(1023);
    }
}
