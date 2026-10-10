using System;
using CodeBrix.VideoProcessing.Core.Frames;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class AudioSourceOptionsTests
{
    [Fact]
    public void defaults_keep_the_source_format()
    {
        //Arrange
        var options = new AudioSourceOptions();

        //Act
        Action act = options.Validate;

        //Assert
        options.SampleRate.Should().Be(0);
        options.Channels.Should().Be(0);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(500, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 33)]
    public void Validate_rejects_out_of_range_values(int sampleRate, int channels)
        => Assert.Throws<ArgumentOutOfRangeException>(new AudioSourceOptions { SampleRate = sampleRate, Channels = channels }.Validate);
}
