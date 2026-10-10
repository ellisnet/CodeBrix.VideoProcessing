using System;
using CodeBrix.VideoProcessing.Core.Output;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class VideoQualityTests
{
    [Fact]
    public void default_is_engine_default()
    {
        //Arrange
        VideoQuality quality = default;

        //Assert
        quality.Mode.Should().Be(VideoQualityMode.EngineDefault);
        quality.Should().Be(VideoQuality.EngineDefault);
        quality.ToString().Should().Be("engine default");
    }

    [Fact]
    public void ConstantQuality_holds_the_level()
    {
        //Act
        var quality = VideoQuality.ConstantQuality(23);

        //Assert
        quality.Mode.Should().Be(VideoQualityMode.ConstantQuality);
        quality.QualityLevel.Should().Be(23);
        quality.BitrateKilobitsPerSecond.Should().Be(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(52)]
    public void ConstantQuality_rejects_levels_outside_0_to_51(int level)
        => Assert.Throws<ArgumentOutOfRangeException>(() => VideoQuality.ConstantQuality(level));

    [Fact]
    public void TargetBitrate_holds_the_bitrate()
    {
        //Act
        var quality = VideoQuality.TargetBitrate(2500);

        //Assert
        quality.Mode.Should().Be(VideoQualityMode.TargetBitrate);
        quality.BitrateKilobitsPerSecond.Should().Be(2500);
        quality.ToString().Should().Be("2500 kbit/s");
    }

    [Fact]
    public void TargetBitrate_rejects_zero()
        => Assert.Throws<ArgumentOutOfRangeException>(() => VideoQuality.TargetBitrate(0));

    [Fact]
    public void equality_compares_mode_and_value()
    {
        //Assert
        (VideoQuality.ConstantQuality(20) == VideoQuality.ConstantQuality(20)).Should().BeTrue();
        (VideoQuality.ConstantQuality(20) != VideoQuality.TargetBitrate(20)).Should().BeTrue();
    }
}
