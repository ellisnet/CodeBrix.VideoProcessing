using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class VideoProgressTests
{
    [Fact]
    public void constructor_keeps_the_values()
    {
        //Act
        var progress = new VideoProgress(0.5, 10, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        //Assert
        progress.Fraction.Should().Be(0.5);
        progress.Frames.Should().Be(10);
        progress.Position.Should().Be(TimeSpan.FromSeconds(1));
        progress.Elapsed.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void unknown_fraction_is_null()
        => new VideoProgress(null, 0, TimeSpan.Zero, TimeSpan.Zero).Fraction.Should().BeNull();

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void constructor_rejects_fractions_outside_0_to_1(double fraction)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new VideoProgress(fraction, 0, TimeSpan.Zero, TimeSpan.Zero));

    [Fact]
    public void constructor_rejects_negative_frames()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new VideoProgress(null, -1, TimeSpan.Zero, TimeSpan.Zero));
}
