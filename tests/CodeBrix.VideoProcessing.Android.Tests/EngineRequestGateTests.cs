using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

/// <summary>A stand-in engine with the Android engine's capability set, for the request gate.</summary>
internal sealed class StubProcessor : IVideoProcessor
{
    public StubProcessor(bool hevc, bool p010 = false) => Capabilities = AndroidEngineCapabilities.Compute(hevc, p010);

    public string Name => "android-mediacodec";

    public VideoProcessorCapabilities Capabilities { get; }

    public MediaInfo Analyse(string path, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<MediaInfo> AnalyseAsync(string path, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public VideoFrameData Snapshot(string path, TimeSpan at, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public IVideoFrameSource OpenVideo(string path, VideoSourceOptions options = null) => throw new NotImplementedException();

    public IAudioSampleSource OpenAudio(string path, AudioSourceOptions options = null) => throw new NotImplementedException();

    public IStandardVideoWriter CreateWriter(StandardVideoRequest request, IProgress<VideoProgress> progress = null) => throw new NotImplementedException();

    public void TranscodeFile(TranscodeRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public void TrimFile(TrimRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public void ConcatenateFiles(ConcatenateRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}

public class EngineRequestGateTests
{
    private static StandardVideoRequest Writer(VideoPixelLayout layout = VideoPixelLayout.I420, StandardVideoProfile profile = StandardVideoProfile.Mp4H264Aac) =>
        new StandardVideoRequest { OutputPath = "out.mp4", Width = 64, Height = 48, FrameRate = new FrameRate(30, 1), InputPixelLayout = layout, Profile = profile };

    [Theory]
    [InlineData(VideoPixelLayout.I420)]
    [InlineData(VideoPixelLayout.Nv12)]
    [InlineData(VideoPixelLayout.Rgba)]
    [InlineData(VideoPixelLayout.Gray)]
    public void the_offered_frame_layouts_pass(VideoPixelLayout layout)
    {
        //Act
        VideoSourceOptions options = EngineRequestGate.CheckSourceOptions(new StubProcessor(false), new VideoSourceOptions { PixelLayout = layout });

        //Assert
        options.PixelLayout.Should().Be(layout);
    }

    [Fact]
    public void null_options_mean_the_defaults()
        => EngineRequestGate.CheckSourceOptions(new StubProcessor(false), null).PixelLayout.Should().Be(VideoPixelLayout.I420);

    [Fact]
    public void i444_deep_samples_and_alpha_are_not_supported()
    {
        //Arrange
        var engine = new StubProcessor(true);

        //Act
        var i444 = Assert.Throws<NotSupportedException>(() => EngineRequestGate.CheckSourceOptions(engine, new VideoSourceOptions { PixelLayout = VideoPixelLayout.I444 }));
        var deep = Assert.Throws<NotSupportedException>(() => EngineRequestGate.CheckSourceOptions(engine, new VideoSourceOptions { BitDepth = 10 }));
        var alpha = Assert.Throws<NotSupportedException>(() => EngineRequestGate.CheckSourceOptions(engine, new VideoSourceOptions { IncludeAlpha = true }));

        //Assert
        i444.Message.Should().Contain("I444FrameSource");
        deep.Message.Should().Contain("HighBitDepthFrameSource");
        alpha.Message.Should().Contain("AlphaFrameSource");
        alpha.Message.Should().Contain("'android-mediacodec'");
    }

    [Theory]
    [InlineData(VideoPixelLayout.I420)]
    [InlineData(VideoPixelLayout.Nv12)]
    [InlineData(VideoPixelLayout.Gray)]
    public void ten_bit_frames_pass_on_a_device_with_a_p010_decoder(VideoPixelLayout layout)
    {
        //Act
        VideoSourceOptions options = EngineRequestGate.CheckSourceOptions(new StubProcessor(false, p010: true), new VideoSourceOptions { PixelLayout = layout, BitDepth = 10 });

        //Assert
        options.BitDepth.Should().Be(10);
        options.PixelLayout.Should().Be(layout);
    }

    [Fact]
    public void twelve_bit_frames_are_refused_even_with_a_p010_decoder()
    {
        //Act
        var refused = Assert.Throws<NotSupportedException>(() => EngineRequestGate.CheckSourceOptions(new StubProcessor(true, p010: true), new VideoSourceOptions { BitDepth = 12 }));

        //Assert
        refused.Message.Should().Contain("12-bit");
        refused.Message.Should().Contain("'android-mediacodec'");
        refused.Message.Should().Contain("P010");
    }

    [Fact]
    public void twelve_bit_frames_without_a_p010_decoder_name_the_missing_capability()
        => Assert.Throws<NotSupportedException>(() => EngineRequestGate.CheckSourceOptions(new StubProcessor(false), new VideoSourceOptions { BitDepth = 12 }))
            .Message.Should().Contain("HighBitDepthFrameSource");

    [Fact]
    public void rgba_at_ten_bits_is_the_contract_argument_error_even_with_a_p010_decoder()
        => Assert.Throws<ArgumentOutOfRangeException>(() => EngineRequestGate.CheckSourceOptions(new StubProcessor(false, p010: true), new VideoSourceOptions { PixelLayout = VideoPixelLayout.Rgba, BitDepth = 10 }))
            .ParamName.Should().Be("BitDepth");

    [Fact]
    public void i444_and_alpha_stay_refused_with_a_p010_decoder()
    {
        //Arrange
        var engine = new StubProcessor(true, p010: true);

        //Assert
        Assert.Throws<NotSupportedException>(() => EngineRequestGate.CheckSourceOptions(engine, new VideoSourceOptions { PixelLayout = VideoPixelLayout.I444, BitDepth = 10 }))
            .Message.Should().Contain("I444FrameSource");
        Assert.Throws<NotSupportedException>(() => EngineRequestGate.CheckSourceOptions(engine, new VideoSourceOptions { BitDepth = 10, IncludeAlpha = true }))
            .Message.Should().Contain("AlphaFrameSource");
    }

    [Fact]
    public void alpha_is_ignored_for_rgba_as_the_contract_says()
        => EngineRequestGate.CheckSourceOptions(new StubProcessor(false), new VideoSourceOptions { PixelLayout = VideoPixelLayout.Rgba, IncludeAlpha = true }).IncludeAlpha.Should().BeTrue();

    [Fact]
    public void invalid_options_throw_the_contract_exception()
        => Assert.Throws<ArgumentOutOfRangeException>(() => EngineRequestGate.CheckSourceOptions(new StubProcessor(false), new VideoSourceOptions { StartTime = TimeSpan.FromSeconds(-1) }));

    [Fact]
    public void hevc_needs_an_hevc_encoder()
    {
        //Assert
        Assert.Throws<NotSupportedException>(() => EngineRequestGate.CheckWriterRequest(new StubProcessor(false), Writer(profile: StandardVideoProfile.Mp4HevcAac)));
        EngineRequestGate.CheckWriterRequest(new StubProcessor(true), Writer(profile: StandardVideoProfile.Mp4HevcAac));
        Assert.Throws<NotSupportedException>(() => EngineRequestGate.CheckTranscode(new StubProcessor(false), new TranscodeRequest { InputPath = "in.mp4", Output = Writer(profile: StandardVideoProfile.Mp4HevcAac) }));
    }

    [Fact]
    public void nv12_and_rgba_writers_are_accepted()
    {
        //Act
        EngineRequestGate.CheckWriterRequest(new StubProcessor(false), Writer(VideoPixelLayout.Nv12));
        EngineRequestGate.CheckWriterRequest(new StubProcessor(false), Writer(VideoPixelLayout.Rgba));
    }

    [Fact]
    public void writer_requests_are_validated()
    {
        //Assert
        Assert.Throws<ArgumentNullException>(() => EngineRequestGate.CheckWriterRequest(new StubProcessor(false), null));
        Assert.Throws<ArgumentOutOfRangeException>(() => EngineRequestGate.CheckWriterRequest(new StubProcessor(false), new StandardVideoRequest { OutputPath = "o.mp4", Width = 63, Height = 48, FrameRate = new FrameRate(30, 1) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => EngineRequestGate.CheckWriterRequest(new StubProcessor(false), Writer(VideoPixelLayout.Gray)));
    }

    [Fact]
    public void whole_file_requests_are_validated()
    {
        //Arrange
        var engine = new StubProcessor(false);

        //Assert
        Assert.Throws<ArgumentNullException>(() => EngineRequestGate.CheckTrim(engine, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => EngineRequestGate.CheckTrim(engine, new TrimRequest { InputPath = "a", OutputPath = "b", From = TimeSpan.FromSeconds(2), To = TimeSpan.FromSeconds(1) }));
        Assert.Throws<ArgumentException>(() => EngineRequestGate.CheckConcatenate(engine, new ConcatenateRequest { OutputPath = "b" }));
        Assert.Throws<ArgumentNullException>(() => EngineRequestGate.CheckTranscode(engine, null));
        Assert.Throws<ArgumentException>(() => EngineRequestGate.CheckTranscode(engine, new TranscodeRequest()));
        Assert.Throws<ArgumentOutOfRangeException>(() => EngineRequestGate.CheckSnapshot(engine, "a.mp4", TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentException>(() => EngineRequestGate.CheckSnapshot(engine, "", TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() => EngineRequestGate.CheckPath(null, "path"));
    }

    [Fact]
    public void the_whole_file_operations_pass_on_the_android_capability_set()
    {
        //Arrange
        var engine = new StubProcessor(false);

        //Act
        EngineRequestGate.CheckTrim(engine, new TrimRequest { InputPath = "a", OutputPath = "b", To = TimeSpan.FromSeconds(1) });
        EngineRequestGate.CheckConcatenate(engine, new ConcatenateRequest { InputPaths = { "a", "b" }, OutputPath = "c" });
        EngineRequestGate.CheckTranscode(engine, new TranscodeRequest { InputPath = "a", Output = { OutputPath = "b" } });
        EngineRequestGate.CheckSnapshot(engine, "a.mp4", TimeSpan.Zero);
    }

    [Fact]
    public void writer_frames_must_match_the_request()
    {
        //Arrange
        StandardVideoRequest request = Writer();
        VideoFrameData good = I420(64, 48);
        VideoFrameData wrongSize = I420(32, 48);
        VideoFrameData gray = VideoFrameData.CreateGray(64, 48, VideoPlane.FromArray(new byte[64 * 48], 64, 48), TimeSpan.Zero, 0);
        VideoFrameData deep = VideoFrameData.CreateI420(64, 48,
            VideoPlane.FromArray(new byte[128 * 48], 128, 48), VideoPlane.FromArray(new byte[64 * 24], 64, 24), VideoPlane.FromArray(new byte[64 * 24], 64, 24),
            TimeSpan.Zero, 0, default, 10);

        //Act
        EngineRequestGate.CheckWriterFrame(request, good);

        //Assert
        Assert.Throws<ArgumentException>(() => EngineRequestGate.CheckWriterFrame(request, wrongSize)).Message.Should().Contain("32x48");
        Assert.Throws<ArgumentException>(() => EngineRequestGate.CheckWriterFrame(request, gray)).Message.Should().Contain("Gray");
        Assert.Throws<ArgumentException>(() => EngineRequestGate.CheckWriterFrame(request, deep)).Message.Should().Contain("10-bit");
        Assert.Throws<ArgumentException>(() => EngineRequestGate.CheckWriterFrame(request, default));
    }

    private static VideoFrameData I420(int width, int height) =>
        VideoFrameData.CreateI420(width, height,
            VideoPlane.FromArray(new byte[width * height], width, height),
            VideoPlane.FromArray(new byte[(width / 2) * (height / 2)], width / 2, height / 2),
            VideoPlane.FromArray(new byte[(width / 2) * (height / 2)], width / 2, height / 2),
            TimeSpan.Zero, 0);
}
