using System;
using System.Collections.Generic;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core.Media;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class MediaFormatMappingTests
{
    [Theory]
    [InlineData("video/avc", "h264")]
    [InlineData("video/hevc", "hevc")]
    [InlineData("video/av01", "av1")]
    [InlineData("video/x-vnd.on2.vp9", "vp9")]
    [InlineData("audio/mp4a-latm", "aac")]
    [InlineData("audio/opus", "opus")]
    [InlineData("AUDIO/FLAC", "flac")]
    [InlineData("video/x-unknown", "video/x-unknown")]
    [InlineData(null, null)]
    public void mime_types_map_to_ffmpeg_codec_names(string mime, string name)
        => MediaFormatMapping.CodecNameFromMime(mime).Should().Be(name);

    [Theory]
    [InlineData(30.0, 30, 1)]
    [InlineData(29.97, 30000, 1001)]
    [InlineData(29.970029, 30000, 1001)]
    [InlineData(23.976, 24000, 1001)]
    [InlineData(59.94, 60000, 1001)]
    [InlineData(25.0001, 25, 1)]
    [InlineData(12.5, 25, 2)]
    public void float_frame_rates_snap_to_the_usual_rationals(double value, int numerator, int denominator)
        => MediaFormatMapping.FrameRateFromValue(value).Should().Be(new FrameRate(numerator, denominator));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void no_frame_rate_value_is_unspecified(double value)
        => MediaFormatMapping.FrameRateFromValue(value).IsSpecified.Should().BeFalse();

    [Fact]
    public void the_container_rate_wins_over_the_format_value()
    {
        //Arrange
        var track = new TrackDescription { Mime = "video/avc", FrameRateValue = 30, ContainerFrameRate = new FrameRate(30000, 1001) };

        //Assert
        MediaFormatMapping.ChooseFrameRate(track).Should().Be(new FrameRate(30000, 1001));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(90, 90)]
    [InlineData(-90, 270)]
    [InlineData(450, 90)]
    [InlineData(271, 270)]
    public void rotations_normalise_to_quarter_turns(int degrees, int expected)
        => MediaFormatMapping.NormaliseRotation(degrees).Should().Be(expected);

    [Fact]
    public void colour_keys_map_both_ways()
    {
        //Assert
        MediaFormatMapping.ToColorRange(1).Should().Be(VideoColorRange.Full);
        MediaFormatMapping.ToColorRange(2).Should().Be(VideoColorRange.Limited);
        MediaFormatMapping.ToColorRange(-1).Should().Be(VideoColorRange.Unspecified);
        MediaFormatMapping.ToColorMatrix(1).Should().Be(VideoColorMatrix.Bt709);
        MediaFormatMapping.ToColorMatrix(2).Should().Be(VideoColorMatrix.Bt601);
        MediaFormatMapping.ToColorMatrix(4).Should().Be(VideoColorMatrix.Bt601);
        MediaFormatMapping.ToColorMatrix(6).Should().Be(VideoColorMatrix.Bt2020NonConstant);
        MediaFormatMapping.ToColorMatrix(9).Should().Be(VideoColorMatrix.Other);
        MediaFormatMapping.ToColorPrimaries(2).Should().Be(VideoColorPrimaries.Bt601Pal);
        MediaFormatMapping.ToColorPrimaries(4).Should().Be(VideoColorPrimaries.Bt601Ntsc);
        MediaFormatMapping.ToColorTransfer(3).Should().Be(VideoColorTransfer.Bt709);
        MediaFormatMapping.ToColorTransfer(6).Should().Be(VideoColorTransfer.Pq);
        MediaFormatMapping.ToColorTransfer(7).Should().Be(VideoColorTransfer.Hlg);
        MediaFormatMapping.ToMediaFormatColorRange(VideoColorRange.Full).Should().Be(1);
        MediaFormatMapping.ToMediaFormatColorRange(VideoColorRange.Unspecified).Should().Be(2);
        MediaFormatMapping.ToMediaFormatColorStandard(VideoColorMatrix.Bt709).Should().Be(1);
        MediaFormatMapping.ToMediaFormatColorStandard(VideoColorMatrix.Bt601).Should().Be(4);
        MediaFormatMapping.ToMediaFormatColorStandard(VideoColorMatrix.Unspecified).Should().Be(-1);
        MediaFormatMapping.ToMediaFormatColorTransfer(VideoColorMatrix.Bt709).Should().Be(3);
        MediaFormatMapping.ToMediaFormatColorTransfer(VideoColorMatrix.Rgb).Should().Be(-1);
    }

    [Theory]
    [InlineData("video/avc", 0x8, 8, VideoChromaSubsampling.Yuv420)]
    [InlineData("video/avc", 0x10, 10, VideoChromaSubsampling.Yuv420)]
    [InlineData("video/avc", 0x40, 0, VideoChromaSubsampling.Yuv444)]
    [InlineData("video/hevc", 0x2, 10, VideoChromaSubsampling.Yuv420)]
    [InlineData("video/hevc", 0x1000, 10, VideoChromaSubsampling.Yuv420)]
    [InlineData("video/x-vnd.on2.vp9", 0x1, 8, VideoChromaSubsampling.Yuv420)]
    [InlineData("video/x-vnd.on2.vp9", 0x8, 10, VideoChromaSubsampling.Unknown)]
    [InlineData("video/av01", 0x2, 10, VideoChromaSubsampling.Yuv420)]
    [InlineData("video/avc", -1, 0, VideoChromaSubsampling.Unknown)]
    public void profiles_give_depth_and_chroma_when_they_fix_them(string mime, int profile, int depth, VideoChromaSubsampling chroma)
    {
        //Assert
        MediaFormatMapping.BitDepthFromProfile(mime, profile).Should().Be(depth);
        MediaFormatMapping.ChromaFromProfile(mime, profile).Should().Be(chroma);
    }

    [Fact]
    public void build_media_info_maps_every_track()
    {
        //Arrange
        var tracks = new List<TrackDescription>
        {
            new TrackDescription
            {
                Index = 0, Mime = "video/hevc", Width = 1920, Height = 1080, DurationMicroseconds = 2_002_000, FrameRateValue = 29.97,
                Rotation = 90, ColorRange = 2, ColorStandard = 1, ColorTransfer = 3, Profile = 0x2, Language = "eng", ContainerSampleCount = 60
            },
            new TrackDescription { Index = 1, Mime = "audio/mp4a-latm", SampleRate = 44100, ChannelCount = 2, DurationMicroseconds = 2_020_000, Language = "und" },
            new TrackDescription { Index = 2, Mime = "application/x-subrip" }
        };
        var tags = MediaFormatMapping.BuildTags(new[]
        {
            new KeyValuePair<string, string>("title", " Holiday "),
            new KeyValuePair<string, string>("date", "20261011T120304.000Z"),
            new KeyValuePair<string, string>("artist", null)
        });

        //Act
        MediaInfo info = MediaFormatMapping.BuildMediaInfo("video/mp4", 2021, tracks, tags);

        //Assert
        info.ContainerFormat.Should().Be("video/mp4");
        info.Duration.Should().Be(TimeSpan.FromMilliseconds(2020));
        info.VideoStreams.Count.Should().Be(1);
        info.AudioStreams.Count.Should().Be(1);
        VideoStreamInfo video = info.PrimaryVideo;
        video.CodecName.Should().Be("hevc");
        video.FrameRate.Should().Be(new FrameRate(30000, 1001));
        video.FrameCount.Should().Be(60);
        video.Duration.Should().Be(TimeSpan.FromMilliseconds(2002));
        video.BitDepth.Should().Be(10);
        video.PixelFormatName.Should().Be("yuv420p10le");
        video.RotationDegrees.Should().Be(90);
        video.DisplayWidth.Should().Be(1080);
        video.ColorRange.Should().Be(VideoColorRange.Limited);
        video.ColorMatrix.Should().Be(VideoColorMatrix.Bt709);
        video.ColorTransfer.Should().Be(VideoColorTransfer.Bt709);
        video.HasAlpha.Should().BeFalse();
        video.Tags["language"].Should().Be("eng");
        AudioStreamInfo audio = info.PrimaryAudio;
        audio.CodecName.Should().Be("aac");
        audio.SampleRate.Should().Be(44100);
        audio.BitDepth.Should().Be(0);
        audio.Tags.ContainsKey("language").Should().BeFalse();
        info.Tags["title"].Should().Be("Holiday");
        info.Tags["creation_time"].Should().Be("2026-10-11T12:03:04.000000Z");
        info.Tags.ContainsKey("artist").Should().BeFalse();
    }

    [Fact]
    public void an_unset_mp4_date_is_dropped()
    {
        //Act
        Dictionary<string, string> tags = MediaFormatMapping.BuildTags(new[] { new KeyValuePair<string, string>("date", "19040101T000000.000Z") });

        //Assert
        tags.Should().BeEmpty();
    }

    [Fact]
    public void unknown_values_stay_unknown()
    {
        //Act
        VideoStreamInfo video = MediaFormatMapping.MapVideo(new TrackDescription { Mime = "video/x-vnd.on2.vp8" }, TimeSpan.FromSeconds(3));

        //Assert
        video.Width.Should().Be(0);
        video.FrameRate.IsSpecified.Should().BeFalse();
        video.Duration.Should().Be(TimeSpan.FromSeconds(3));
        video.ColorRange.Should().Be(VideoColorRange.Unspecified);
        video.ColorMatrix.Should().Be(VideoColorMatrix.Unspecified);
        video.BitDepth.Should().Be(8);
    }

    [Fact]
    public void raw_audio_reports_its_pcm_depth()
    {
        //Assert
        MediaFormatMapping.AudioBitDepth(new TrackDescription { Mime = "audio/raw" }).Should().Be(16);
        MediaFormatMapping.AudioBitDepth(new TrackDescription { Mime = "audio/raw", PcmEncoding = MediaFormatKeys.PcmEncoding24BitPacked }).Should().Be(24);
        MediaFormatMapping.AudioBitDepth(new TrackDescription { Mime = "audio/flac" }).Should().Be(0);
    }

    [Fact]
    public void microseconds_convert_exactly()
    {
        //Assert
        MediaFormatMapping.FromMicroseconds(1_500_001).Should().Be(TimeSpan.FromTicks(15_000_010));
        MediaFormatMapping.ToMicroseconds(TimeSpan.FromTicks(15_000_019)).Should().Be(1_500_001);
    }
}
