using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class VideoProcessorCapabilitiesTests
{
    [Fact]
    public void chapters_is_bit_twelve()
        => ((int)VideoProcessorCapabilities.Chapters).Should().Be(1 << 12);

    [Fact]
    public void chapters_shares_no_bit_with_any_other_flag()
    {
        //Arrange
        VideoProcessorCapabilities others = VideoProcessorCapabilities.None;
        foreach (VideoProcessorCapabilities flag in Enum.GetValues<VideoProcessorCapabilities>())
        {
            if (flag != VideoProcessorCapabilities.Chapters) others |= flag;
        }

        //Assert
        (others & VideoProcessorCapabilities.Chapters).Should().Be(VideoProcessorCapabilities.None);
    }

    [Fact]
    public void an_engine_reports_chapters_only_when_it_declares_the_flag()
    {
        //Arrange
        var reader = new FakeVideoProcessor(capabilities: VideoProcessorCapabilities.Chapters | VideoProcessorCapabilities.Snapshot);
        var other = new FakeVideoProcessor(capabilities: VideoProcessorCapabilities.Snapshot);

        //Assert
        reader.Supports(VideoProcessorCapabilities.Chapters).Should().BeTrue();
        other.Supports(VideoProcessorCapabilities.Chapters).Should().BeFalse();
    }
}
