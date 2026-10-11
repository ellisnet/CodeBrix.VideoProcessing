using System;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core.Media;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class KeyframeIntervalTests
{
    [Theory]
    [InlineData(30, 30, 1, 1.0f)]
    [InlineData(15, 30, 1, 0.5f)]
    [InlineData(60, 25, 1, 2.4f)]
    [InlineData(1, 30, 1, 0.033f)]
    [InlineData(30, 30000, 1001, 1.001f)]
    public void frames_become_seconds_rounded_to_the_millisecond(int frames, int numerator, int denominator, float seconds)
        => KeyframeInterval.ToSeconds(frames, new FrameRate(numerator, denominator)).Should().BeApproximately(seconds, 0.0000001f);

    [Fact]
    public void zero_means_the_engine_default()
        => KeyframeInterval.ToSeconds(0, new FrameRate(30, 1)).Should().Be(KeyframeInterval.DefaultSeconds);

    [Fact]
    public void a_tiny_interval_never_rounds_to_zero()
        => KeyframeInterval.ToSeconds(1, new FrameRate(10000, 1)).Should().Be(0.001f);

    [Fact]
    public void negative_frames_are_refused()
        => Assert.Throws<ArgumentOutOfRangeException>(() => KeyframeInterval.ToSeconds(-1, new FrameRate(30, 1)));
}
