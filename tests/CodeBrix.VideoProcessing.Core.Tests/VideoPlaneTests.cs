using System;
using CodeBrix.VideoProcessing.Core.Frames;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class VideoPlaneTests
{
    [Fact]
    public void default_is_empty()
    {
        //Arrange
        VideoPlane plane = default;

        //Assert
        plane.IsEmpty.Should().BeTrue();
        plane.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void constructor_accepts_an_unpadded_last_row()
    {
        //Act
        var plane = new VideoPlane(new byte[8 * 2 + 5], 8, 5, 3);

        //Assert
        plane.Rows.Should().Be(3);
        plane.Stride.Should().Be(8);
    }

    [Fact]
    public void constructor_rejects_short_data()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new VideoPlane(new byte[8 * 2 + 4], 8, 5, 3));

    [Fact]
    public void constructor_rejects_stride_below_row_length()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new VideoPlane(new byte[100], 4, 5, 3));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void constructor_rejects_empty_sizes(int rowLength, int rows)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new VideoPlane(new byte[100], 10, rowLength, rows));

    [Fact]
    public void GetRow_skips_padding()
    {
        //Arrange
        byte[] data = { 1, 2, 9, 9, 3, 4, 9, 9 };
        var plane = new VideoPlane(data, 4, 2, 2);

        //Act
        byte[] row = plane.GetRow(1).ToArray();

        //Assert
        row.Should().Equal(3, 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => plane.GetRow(2));
    }

    [Fact]
    public void ToArray_packs_rows_tightly()
    {
        //Arrange
        byte[] data = { 1, 2, 9, 9, 3, 4 };
        var plane = new VideoPlane(data, 4, 2, 2);

        //Act
        byte[] packed = plane.ToArray();

        //Assert
        packed.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void CopyTo_rejects_a_short_destination()
        => Assert.Throws<ArgumentException>(() => VideoPlane.FromArray(new byte[4], 2, 2).CopyTo(new byte[3]));

    [Fact]
    public void a_sixteen_bit_plane_counts_row_length_in_bytes()
    {
        //Arrange
        byte[] data = { 0xFF, 0x03, 0x01, 0x00, 7, 7, 0x00, 0x02, 0x10, 0x00 };
        var plane = new VideoPlane(data, 6, 4, 2);

        //Act
        ReadOnlySpan<byte> row = plane.GetRow(1);
        int first = row[0] | (row[1] << 8);

        //Assert
        plane.RowLength.Should().Be(4);
        row.Length.Should().Be(4);
        first.Should().Be(512);
        plane.ToArray().Should().Equal(0xFF, 0x03, 0x01, 0x00, 0x00, 0x02, 0x10, 0x00);
    }
}
