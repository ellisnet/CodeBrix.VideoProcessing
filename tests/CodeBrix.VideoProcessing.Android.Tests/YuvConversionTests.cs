using System;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class YuvConversionTests
{
    private static VideoPlane SolidRgba(int width, int height, byte r, byte g, byte b)
    {
        byte[] data = new byte[4 * width * height];
        for (int i = 0; i < width * height; i++)
        {
            data[4 * i] = r;
            data[4 * i + 1] = g;
            data[4 * i + 2] = b;
            data[4 * i + 3] = 255;
        }

        return VideoPlane.FromArray(data, 4 * width, height);
    }

    [Theory]
    [InlineData(255, 255, 255, 235, 128, 128)]
    [InlineData(0, 0, 0, 16, 128, 128)]
    [InlineData(255, 0, 0, 63, 102, 240)]
    public void rgba_to_i420_gives_the_bt709_limited_range_values(byte r, byte g, byte b, int y, int u, int v)
    {
        //Arrange
        byte[] i420 = new byte[YuvConversion.I420Size(4, 2)];

        //Act
        YuvConversion.RgbaToI420(SolidRgba(4, 2, r, g, b), 4, 2, VideoColorMatrix.Bt709, VideoColorRange.Limited, i420);

        //Assert
        i420[0].Should().Be((byte)y);
        i420[8].Should().Be((byte)u);
        i420[10].Should().Be((byte)v);
    }

    [Fact]
    public void rgba_to_i420_full_range_white_is_255()
    {
        //Arrange
        byte[] i420 = new byte[YuvConversion.I420Size(2, 2)];

        //Act
        YuvConversion.RgbaToI420(SolidRgba(2, 2, 255, 255, 255), 2, 2, VideoColorMatrix.Bt601, VideoColorRange.Full, i420);

        //Assert
        i420[0].Should().Be((byte)255);
        i420[4].Should().Be((byte)128);
    }

    [Fact]
    public void unspecified_matrix_resolves_by_height()
    {
        //Assert
        YuvConversion.ResolveMatrix(VideoColorMatrix.Unspecified, 480).Should().Be(VideoColorMatrix.Bt601);
        YuvConversion.ResolveMatrix(VideoColorMatrix.Unspecified, 720).Should().Be(VideoColorMatrix.Bt709);
        YuvConversion.ResolveMatrix(VideoColorMatrix.Other, 1080).Should().Be(VideoColorMatrix.Bt709);
        YuvConversion.ResolveMatrix(VideoColorMatrix.Bt2020Constant, 480).Should().Be(VideoColorMatrix.Bt2020NonConstant);
    }

    [Theory]
    [InlineData(VideoColorMatrix.Bt601, VideoColorRange.Limited)]
    [InlineData(VideoColorMatrix.Bt709, VideoColorRange.Limited)]
    [InlineData(VideoColorMatrix.Bt2020NonConstant, VideoColorRange.Full)]
    public void rgba_round_trips_through_i420_within_rounding(VideoColorMatrix matrix, VideoColorRange range)
    {
        //Arrange
        var colours = new[] { (200, 40, 90), (10, 220, 130), (128, 128, 128), (250, 250, 5) };
        foreach ((int r, int g, int b) in colours)
        {
            byte[] i420 = new byte[YuvConversion.I420Size(2, 2)];
            byte[] back = new byte[16];

            //Act
            YuvConversion.RgbaToI420(SolidRgba(2, 2, (byte)r, (byte)g, (byte)b), 2, 2, matrix, range, i420);
            YuvConversion.I420ToRgba(i420, 2, 2, matrix, range, back);

            //Assert
            Math.Abs(back[0] - r).Should().BeLessThanOrEqualTo(3);
            Math.Abs(back[1] - g).Should().BeLessThanOrEqualTo(3);
            Math.Abs(back[2] - b).Should().BeLessThanOrEqualTo(3);
            back[3].Should().Be((byte)255);
        }
    }

    [Fact]
    public void nv12_and_i420_convert_both_ways_exactly_with_odd_sizes()
    {
        //Arrange
        const int width = 5, height = 3;
        int size = YuvConversion.I420Size(width, height);
        byte[] i420 = new byte[size];
        for (int i = 0; i < size; i++) i420[i] = (byte)(i * 7);
        byte[] nv12 = new byte[size];
        byte[] back = new byte[size];

        //Act
        YuvConversion.I420ToNv12(i420, width, height, nv12);
        YuvConversion.Nv12ToI420(VideoPlane.FromArray(nv12[..(width * height)], width, height), VideoPlane.FromArray(nv12[(width * height)..], 6, 2), width, height, back);

        //Assert
        size.Should().Be(15 + 2 * 6);
        nv12[15].Should().Be(i420[15]);
        nv12[16].Should().Be(i420[21]);
        back.Should().Equal(i420);
    }

    [Fact]
    public void pack_i420_removes_row_padding()
    {
        //Arrange
        byte[] y = new byte[2 * 8];
        y[0] = 1; y[1] = 2; y[8] = 3; y[9] = 4;
        byte[] u = { 5 };
        byte[] v = { 6 };
        VideoFrameData frame = VideoFrameData.CreateI420(2, 2, new VideoPlane(y, 8, 2, 2), VideoPlane.FromArray(u, 1, 1), VideoPlane.FromArray(v, 1, 1), TimeSpan.Zero, 0);
        byte[] packed = new byte[6];

        //Act
        YuvConversion.PackI420(frame, packed);

        //Assert
        packed.Should().Equal(new byte[] { 1, 2, 3, 4, 5, 6 });
    }

    [Fact]
    public void scale_keeps_a_flat_picture_flat_and_copies_at_the_same_size()
    {
        //Arrange
        byte[] source = new byte[YuvConversion.I420Size(8, 6)];
        Array.Fill(source, (byte)77);
        byte[] smaller = new byte[YuvConversion.I420Size(4, 2)];
        byte[] same = new byte[source.Length];

        //Act
        YuvConversion.ScaleI420(source, 8, 6, smaller, 4, 2);
        YuvConversion.ScaleI420(source, 8, 6, same, 8, 6);

        //Assert
        smaller.Should().OnlyContain(value => value == 77);
        same.Should().Equal(source);
    }

    [Fact]
    public void scale_interpolates_a_horizontal_ramp()
    {
        //Arrange
        byte[] source = new byte[YuvConversion.I420Size(4, 2)];
        byte[] ramp = { 0, 100, 200, 250 };
        for (int row = 0; row < 2; row++) Array.Copy(ramp, 0, source, row * 4, 4);
        byte[] wide = new byte[YuvConversion.I420Size(8, 2)];

        //Act
        YuvConversion.ScaleI420(source, 4, 2, wide, 8, 2);

        //Assert
        wide[0].Should().Be((byte)0);
        wide[7].Should().Be((byte)250);
        for (int x = 1; x < 8; x++) wide[x].Should().BeGreaterThanOrEqualTo(wide[x - 1]);
    }
}
