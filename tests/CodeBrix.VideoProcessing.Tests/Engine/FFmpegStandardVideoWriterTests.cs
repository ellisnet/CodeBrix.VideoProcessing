using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;
using CodeBrix.VideoProcessing.Engine;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests.Engine;

public class FFmpegStandardVideoWriterTests
{
    private const int Width = 320;
    private const int Height = 240;
    private const int SampleRate = 48000;

    private readonly FFmpegVideoProcessor engine = new FFmpegVideoProcessor();

    private static StandardVideoRequest Request(string path, bool audio) => new StandardVideoRequest
    {
        OutputPath = path,
        Width = Width,
        Height = Height,
        FrameRate = new FrameRate(25, 1),
        Quality = VideoQuality.ConstantQuality(30),
        HasAudio = audio,
        AudioSampleRate = SampleRate,
        AudioChannels = 2,
        Title = "Writer test",
        CreationTime = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero)
    };

    // 50 frames (2 s) of picture with one frame's worth of tone written after each frame - "in step".
    private static void WriteTwoSeconds(IStandardVideoWriter writer, bool audio)
    {
        const int samplesPerFrame = SampleRate / 25;
        for (int i = 0; i < 50; i++)
        {
            writer.WriteFrame(EngineTestMedia.I420Frame(Width, Height, i));
            if (audio) writer.WriteAudio(EngineTestMedia.Tone(SampleRate, 2, samplesPerFrame, (long)i * samplesPerFrame, 440));
        }
    }

    [Fact]
    public void writer_round_trip_produces_h264_and_aac_with_moov_first()
    {
        //Arrange
        using var output = new TemporaryFile("written.mp4");
        var reports = new List<VideoProgress>();

        //Act
        using (IStandardVideoWriter writer = engine.CreateWriter(Request(output, audio: true), new ListProgress(reports)))
        {
            WriteTwoSeconds(writer, audio: true);
            writer.Finish();
            writer.FramesWritten.Should().Be(50);
            writer.AudioFramesWritten.Should().Be(2 * SampleRate);
        }

        //Assert
        EngineTestMedia.ProbeStream(output, "v:0", "codec_name").Should().Be("h264");
        EngineTestMedia.ProbeStream(output, "v:0", "pix_fmt").Should().Be("yuv420p");
        EngineTestMedia.ProbeStream(output, "v:0", "r_frame_rate").Should().Be("25/1");
        EngineTestMedia.ProbeStream(output, "a:0", "codec_name").Should().Be("aac");
        EngineTestMedia.ProbeStream(output, "a:0", "sample_rate").Should().Be("48000");
        EngineTestMedia.CountFrames(output).Should().Be(50);
        EngineTestMedia.Duration(output).Should().BeApproximately(2.0, 0.1);
        List<string> boxes = EngineTestMedia.TopLevelBoxes(output);
        boxes.IndexOf("moov").Should().BeLessThan(boxes.IndexOf("mdat"));
        MediaInfo info = engine.Analyse(output, TestContext.Current.CancellationToken);
        info.Tags["title"].Should().Be("Writer test");
        info.Tags["creation_time"].Should().StartWith("2026-10-10T12:00:00");
        reports.Should().HaveCount(50);
        reports[49].Frames.Should().Be(50);
        reports[49].Fraction.Should().BeNull();
    }

    [Fact]
    public void FastStart_off_leaves_moov_at_the_end()
    {
        //Arrange
        using var output = new TemporaryFile("not-fast.mp4");
        StandardVideoRequest request = Request(output, audio: false);
        request.FastStart = false;

        //Act
        using (IStandardVideoWriter writer = engine.CreateWriter(request))
        {
            WriteTwoSeconds(writer, audio: false);
            writer.Finish();
        }

        //Assert
        List<string> boxes = EngineTestMedia.TopLevelBoxes(output);
        boxes.IndexOf("moov").Should().BeGreaterThan(boxes.IndexOf("mdat"));
        engine.Analyse(output, TestContext.Current.CancellationToken).HasAudio.Should().BeFalse();
    }

    [Fact]
    public void audio_written_before_video_does_not_deadlock()
    {
        //Arrange
        using var output = new TemporaryFile("audio-first.mp4");

        //Act
        using (IStandardVideoWriter writer = engine.CreateWriter(Request(output, audio: true)))
        {
            writer.WriteAudio(EngineTestMedia.Tone(SampleRate, 2, 2 * SampleRate, 0, 440));
            for (int i = 0; i < 50; i++) writer.WriteFrame(EngineTestMedia.I420Frame(Width, Height, i));
            writer.Finish();
        }

        //Assert
        EngineTestMedia.CountFrames(output).Should().Be(50);
        EngineTestMedia.ProbeStream(output, "a:0", "codec_name").Should().Be("aac");
    }

    [Fact]
    public void keyframe_interval_and_colour_tags_are_applied()
    {
        //Arrange
        using var output = new TemporaryFile("gop.mp4");
        StandardVideoRequest request = Request(output, audio: false);
        request.KeyframeIntervalFrames = 10;
        request.ColorRange = VideoColorRange.Full;
        request.ColorMatrix = VideoColorMatrix.Bt601;

        //Act
        using (IStandardVideoWriter writer = engine.CreateWriter(request))
        {
            WriteTwoSeconds(writer, audio: false);
            writer.Finish();
        }

        //Assert
        List<int> keyframes = EngineTestMedia.KeyframeIndexes(output);
        keyframes[0].Should().Be(0);
        keyframes.Zip(keyframes.Skip(1), (a, b) => b - a).All(gap => gap <= 10).Should().BeTrue();
        (50 - keyframes[keyframes.Count - 1]).Should().BeLessThanOrEqualTo(10);
        VideoStreamInfo video = engine.Analyse(output, TestContext.Current.CancellationToken).PrimaryVideo;
        video.ColorRange.Should().Be(VideoColorRange.Full);
        video.ColorMatrix.Should().Be(VideoColorMatrix.Bt601);
    }

    [Fact]
    public void RotationDegrees_is_written_as_metadata_only()
    {
        //Arrange
        using var output = new TemporaryFile("rotated.mp4");
        StandardVideoRequest request = Request(output, audio: false);
        request.RotationDegrees = 90;

        //Act
        using (IStandardVideoWriter writer = engine.CreateWriter(request))
        {
            WriteTwoSeconds(writer, audio: false);
            writer.Finish();
        }

        //Assert
        VideoStreamInfo video = engine.Analyse(output, TestContext.Current.CancellationToken).PrimaryVideo;
        video.RotationDegrees.Should().Be(90);
        video.Width.Should().Be(Width);
        video.Height.Should().Be(Height);
    }

    [Fact]
    public void hevc_profile_writes_hvc1_tagged_hevc()
    {
        //Arrange
        using var output = new TemporaryFile("hevc.mp4");
        StandardVideoRequest request = Request(output, audio: true);
        request.Profile = StandardVideoProfile.Mp4HevcAac;

        //Act
        using (IStandardVideoWriter writer = engine.CreateWriter(request))
        {
            WriteTwoSeconds(writer, audio: true);
            writer.Finish();
        }

        //Assert
        EngineTestMedia.ProbeStream(output, "v:0", "codec_name").Should().Be("hevc");
        EngineTestMedia.ProbeStream(output, "v:0", "codec_tag_string").Should().Be("hvc1");
        EngineTestMedia.CountFrames(output).Should().Be(50);
    }

    [Fact]
    public void rgba_input_is_converted_to_yuv()
    {
        //Arrange
        using var output = new TemporaryFile("rgba.mp4");
        StandardVideoRequest request = Request(output, audio: false);
        request.InputPixelLayout = VideoPixelLayout.Rgba;
        byte[] red = new byte[Width * Height * 4];
        for (int i = 0; i < red.Length; i += 4)
        {
            red[i] = 255;
            red[i + 3] = 255;
        }

        //Act
        using (IStandardVideoWriter writer = engine.CreateWriter(request))
        {
            for (int i = 0; i < 10; i++)
            {
                writer.WriteFrame(VideoFrameData.CreateRgba(Width, Height, VideoPlane.FromArray(red, Width * 4, Height), TimeSpan.Zero, 0));
            }

            writer.Finish();
        }

        using IVideoFrameSource source = engine.OpenVideo(output, new VideoSourceOptions { PixelLayout = VideoPixelLayout.Rgba });
        source.TryReadFrame(out VideoFrameData decoded);

        //Assert
        ReadOnlySpan<byte> pixel = decoded.Rgba.GetRow(Height / 2).Slice(Width * 2, 4);
        ((int)pixel[0]).Should().BeGreaterThan(230);
        ((int)pixel[1]).Should().BeLessThan(30);
        ((int)pixel[2]).Should().BeLessThan(30);
    }

    [Fact]
    public void Dispose_without_Finish_deletes_the_output()
    {
        //Arrange
        using var output = new TemporaryFile("abandoned.mp4");

        //Act
        using (IStandardVideoWriter writer = engine.CreateWriter(Request(output, audio: true)))
        {
            WriteTwoSeconds(writer, audio: true);
        }

        //Assert
        File.Exists(output).Should().BeFalse();
    }

    [Fact]
    public void WriteFrame_rejects_a_frame_of_the_wrong_size()
    {
        //Arrange
        using var output = new TemporaryFile("wrong.mp4");
        using IStandardVideoWriter writer = engine.CreateWriter(Request(output, audio: false));

        //Act + Assert
        Assert.Throws<ArgumentException>(() => writer.WriteFrame(EngineTestMedia.I420Frame(64, 48, 0)));
    }

    [Fact]
    public void WriteFrame_rejects_a_high_bit_depth_frame()
    {
        //Arrange
        using var output = new TemporaryFile("deep.mp4");
        using IStandardVideoWriter writer = engine.CreateWriter(Request(output, audio: false));
        int chromaWidth = Width / 2;
        int chromaHeight = Height / 2;
        VideoFrameData deep = VideoFrameData.CreateI420(
            Width,
            Height,
            VideoPlane.FromArray(new byte[Width * 2 * Height], Width * 2, Height),
            VideoPlane.FromArray(new byte[chromaWidth * 2 * chromaHeight], chromaWidth * 2, chromaHeight),
            VideoPlane.FromArray(new byte[chromaWidth * 2 * chromaHeight], chromaWidth * 2, chromaHeight),
            TimeSpan.Zero,
            0,
            bitDepth: 10);

        //Act
        var exception = Assert.Throws<ArgumentException>(() => writer.WriteFrame(deep));

        //Assert
        exception.Message.Should().Contain("8-bit");
        writer.FramesWritten.Should().Be(0);
    }

    [Theory]
    [InlineData(VideoPixelLayout.I444)]
    [InlineData(VideoPixelLayout.Gray)]
    public void CreateWriter_refuses_i444_and_gray_input(VideoPixelLayout layout)
    {
        //Arrange
        StandardVideoRequest request = Request("x.mp4", audio: false);
        request.InputPixelLayout = layout;

        //Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => engine.CreateWriter(request));

        //Assert
        exception.ParamName.Should().Be("InputPixelLayout");
    }

    [Fact]
    public void WriteAudio_without_audio_in_the_request_throws()
    {
        //Arrange
        using var output = new TemporaryFile("silent.mp4");
        using IStandardVideoWriter writer = engine.CreateWriter(Request(output, audio: false));

        //Act + Assert
        Assert.Throws<InvalidOperationException>(() => writer.WriteAudio(new float[2]));
    }

    [Fact]
    public void Finish_without_frames_throws()
    {
        //Arrange
        using var output = new TemporaryFile("empty.mp4");
        using IStandardVideoWriter writer = engine.CreateWriter(Request(output, audio: false));

        //Act + Assert
        Assert.Throws<InvalidOperationException>(writer.Finish);
    }

    [Fact]
    public void CreateWriter_refuses_nv12_and_invalid_requests()
    {
        //Arrange
        StandardVideoRequest nv12 = Request("x.mp4", audio: false);
        nv12.InputPixelLayout = VideoPixelLayout.Nv12;
        StandardVideoRequest odd = Request("x.mp4", audio: false);
        odd.Width = 321;

        //Act + Assert
        Assert.Throws<NotSupportedException>(() => engine.CreateWriter(nv12));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.CreateWriter(odd));
    }

    private sealed class ListProgress : IProgress<VideoProgress>
    {
        private readonly List<VideoProgress> reports;

        public ListProgress(List<VideoProgress> reports) => this.reports = reports;

        public void Report(VideoProgress value) => reports.Add(value);
    }
}
