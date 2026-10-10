using System;
using System.Collections.Generic;
using CodeBrix.VideoProcessing.Core.Media;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class MediaInfoTests
{
    [Fact]
    public void collections_are_never_null()
    {
        //Act
        var info = new MediaInfo { VideoStreams = null, AudioStreams = null, Tags = null };

        //Assert
        info.VideoStreams.Should().BeEmpty();
        info.AudioStreams.Should().BeEmpty();
        info.Tags.Should().BeEmpty();
        info.PrimaryVideo.Should().BeNull();
        info.PrimaryAudio.Should().BeNull();
        info.HasVideo.Should().BeFalse();
        info.HasAudio.Should().BeFalse();
    }

    [Fact]
    public void Tags_are_case_insensitive_copies()
    {
        //Arrange
        var source = new Dictionary<string, string> { ["Title"] = "Holiday" };

        //Act
        var info = new MediaInfo { Tags = source };
        source["Title"] = "changed";

        //Assert
        info.Tags["title"].Should().Be("Holiday");
    }

    [Fact]
    public void PrimaryVideo_and_PrimaryAudio_are_the_first_streams()
    {
        //Arrange
        var first = new VideoStreamInfo { Index = 0 };
        var audio = new AudioStreamInfo { Index = 2, SampleRate = 48000 };

        //Act
        var info = new MediaInfo { VideoStreams = new[] { first, new VideoStreamInfo { Index = 1 } }, AudioStreams = new[] { audio } };

        //Assert
        info.PrimaryVideo.Should().BeSameAs(first);
        info.PrimaryAudio.Should().BeSameAs(audio);
        info.HasVideo.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 1280, 720)]
    [InlineData(90, 720, 1280)]
    [InlineData(180, 1280, 720)]
    [InlineData(270, 720, 1280)]
    public void VideoStreamInfo_display_size_follows_rotation(int rotation, int displayWidth, int displayHeight)
    {
        //Act
        var stream = new VideoStreamInfo { Width = 1280, Height = 720, RotationDegrees = rotation };

        //Assert
        stream.DisplayWidth.Should().Be(displayWidth);
        stream.DisplayHeight.Should().Be(displayHeight);
    }

    [Fact]
    public void stream_tags_are_never_null()
    {
        //Assert
        new VideoStreamInfo { Tags = null }.Tags.Should().BeEmpty();
        new AudioStreamInfo().Tags.Should().BeEmpty();
    }
}
