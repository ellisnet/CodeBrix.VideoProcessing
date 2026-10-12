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
    public void subtitles_and_chapters_are_empty_by_default_and_never_null()
    {
        //Act
        var unset = new MediaInfo();
        var nulled = new MediaInfo { SubtitleStreams = null, Chapters = null };

        //Assert
        unset.SubtitleStreams.Should().BeEmpty();
        unset.Chapters.Should().BeEmpty();
        unset.HasSubtitles.Should().BeFalse();
        unset.HasChapters.Should().BeFalse();
        nulled.SubtitleStreams.Should().BeEmpty();
        nulled.Chapters.Should().BeEmpty();
    }

    [Fact]
    public void subtitles_and_chapters_are_reported_in_order()
    {
        //Arrange
        var english = new SubtitleStreamInfo { Index = 2, CodecName = "mov_text", Language = "eng", Title = "English" };
        var german = new SubtitleStreamInfo { Index = 3, CodecName = "mov_text", Language = "ger" };
        var opening = new ChapterInfo { Index = 0, Start = TimeSpan.Zero, End = TimeSpan.FromSeconds(1), Title = "Opening" };
        var closing = new ChapterInfo { Index = 1, Start = TimeSpan.FromSeconds(1), End = TimeSpan.FromSeconds(2) };

        //Act
        var info = new MediaInfo { SubtitleStreams = new[] { english, german }, Chapters = new[] { opening, closing } };

        //Assert
        info.HasSubtitles.Should().BeTrue();
        info.HasChapters.Should().BeTrue();
        info.SubtitleStreams.Should().Equal(english, german);
        info.Chapters.Should().Equal(opening, closing);
    }

    [Fact]
    public void subtitle_and_chapter_tags_are_case_insensitive_and_never_null()
    {
        //Act
        var subtitle = new SubtitleStreamInfo { Tags = new Dictionary<string, string> { ["LANGUAGE"] = "eng" } };
        var chapter = new ChapterInfo { Tags = new Dictionary<string, string> { ["Title"] = "One" } };

        //Assert
        subtitle.Tags["language"].Should().Be("eng");
        chapter.Tags["title"].Should().Be("One");
        new SubtitleStreamInfo { Tags = null }.Tags.Should().BeEmpty();
        new ChapterInfo().Tags.Should().BeEmpty();
        new SubtitleStreamInfo().Title.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 1500, 1500)]
    [InlineData(1000, 3000, 2000)]
    [InlineData(3000, 1000, 0)]
    public void a_chapter_duration_is_end_minus_start_and_never_negative(int startMs, int endMs, int expectedMs)
        => new ChapterInfo { Start = TimeSpan.FromMilliseconds(startMs), End = TimeSpan.FromMilliseconds(endMs) }
            .Duration.Should().Be(TimeSpan.FromMilliseconds(expectedMs));

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
