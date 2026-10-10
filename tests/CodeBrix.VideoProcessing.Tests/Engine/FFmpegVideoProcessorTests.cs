using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;
using CodeBrix.VideoProcessing.Engine;
using CodeBrix.VideoProcessing.Tests.Resources;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests.Engine;

public class FFmpegVideoProcessorTests
{
    private readonly FFmpegVideoProcessor engine = new FFmpegVideoProcessor();

    [Fact]
    public void Capabilities_are_everything_except_nv12()
    {
        //Assert
        engine.Name.Should().Be("ffmpeg");
        engine.Supports(VideoProcessorCapabilities.Nv12Input).Should().BeFalse();
        engine.Supports(
            VideoProcessorCapabilities.AlphaFrameSource | VideoProcessorCapabilities.RgbaInput | VideoProcessorCapabilities.HevcOutput |
            VideoProcessorCapabilities.DirectTranscode | VideoProcessorCapabilities.DirectTrim |
            VideoProcessorCapabilities.DirectConcatenate | VideoProcessorCapabilities.Snapshot).Should().BeTrue();
    }

    [Fact]
    public void Analyse_maps_an_mp4_with_video_and_audio()
    {
        //Act
        MediaInfo info = engine.Analyse(TestResources.Mp4Video, TestContext.Current.CancellationToken);

        //Assert
        info.ContainerFormat.Should().Contain("mp4");
        info.Duration.TotalSeconds.Should().BeApproximately(3.008, 0.01);
        info.VideoStreams.Should().HaveCount(1);
        VideoStreamInfo video = info.PrimaryVideo;
        video.CodecName.Should().Be("h264");
        video.Width.Should().Be(1280);
        video.Height.Should().Be(720);
        video.FrameRate.Should().Be(new FrameRate(25, 1));
        video.FrameCount.Should().Be(75);
        video.BitDepth.Should().Be(8);
        video.PixelFormatName.Should().Be("yuv420p");
        video.ChromaSubsampling.Should().Be(VideoChromaSubsampling.Yuv420);
        video.HasAlpha.Should().BeFalse();
        video.RotationDegrees.Should().Be(0);
        AudioStreamInfo audio = info.PrimaryAudio;
        audio.CodecName.Should().Be("aac");
        audio.SampleRate.Should().Be(48000);
        audio.Channels.Should().Be(6);
    }

    [Fact]
    public async Task AnalyseAsync_matches_Analyse()
    {
        //Act
        MediaInfo info = await engine.AnalyseAsync(TestResources.WebmVideo, TestContext.Current.CancellationToken);

        //Assert
        info.PrimaryVideo.CodecName.Should().Be("vp8");
        info.PrimaryVideo.FrameRate.Should().Be(new FrameRate(30, 1));
        info.PrimaryVideo.Duration.Should().Be(TimeSpan.FromSeconds(3));
        info.HasAudio.Should().BeFalse();
    }

    [Theory]
    [InlineData("./Resources/input_3sec_rotation_90deg.mp4", 270)]
    [InlineData("./Resources/input_3sec_rotation_negative_90deg.mp4", 90)]
    public void Analyse_reports_rotation_clockwise(string path, int clockwise)
    {
        //Act
        VideoStreamInfo video = engine.Analyse(path, TestContext.Current.CancellationToken).PrimaryVideo;

        //Assert
        video.RotationDegrees.Should().Be(clockwise);
        video.DisplayWidth.Should().Be(720);
        video.DisplayHeight.Should().Be(1280);
    }

    [Fact]
    public void Analyse_reports_hdr_colour_and_depth()
    {
        //Act
        VideoStreamInfo video = engine.Analyse(TestResources.HdrVideo, TestContext.Current.CancellationToken).PrimaryVideo;

        //Assert
        video.BitDepth.Should().Be(10);
        video.ColorRange.Should().Be(VideoColorRange.Limited);
        video.ColorMatrix.Should().Be(VideoColorMatrix.Bt2020NonConstant);
        video.ColorPrimaries.Should().Be(VideoColorPrimaries.Bt2020);
        video.ColorTransfer.Should().Be(VideoColorTransfer.Hlg);
    }

    [Fact]
    public void Analyse_reports_alpha_from_the_pixel_format()
    {
        //Arrange
        using var clip = new TemporaryFile("alpha.mkv");
        EngineTestMedia.CreateAlphaClip(clip);

        //Act
        VideoStreamInfo video = engine.Analyse(clip, TestContext.Current.CancellationToken).PrimaryVideo;

        //Assert
        video.HasAlpha.Should().BeTrue();
        video.PixelFormatName.Should().Be("yuva420p");
    }

    [Fact]
    public void Analyse_throws_file_not_found()
        => Assert.Throws<FileNotFoundException>(() => engine.Analyse("./Resources/does-not-exist.mp4", TestContext.Current.CancellationToken));

    [Fact]
    public void Analyse_throws_media_format_exception_for_garbage()
    {
        //Arrange
        using var garbage = new TemporaryFile("garbage.mp4");
        File.WriteAllBytes(garbage, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });

        //Act + Assert
        Assert.Throws<MediaFormatException>(() => engine.Analyse(garbage, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Snapshot_returns_an_owned_i420_frame_at_the_position()
    {
        //Act
        VideoFrameData frame = engine.Snapshot(TestResources.Mp4Video, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        //Assert
        frame.Layout.Should().Be(VideoPixelLayout.I420);
        frame.Width.Should().Be(1280);
        frame.FrameIndex.Should().Be(25);
        frame.Timestamp.Should().Be(TimeSpan.FromSeconds(1));
        frame.Y.Stride.Should().Be(1280);
        frame.HasAlpha.Should().BeFalse();
    }

    [Fact]
    public void Snapshot_past_the_end_throws_media_format_exception()
        => Assert.Throws<MediaFormatException>(() => engine.Snapshot(TestResources.Mp4WithoutAudio, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

    [Fact]
    public void OpenVideo_throws_for_a_file_without_video()
        => Assert.Throws<MediaFormatException>(() => engine.OpenVideo(TestResources.Mp4WithoutVideo));

    [Fact]
    public void Capabilities_include_i444_gray_and_high_bit_depth_frame_sources()
        => engine.Supports(
            VideoProcessorCapabilities.I444FrameSource | VideoProcessorCapabilities.GrayFrameSource |
            VideoProcessorCapabilities.HighBitDepthFrameSource).Should().BeTrue();

    [Fact]
    public void OpenVideo_refuses_invalid_bit_depths()
    {
        //Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.OpenVideo(TestResources.Mp4Video, new VideoSourceOptions { BitDepth = 9 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            engine.OpenVideo(TestResources.Mp4Video, new VideoSourceOptions { PixelLayout = VideoPixelLayout.Rgba, BitDepth = 10 }));
    }

    [Fact]
    public void OpenVideo_refuses_nv12()
        => Assert.Throws<NotSupportedException>(() => engine.OpenVideo(TestResources.Mp4Video, new VideoSourceOptions { PixelLayout = VideoPixelLayout.Nv12 }));

    [Fact]
    public void OpenAudio_throws_for_a_file_without_audio()
        => Assert.Throws<MediaFormatException>(() => engine.OpenAudio(TestResources.Mp4WithoutAudio));

    [Fact]
    public void TrimFile_accurate_cut_has_the_requested_length()
    {
        //Arrange
        using var output = new TemporaryFile("trim.mp4");
        var request = new TrimRequest
        {
            InputPath = TestResources.Mp4Video,
            OutputPath = output,
            From = TimeSpan.FromSeconds(1),
            To = TimeSpan.FromSeconds(2)
        };
        var reports = new List<VideoProgress>();

        //Act
        engine.TrimFile(request, new SynchronousProgress(reports), TestContext.Current.CancellationToken);

        //Assert
        EngineTestMedia.CountFrames(output).Should().Be(25);
        EngineTestMedia.ProbeStream(output, "v:0", "codec_name").Should().Be("h264");
        EngineTestMedia.ProbeStream(output, "a:0", "codec_name").Should().Be("aac");
        reports[reports.Count - 1].Fraction.Should().Be(1);
    }

    [Fact]
    public void TrimFile_snapped_cut_copies_the_streams()
    {
        //Arrange
        using var output = new TemporaryFile("trim-copy.mp4");
        var request = new TrimRequest
        {
            InputPath = TestResources.Mp4Video,
            OutputPath = output,
            From = TimeSpan.FromSeconds(1),
            To = TimeSpan.FromSeconds(2),
            SnapToKeyframes = true
        };

        //Act
        engine.TrimFile(request, null, TestContext.Current.CancellationToken);

        //Assert
        File.Exists(output).Should().BeTrue();
        EngineTestMedia.ProbeStream(output, "v:0", "codec_name").Should().Be("h264");
        EngineTestMedia.Duration(output).Should().BeGreaterThan(0.9);
    }

    [Fact]
    public void ConcatenateFiles_joins_clips_end_to_end()
    {
        //Arrange
        using var first = new TemporaryFile("part1.mp4");
        using var second = new TemporaryFile("part2.mp4");
        using var output = new TemporaryFile("joined.mp4");
        EngineTestMedia.CreateTestClip(first, 1, 440);
        EngineTestMedia.CreateTestClip(second, 1, 660);
        var request = new ConcatenateRequest { InputPaths = { first, second }, OutputPath = output };

        //Act
        engine.ConcatenateFiles(request, null, TestContext.Current.CancellationToken);

        //Assert
        EngineTestMedia.CountFrames(output).Should().Be(50);
        EngineTestMedia.Duration(output).Should().BeApproximately(2.0, 0.1);
    }

    [Fact]
    public void TranscodeFile_resizes_and_keeps_audio()
    {
        //Arrange
        using var output = new TemporaryFile("transcode.mp4");
        var request = new TranscodeRequest { InputPath = TestResources.Mp4Video };
        request.Output.OutputPath = output;
        request.Output.Width = 640;
        request.Output.Height = 360;
        request.Output.Quality = VideoQuality.ConstantQuality(28);
        var reports = new List<VideoProgress>();

        //Act
        engine.TranscodeFile(request, new SynchronousProgress(reports), TestContext.Current.CancellationToken);

        //Assert
        MediaInfo info = engine.Analyse(output, TestContext.Current.CancellationToken);
        info.PrimaryVideo.Width.Should().Be(640);
        info.PrimaryVideo.Height.Should().Be(360);
        info.PrimaryVideo.FrameRate.Should().Be(new FrameRate(25, 1));
        info.PrimaryVideo.FrameCount.Should().Be(75);
        info.PrimaryAudio.CodecName.Should().Be("aac");
        info.PrimaryAudio.Channels.Should().Be(6);
        reports.Should().NotBeEmpty();
        reports[reports.Count - 1].Fraction.Should().Be(1);
    }

    [Fact]
    public void TranscodeFile_keeps_stored_orientation_and_rotation_metadata()
    {
        //Arrange
        using var output = new TemporaryFile("transcode-rotated.mp4");
        var request = new TranscodeRequest { InputPath = "./Resources/input_3sec_rotation_90deg.mp4" };
        request.Output.OutputPath = output;
        request.Output.HasAudio = false;

        //Act
        engine.TranscodeFile(request, null, TestContext.Current.CancellationToken);

        //Assert
        MediaInfo info = engine.Analyse(output, TestContext.Current.CancellationToken);
        info.PrimaryVideo.Width.Should().Be(1280);
        info.PrimaryVideo.RotationDegrees.Should().Be(270);
        info.HasAudio.Should().BeFalse();
    }

    [Fact]
    public void TranscodeFile_honours_cancellation_and_leaves_no_output()
    {
        //Arrange
        using var output = new TemporaryFile("cancelled.mp4");
        var request = new TranscodeRequest { InputPath = TestResources.Mp4Video };
        request.Output.OutputPath = output;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        //Act + Assert
        Assert.ThrowsAny<OperationCanceledException>(() => engine.TranscodeFile(request, null, cancellation.Token));
        File.Exists(output).Should().BeFalse();
    }

    [Fact]
    public void TranscodeFile_refuses_an_invalid_request()
        => Assert.ThrowsAny<ArgumentException>(() => engine.TranscodeFile(new TranscodeRequest(), null, TestContext.Current.CancellationToken));

    private sealed class SynchronousProgress : IProgress<VideoProgress>
    {
        private readonly List<VideoProgress> reports;

        public SynchronousProgress(List<VideoProgress> reports) => this.reports = reports;

        public void Report(VideoProgress value)
        {
            lock (reports) reports.Add(value);
        }
    }
}
