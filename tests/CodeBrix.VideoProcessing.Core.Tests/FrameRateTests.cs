using System;
using CodeBrix.VideoProcessing.Core.Media;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class FrameRateTests
{
    [Fact]
    public void constructor_reduces_the_fraction()
    {
        //Act
        var rate = new FrameRate(60, 2);

        //Assert
        rate.Numerator.Should().Be(30);
        rate.Denominator.Should().Be(1);
        rate.Should().Be(new FrameRate(30, 1));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-25, 1)]
    public void constructor_rejects_non_positive_values(int numerator, int denominator)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new FrameRate(numerator, denominator));

    [Fact]
    public void default_is_unspecified()
    {
        //Arrange
        FrameRate rate = default;

        //Assert
        rate.IsSpecified.Should().BeFalse();
        rate.Should().Be(FrameRate.Unspecified);
        rate.FramesPerSecond.Should().Be(0);
        rate.ToString().Should().Be("unspecified");
        Assert.Throws<InvalidOperationException>(() => rate.TimestampOf(1));
    }

    [Theory]
    [InlineData("30000/1001", 30000, 1001)]
    [InlineData("25", 25, 1)]
    [InlineData(" 50/2 ", 25, 1)]
    public void Parse_reads_rationals_and_whole_numbers(string text, int numerator, int denominator)
        => FrameRate.Parse(text).Should().Be(new FrameRate(numerator, denominator));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0/0")]
    [InlineData("29.97")]
    [InlineData("1/2/3")]
    [InlineData("-25")]
    public void TryParse_rejects_bad_text(string text)
    {
        //Act
        bool parsed = FrameRate.TryParse(text, out FrameRate rate);

        //Assert
        parsed.Should().BeFalse();
        rate.Should().Be(FrameRate.Unspecified);
    }

    [Fact]
    public void Parse_throws_format_exception()
        => Assert.Throws<FormatException>(() => FrameRate.Parse("fast"));

    [Fact]
    public void ToString_round_trips_through_Parse()
        => FrameRate.Parse(new FrameRate(30000, 1001).ToString()).Should().Be(new FrameRate(30000, 1001));

    [Fact]
    public void TimestampOf_is_exact_for_whole_rates()
    {
        //Arrange
        var rate = new FrameRate(25, 1);

        //Assert
        rate.TimestampOf(0).Should().Be(TimeSpan.Zero);
        rate.TimestampOf(25).Should().Be(TimeSpan.FromSeconds(1));
        rate.FrameDuration.Should().Be(TimeSpan.FromMilliseconds(40));
    }

    [Theory]
    [InlineData(30000, 1001)]
    [InlineData(24000, 1001)]
    [InlineData(25, 1)]
    [InlineData(60, 1)]
    [InlineData(3, 1)]
    public void FrameIndexAt_inverts_TimestampOf(int numerator, int denominator)
    {
        //Arrange
        var rate = new FrameRate(numerator, denominator);

        //Act + Assert
        for (long index = 0; index < 100_000; index += 7)
        {
            rate.FrameIndexAt(rate.TimestampOf(index)).Should().Be(index);
        }
    }

    [Fact]
    public void FrameIndexAt_returns_the_frame_on_screen()
    {
        //Arrange
        var rate = new FrameRate(25, 1);

        //Assert
        rate.FrameIndexAt(TimeSpan.FromMilliseconds(39)).Should().Be(0);
        rate.FrameIndexAt(TimeSpan.FromMilliseconds(40)).Should().Be(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => rate.FrameIndexAt(TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void FrameCountFor_counts_partial_frames()
    {
        //Arrange
        var rate = new FrameRate(25, 1);

        //Assert
        rate.FrameCountFor(TimeSpan.FromSeconds(3)).Should().Be(75);
        rate.FrameCountFor(TimeSpan.FromMilliseconds(3001)).Should().Be(76);
        rate.FrameCountFor(TimeSpan.Zero).Should().Be(0);
    }

    [Fact]
    public void TimestampOf_handles_very_long_streams_without_overflow()
        => new FrameRate(30000, 1001).TimestampOf(1_000_000_000L).Should().BeGreaterThan(TimeSpan.FromDays(300));

    [Fact]
    public void equality_operators_work()
    {
        //Assert
        (new FrameRate(30, 1) == new FrameRate(60, 2)).Should().BeTrue();
        (new FrameRate(30, 1) != new FrameRate(30000, 1001)).Should().BeTrue();
    }
}
