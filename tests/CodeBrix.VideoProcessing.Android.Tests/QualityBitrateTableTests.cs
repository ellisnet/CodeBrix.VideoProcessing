using System;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class QualityBitrateTableTests
{
    private static readonly FrameRate Thirty = new FrameRate(30, 1);

    [Fact]
    public void the_table_is_0_1_bit_per_pixel_at_level_23_and_halves_every_6_levels()
    {
        //Arrange
        int[] table = QualityBitrateTable.BitsPerPixelTenThousandths;

        //Assert
        table.Length.Should().Be(52);
        table[23].Should().Be(1000);
        table[17].Should().Be(2000);
        table[29].Should().Be(500);
        table[35].Should().Be(250);
        table[11].Should().Be(4000);
        table[0].Should().Be(14254);
        table[51].Should().Be(39);
        for (int level = 1; level < table.Length; level++) table[level].Should().BeLessThan(table[level - 1]);
    }

    [Theory]
    [InlineData(23, 1920, 1080, 6220)]
    [InlineData(23, 1280, 720, 2764)]
    [InlineData(18, 1920, 1080, 11085)]
    [InlineData(28, 640, 480, 517)]
    [InlineData(23, 320, 240, 230)]
    public void h264_bitrates_are_width_times_height_times_fps_times_bpp(int level, int width, int height, int kilobits)
        => QualityBitrateTable.ForLevel(level, width, height, Thirty, hevc: false).Should().Be(kilobits);

    [Fact]
    public void hevc_gets_65_percent()
        => QualityBitrateTable.ForLevel(23, 1920, 1080, Thirty, hevc: true).Should().Be(6220 * 65 / 100);

    [Fact]
    public void rational_rates_are_exact()
        => QualityBitrateTable.ForLevel(23, 1920, 1080, new FrameRate(30000, 1001), hevc: false).Should().Be(6214);

    [Fact]
    public void results_are_clamped()
    {
        //Assert
        QualityBitrateTable.ForLevel(51, 16, 16, Thirty, hevc: false).Should().Be(QualityBitrateTable.MinimumKilobitsPerSecond);
        QualityBitrateTable.ForLevel(0, 7680, 4320, new FrameRate(120, 1), hevc: false).Should().Be(QualityBitrateTable.MaximumKilobitsPerSecond);
    }

    [Fact]
    public void the_request_modes_map_as_documented()
    {
        //Assert
        QualityBitrateTable.ToKilobitsPerSecond(VideoQuality.TargetBitrate(2500), 1920, 1080, Thirty, false).Should().Be(2500);
        QualityBitrateTable.ToKilobitsPerSecond(VideoQuality.EngineDefault, 1920, 1080, Thirty, false).Should().Be(6220);
        QualityBitrateTable.ToKilobitsPerSecond(VideoQuality.ConstantQuality(29), 1920, 1080, Thirty, false).Should().Be(3110);
    }

    [Fact]
    public void bad_arguments_are_refused()
    {
        //Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => QualityBitrateTable.ForLevel(52, 2, 2, Thirty, false));
        Assert.Throws<ArgumentException>(() => QualityBitrateTable.ForLevel(23, 2, 2, FrameRate.Unspecified, false));
    }
}
