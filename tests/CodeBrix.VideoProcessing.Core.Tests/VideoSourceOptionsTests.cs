using System;
using CodeBrix.VideoProcessing.Core.Frames;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class VideoSourceOptionsTests
{
    [Fact]
    public void defaults_are_i420_without_alpha_from_the_start()
    {
        //Arrange
        var options = new VideoSourceOptions();

        //Assert
        options.PixelLayout.Should().Be(VideoPixelLayout.I420);
        options.IncludeAlpha.Should().BeFalse();
        options.StartTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Validate_rejects_a_negative_start()
        => Assert.Throws<ArgumentOutOfRangeException>(new VideoSourceOptions { StartTime = TimeSpan.FromSeconds(-1) }.Validate);

    [Fact]
    public void Validate_rejects_an_unknown_layout()
        => Assert.Throws<ArgumentOutOfRangeException>(new VideoSourceOptions { PixelLayout = (VideoPixelLayout)9 }.Validate);

    [Fact]
    public void default_bit_depth_is_eight()
        => new VideoSourceOptions().BitDepth.Should().Be(8);

    [Theory]
    [InlineData(VideoPixelLayout.I420, 8)]
    [InlineData(VideoPixelLayout.I420, 10)]
    [InlineData(VideoPixelLayout.I420, 12)]
    [InlineData(VideoPixelLayout.I444, 10)]
    [InlineData(VideoPixelLayout.Gray, 12)]
    [InlineData(VideoPixelLayout.Nv12, 10)]
    [InlineData(VideoPixelLayout.Rgba, 8)]
    public void Validate_accepts_supported_layouts_and_depths(VideoPixelLayout layout, int bitDepth)
        => new VideoSourceOptions { PixelLayout = layout, BitDepth = bitDepth }.Validate();

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(16)]
    public void Validate_rejects_unsupported_bit_depths(int bitDepth)
    {
        //Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(new VideoSourceOptions { BitDepth = bitDepth }.Validate);

        //Assert
        exception.ParamName.Should().Be("BitDepth");
    }

    [Fact]
    public void Validate_rejects_deep_rgba()
    {
        //Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(new VideoSourceOptions { PixelLayout = VideoPixelLayout.Rgba, BitDepth = 10 }.Validate);

        //Assert
        exception.ParamName.Should().Be("BitDepth");
    }
}
