using System;
using CodeBrix.VideoProcessing.Core.Frames;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class VideoFrameDataTests
{
    private static VideoPlane Plane(int rowLength, int rows, byte fill = 0)
    {
        byte[] data = new byte[rowLength * rows];
        Array.Fill(data, fill);
        return VideoPlane.FromArray(data, rowLength, rows);
    }

    [Fact]
    public void CreateI420_rounds_chroma_up_for_odd_sizes()
    {
        //Act
        var frame = VideoFrameData.CreateI420(5, 3, Plane(5, 3), Plane(3, 2), Plane(3, 2), TimeSpan.Zero, 0);

        //Assert
        frame.Layout.Should().Be(VideoPixelLayout.I420);
        frame.U.RowLength.Should().Be(3);
        frame.V.Rows.Should().Be(2);
        frame.BitDepth.Should().Be(8);
        frame.HasAlpha.Should().BeFalse();
    }

    [Fact]
    public void CreateI420_rejects_a_wrong_chroma_size()
    {
        //Act
        var exception = Assert.Throws<ArgumentException>(() =>
            VideoFrameData.CreateI420(4, 4, Plane(4, 4), Plane(2, 2), Plane(4, 4), TimeSpan.Zero, 0));

        //Assert
        exception.ParamName.Should().Be("v");
    }

    [Fact]
    public void CreateI420_rejects_a_missing_plane()
        => Assert.Throws<ArgumentException>(() => VideoFrameData.CreateI420(4, 4, Plane(4, 4), default, Plane(2, 2), TimeSpan.Zero, 0));

    [Fact]
    public void CreateI420_accepts_an_alpha_plane_of_luma_size()
    {
        //Act
        var frame = VideoFrameData.CreateI420(4, 2, Plane(4, 2), Plane(2, 1), Plane(2, 1), TimeSpan.Zero, 0, Plane(4, 2, 255));

        //Assert
        frame.HasAlpha.Should().BeTrue();
        frame.A.GetRow(0)[0].Should().Be((byte)255);
    }

    [Fact]
    public void CreateI420_rejects_a_wrong_alpha_size()
        => Assert.Throws<ArgumentException>(() =>
            VideoFrameData.CreateI420(4, 2, Plane(4, 2), Plane(2, 1), Plane(2, 1), TimeSpan.Zero, 0, Plane(2, 1)));

    [Fact]
    public void CreateNv12_exposes_only_Y_and_UV()
    {
        //Act
        var frame = VideoFrameData.CreateNv12(6, 4, Plane(6, 4), Plane(6, 2), TimeSpan.Zero, 0);

        //Assert
        frame.Y.IsEmpty.Should().BeFalse();
        frame.UV.RowLength.Should().Be(6);
        frame.U.IsEmpty.Should().BeTrue();
        frame.V.IsEmpty.Should().BeTrue();
        frame.Rgba.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void CreateRgba_exposes_only_the_packed_plane()
    {
        //Act
        var frame = VideoFrameData.CreateRgba(3, 2, Plane(12, 2), TimeSpan.Zero, 0);

        //Assert
        frame.Rgba.RowLength.Should().Be(12);
        frame.Y.IsEmpty.Should().BeTrue();
        frame.HasAlpha.Should().BeTrue();
    }

    [Fact]
    public void CreateRgba_rejects_a_plane_without_four_bytes_per_pixel()
        => Assert.Throws<ArgumentException>(() => VideoFrameData.CreateRgba(3, 2, Plane(9, 2), TimeSpan.Zero, 0));

    [Theory]
    [InlineData(0, 2, 0, 0)]
    [InlineData(2, 0, 0, 0)]
    [InlineData(2, 2, -1, 0)]
    [InlineData(2, 2, 0, -1)]
    public void factories_reject_bad_frame_values(int width, int height, long ticks, long index)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            VideoFrameData.CreateRgba(width, height, Plane(8, 2), TimeSpan.FromTicks(ticks), index));

    [Fact]
    public void default_frame_is_empty()
    {
        //Arrange
        VideoFrameData frame = default;

        //Assert
        frame.IsEmpty.Should().BeTrue();
        frame.BitDepth.Should().Be(0);
        frame.HasAlpha.Should().BeFalse();
        frame.Copy().IsEmpty.Should().BeTrue();
        Assert.Throws<InvalidOperationException>(() => frame.WithPosition(TimeSpan.Zero, 0));
    }

    [Fact]
    public void Copy_is_deep_and_tightly_packed()
    {
        //Arrange
        byte[] y = { 10, 11, 0, 0, 12, 13 };
        var frame = VideoFrameData.CreateI420(2, 2, new VideoPlane(y, 4, 2, 2), Plane(1, 1, 7), Plane(1, 1, 8), TimeSpan.FromSeconds(1), 25);

        //Act
        VideoFrameData copy = frame.Copy();
        y[0] = 99;

        //Assert
        copy.Y.Stride.Should().Be(2);
        copy.Y.ToArray().Should().Equal(10, 11, 12, 13);
        copy.Timestamp.Should().Be(TimeSpan.FromSeconds(1));
        copy.FrameIndex.Should().Be(25);
    }

    [Fact]
    public void WithPosition_keeps_the_planes()
    {
        //Arrange
        var frame = VideoFrameData.CreateRgba(1, 1, Plane(4, 1, 5), TimeSpan.Zero, 0);

        //Act
        VideoFrameData moved = frame.WithPosition(TimeSpan.FromSeconds(2), 50);

        //Assert
        moved.FrameIndex.Should().Be(50);
        moved.Rgba.GetRow(0)[3].Should().Be((byte)5);
    }

    [Fact]
    public void CreateI420_at_ten_bits_needs_two_bytes_per_sample()
    {
        //Act
        var frame = VideoFrameData.CreateI420(5, 3, Plane(10, 3), Plane(6, 2), Plane(6, 2), TimeSpan.Zero, 0, bitDepth: 10);

        //Assert
        frame.BitDepth.Should().Be(10);
        frame.BytesPerSample.Should().Be(2);
        frame.Y.RowLength.Should().Be(10);
        frame.U.RowLength.Should().Be(6);
        frame.V.Rows.Should().Be(2);
    }

    [Fact]
    public void CreateI420_at_ten_bits_rejects_one_byte_planes()
    {
        //Act
        var exception = Assert.Throws<ArgumentException>(() =>
            VideoFrameData.CreateI420(4, 2, Plane(4, 2), Plane(2, 1), Plane(2, 1), TimeSpan.Zero, 0, bitDepth: 10));

        //Assert
        exception.ParamName.Should().Be("y");
    }

    [Fact]
    public void CreateI420_alpha_has_the_frame_bit_depth()
    {
        //Act
        var frame = VideoFrameData.CreateI420(2, 2, Plane(4, 2), Plane(2, 1), Plane(2, 1), TimeSpan.Zero, 0, Plane(4, 2), 12);

        //Assert
        frame.HasAlpha.Should().BeTrue();
        frame.A.RowLength.Should().Be(4);
        Assert.Throws<ArgumentException>(() =>
            VideoFrameData.CreateI420(2, 2, Plane(4, 2), Plane(2, 1), Plane(2, 1), TimeSpan.Zero, 0, Plane(2, 2), 12));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(16)]
    public void factories_reject_unsupported_bit_depths(int bitDepth)
    {
        //Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            VideoFrameData.CreateGray(2, 2, Plane(4, 2), TimeSpan.Zero, 0, default, bitDepth));

        //Assert
        exception.ParamName.Should().Be("bitDepth");
    }

    [Fact]
    public void CreateNv12_at_twelve_bits_doubles_the_interleaved_plane()
    {
        //Act
        var frame = VideoFrameData.CreateNv12(5, 3, Plane(10, 3), Plane(12, 2), TimeSpan.Zero, 0, bitDepth: 12);

        //Assert
        frame.BitDepth.Should().Be(12);
        frame.UV.RowLength.Should().Be(12);
        frame.U.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void CreateI444_has_full_size_chroma()
    {
        //Act
        var frame = VideoFrameData.CreateI444(5, 3, Plane(5, 3, 1), Plane(5, 3, 2), Plane(5, 3, 3), TimeSpan.Zero, 0);

        //Assert
        frame.Layout.Should().Be(VideoPixelLayout.I444);
        frame.BitDepth.Should().Be(8);
        frame.Y.GetRow(0)[0].Should().Be((byte)1);
        frame.U.GetRow(2)[4].Should().Be((byte)2);
        frame.V.GetRow(2)[4].Should().Be((byte)3);
        frame.UV.IsEmpty.Should().BeTrue();
        frame.Rgba.IsEmpty.Should().BeTrue();
        frame.HasAlpha.Should().BeFalse();
    }

    [Fact]
    public void CreateI444_rejects_half_size_chroma()
    {
        //Act
        var exception = Assert.Throws<ArgumentException>(() =>
            VideoFrameData.CreateI444(4, 4, Plane(4, 4), Plane(2, 2), Plane(4, 4), TimeSpan.Zero, 0));

        //Assert
        exception.ParamName.Should().Be("u");
    }

    [Fact]
    public void CreateI444_at_ten_bits_with_alpha()
    {
        //Act
        var frame = VideoFrameData.CreateI444(3, 2, Plane(6, 2), Plane(6, 2), Plane(6, 2), TimeSpan.Zero, 0, Plane(6, 2, 255), 10);

        //Assert
        frame.BitDepth.Should().Be(10);
        frame.BytesPerSample.Should().Be(2);
        frame.HasAlpha.Should().BeTrue();
        frame.V.RowLength.Should().Be(6);
    }

    [Fact]
    public void CreateGray_exposes_only_Y_and_A()
    {
        //Act
        var frame = VideoFrameData.CreateGray(4, 2, Plane(4, 2, 9), TimeSpan.Zero, 0, Plane(4, 2, 200));

        //Assert
        frame.Layout.Should().Be(VideoPixelLayout.Gray);
        frame.Y.GetRow(1)[3].Should().Be((byte)9);
        frame.A.GetRow(1)[3].Should().Be((byte)200);
        frame.U.IsEmpty.Should().BeTrue();
        frame.V.IsEmpty.Should().BeTrue();
        frame.UV.IsEmpty.Should().BeTrue();
        frame.Rgba.IsEmpty.Should().BeTrue();
        frame.HasAlpha.Should().BeTrue();
    }

    [Fact]
    public void CreateGray_rejects_a_missing_or_wrong_luma_plane()
    {
        //Act + Assert
        Assert.Throws<ArgumentException>(() => VideoFrameData.CreateGray(4, 2, default, TimeSpan.Zero, 0));
        Assert.Throws<ArgumentException>(() => VideoFrameData.CreateGray(4, 2, Plane(4, 2), TimeSpan.Zero, 0, default, 10));
    }

    [Fact]
    public void Rgba_frames_are_always_eight_bit()
    {
        //Act
        var frame = VideoFrameData.CreateRgba(1, 1, Plane(4, 1), TimeSpan.Zero, 0);

        //Assert
        frame.BitDepth.Should().Be(8);
        frame.BytesPerSample.Should().Be(1);
    }

    [Fact]
    public void default_frame_has_no_bytes_per_sample()
        => default(VideoFrameData).BytesPerSample.Should().Be(0);

    [Fact]
    public void Copy_keeps_layout_bit_depth_and_alpha()
    {
        //Arrange
        byte[] y = { 1, 0, 2, 0, 9, 9, 3, 0, 4, 0 };
        var frame = VideoFrameData.CreateGray(2, 2, new VideoPlane(y, 6, 4, 2), TimeSpan.FromSeconds(1), 7, Plane(4, 2, 255), 12);

        //Act
        VideoFrameData copy = frame.Copy();
        y[0] = 99;

        //Assert
        copy.Layout.Should().Be(VideoPixelLayout.Gray);
        copy.BitDepth.Should().Be(12);
        copy.Y.Stride.Should().Be(4);
        copy.Y.ToArray().Should().Equal(1, 0, 2, 0, 3, 0, 4, 0);
        copy.A.GetRow(0)[0].Should().Be((byte)255);
        copy.FrameIndex.Should().Be(7);
    }

    [Fact]
    public void Copy_of_an_i444_frame_keeps_every_plane()
    {
        //Arrange
        var frame = VideoFrameData.CreateI444(2, 1, Plane(4, 1, 1), Plane(4, 1, 2), Plane(4, 1, 3), TimeSpan.Zero, 0, default, 10);

        //Act
        VideoFrameData copy = frame.Copy();

        //Assert
        copy.Layout.Should().Be(VideoPixelLayout.I444);
        copy.BitDepth.Should().Be(10);
        copy.U.GetRow(0)[3].Should().Be((byte)2);
        copy.V.GetRow(0)[3].Should().Be((byte)3);
    }

    [Fact]
    public void WithPosition_keeps_the_bit_depth()
    {
        //Arrange
        var frame = VideoFrameData.CreateGray(1, 1, Plane(2, 1), TimeSpan.Zero, 0, default, 10);

        //Act
        VideoFrameData moved = frame.WithPosition(TimeSpan.FromSeconds(1), 25);

        //Assert
        moved.BitDepth.Should().Be(10);
        moved.Layout.Should().Be(VideoPixelLayout.Gray);
        moved.FrameIndex.Should().Be(25);
    }

    [Fact]
    public void ToString_names_the_layout_and_bit_depth()
    {
        //Arrange
        var frame = VideoFrameData.CreateI444(2, 2, Plane(4, 2), Plane(4, 2), Plane(4, 2), TimeSpan.Zero, 3, default, 10);

        //Act
        string text = frame.ToString();

        //Assert
        text.Should().Contain("I444");
        text.Should().Contain("2x2");
        text.Should().Contain("10-bit");
        text.Should().Contain("#3");
    }
}
