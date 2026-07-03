using CodeBrix.VideoProcessing.Exceptions;
using CodeBrix.VideoProcessing.Helpers;
using CodeBrix.VideoProcessing.Tests.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SilverAssertions;
using CodeBrix.VideoProcessing.Tests.Utilities;

namespace CodeBrix.VideoProcessing.Tests;
public class FFProbeTests
{
    [Fact]
    public async Task Audio_FromStream_Duration()
    {
        var fileAnalysis = await FFProbe.AnalyseAsync(TestResources.WebmVideo, cancellationToken: TestContext.Current.CancellationToken);
        await using var inputStream = File.OpenRead(TestResources.WebmVideo);
        var streamAnalysis = await FFProbe.AnalyseAsync(inputStream, cancellationToken: TestContext.Current.CancellationToken);
        (fileAnalysis.Duration == streamAnalysis.Duration).Should().BeTrue();
    }

    [Fact]
    public void FrameAnalysis_Sync()
    {
        var frameAnalysis = FFProbe.GetFrames(TestResources.WebmVideo);

        (frameAnalysis.Frames).Should().HaveCount(90);
        (frameAnalysis.Frames.All(f => f.PixelFormat == "yuv420p")).Should().BeTrue();
        (frameAnalysis.Frames.All(f => f.Height == 360)).Should().BeTrue();
        (frameAnalysis.Frames.All(f => f.Width == 640)).Should().BeTrue();
        (frameAnalysis.Frames.All(f => f.MediaType == "video")).Should().BeTrue();
    }

    [Fact]
    public async Task FrameAnalysis_Async()
    {
        var frameAnalysis = await FFProbe.GetFramesAsync(TestResources.WebmVideo, cancellationToken: TestContext.Current.CancellationToken);

        (frameAnalysis.Frames).Should().HaveCount(90);
        (frameAnalysis.Frames.All(f => f.PixelFormat == "yuv420p")).Should().BeTrue();
        (frameAnalysis.Frames.All(f => f.Height == 360)).Should().BeTrue();
        (frameAnalysis.Frames.All(f => f.Width == 640)).Should().BeTrue();
        (frameAnalysis.Frames.All(f => f.MediaType == "video")).Should().BeTrue();
    }

    [Fact]
    public async Task PacketAnalysis_Async()
    {
        var packetAnalysis = await FFProbe.GetPacketsAsync(TestResources.WebmVideo, cancellationToken: TestContext.Current.CancellationToken);
        var packets = packetAnalysis.Packets;
        (packets).Should().HaveCount(96);
        (packets.All(f => f.CodecType == "video")).Should().BeTrue();
        Assert.StartsWith("K_", packets[0].Flags);
        (packets.Last().Size).Should().Be(1362);
    }

    [Fact]
    public void PacketAnalysis_Sync()
    {
        var packets = FFProbe.GetPackets(TestResources.WebmVideo).Packets;

        (packets).Should().HaveCount(96);
        (packets.All(f => f.CodecType == "video")).Should().BeTrue();
        Assert.StartsWith("K_", packets[0].Flags);
        (packets.Last().Size).Should().Be(1362);
    }

    [Fact]
    public void PacketAnalysisAudioVideo_Sync()
    {
        var packets = FFProbe.GetPackets(TestResources.Mp4Video).Packets;

        (packets).Should().HaveCount(216);
        var actual = packets.Select(f => f.CodecType).Distinct().ToList();
        var expected = new List<string> { "audio", "video" };
        (actual).Should().BeEquivalentTo(expected);
        (packets.Where(t => t.CodecType == "audio").All(f => f.Flags.StartsWith("K_"))).Should().BeTrue();
        (packets.Count(t => t.CodecType == "video")).Should().Be(75);
        (packets.Count(t => t.CodecType == "audio")).Should().Be(141);
    }

    [Theory]
    [InlineData("0:00:03.008000", 0, 0, 0, 3, 8)]
    [InlineData("05:12:59.177", 0, 5, 12, 59, 177)]
    [InlineData("149:07:50.911750", 6, 5, 7, 50, 911)]
    [InlineData("00:00:00.83", 0, 0, 0, 0, 830)]
    public void MediaAnalysis_ParseDuration(string duration, int expectedDays, int expectedHours, int expectedMinutes, int expectedSeconds,
        int expectedMilliseconds)
    {
        var ffprobeStream = new FFProbeStream { Duration = duration };

        var parsedDuration = MediaAnalysisUtils.ParseDuration(ffprobeStream.Duration);

        (parsedDuration.Days).Should().Be(expectedDays);
        (parsedDuration.Hours).Should().Be(expectedHours);
        (parsedDuration.Minutes).Should().Be(expectedMinutes);
        (parsedDuration.Seconds).Should().Be(expectedSeconds);
        (parsedDuration.Milliseconds).Should().Be(expectedMilliseconds);
    }

        [Fact(Skip = "Consistently fails on GitHub Workflow ubuntu agents")]
    public async Task Uri_Duration()
    {
        var fileAnalysis = await FFProbe.AnalyseAsync(new Uri("https://github.com/rosenbjerg/CodeBrix.VideoProcessing/raw/master/CodeBrix.VideoProcessing.Tests/Resources/input_3sec.webm"),
            cancellationToken: TestContext.Current.CancellationToken);
        (fileAnalysis).Should().NotBeNull();
    }

    [Fact]
    public void Probe_Success()
    {
        var info = FFProbe.Analyse(TestResources.Mp4Video);
        (info.Duration.Seconds).Should().Be(3);
        (info.Chapters).Should().BeEmpty();

        (info.PrimaryAudioStream!.ChannelLayout).Should().Be("5.1");
        (info.PrimaryAudioStream.Channels).Should().Be(6);
        (info.PrimaryAudioStream.CodecLongName).Should().Be("AAC (Advanced Audio Coding)");
        (info.PrimaryAudioStream.CodecName).Should().Be("aac");
        (info.PrimaryAudioStream.Profile).Should().Be("LC");
        (info.PrimaryAudioStream.BitRate).Should().Be(377351);
        (info.PrimaryAudioStream.SampleRateHz).Should().Be(48000);
        (info.PrimaryAudioStream.CodecTagString).Should().Be("mp4a");
        (info.PrimaryAudioStream.CodecTag).Should().Be("0x6134706d");

        (info.PrimaryVideoStream!.BitRate).Should().Be(1471810);
        (info.PrimaryVideoStream.DisplayAspectRatio.Width).Should().Be(16);
        (info.PrimaryVideoStream.DisplayAspectRatio.Height).Should().Be(9);
        (info.PrimaryVideoStream.SampleAspectRatio.Width).Should().Be(1);
        (info.PrimaryVideoStream.SampleAspectRatio.Height).Should().Be(1);
        (info.PrimaryVideoStream.PixelFormat).Should().Be("yuv420p");
        (info.PrimaryVideoStream.Level).Should().Be(31);
        (info.PrimaryVideoStream.Width).Should().Be(1280);
        (info.PrimaryVideoStream.Height).Should().Be(720);
        (info.PrimaryVideoStream.AvgFrameRate).Should().Be(25);
        (info.PrimaryVideoStream.FrameRate).Should().Be(25);
        (info.PrimaryVideoStream.CodecLongName).Should().Be("H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10");
        (info.PrimaryVideoStream.CodecName).Should().Be("h264");
        (info.PrimaryVideoStream.BitsPerRawSample).Should().Be(8);
        (info.PrimaryVideoStream.Profile).Should().Be("Main");
        (info.PrimaryVideoStream.CodecTagString).Should().Be("avc1");
        (info.PrimaryVideoStream.CodecTag).Should().Be("0x31637661");
    }

    [Fact]
    public void Probe_Rotation()
    {
        var info = FFProbe.Analyse(TestResources.Mp4Video);
        (info.PrimaryVideoStream).Should().NotBeNull();
        (info.PrimaryVideoStream.Rotation).Should().Be(0);

        info = FFProbe.Analyse(TestResources.Mp4VideoRotation);
        (info.PrimaryVideoStream).Should().NotBeNull();
        (info.PrimaryVideoStream.Rotation).Should().Be(90);
    }

    [Fact]
    public void Probe_Rotation_Negative_Value()
    {
        var info = FFProbe.Analyse(TestResources.Mp4VideoRotationNegative);
        (info.PrimaryVideoStream).Should().NotBeNull();
        (info.PrimaryVideoStream.Rotation).Should().Be(-90);
    }

    [Fact]
    public async Task Probe_Async_Success()
    {
        var info = await FFProbe.AnalyseAsync(TestResources.Mp4Video, cancellationToken: TestContext.Current.CancellationToken);
        (info.Duration.Seconds).Should().Be(3);
        (info.PrimaryVideoStream).Should().NotBeNull();
        (info.PrimaryVideoStream.BitDepth).Should().Be(8);
        // This video's audio stream is AAC, which is lossy, so bit depth is meaningless.
        (info.PrimaryAudioStream).Should().NotBeNull();
        (info.PrimaryAudioStream.BitDepth).Should().BeNull();
    }

    [Fact]
    public void Probe_Success_FromStream()
    {
        using var stream = File.OpenRead(TestResources.WebmVideo);
        var info = FFProbe.Analyse(stream);
        (info.Duration.Seconds).Should().Be(3);
        // This video has no audio stream.
        (info.PrimaryAudioStream).Should().BeNull();
    }

    [Fact]
    public async Task Probe_Success_FromStream_Async()
    {
        await using var stream = File.OpenRead(TestResources.WebmVideo);
        var info = await FFProbe.AnalyseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        (info.Duration.Seconds).Should().Be(3);
    }

    [Fact]
    public void Probe_HDR()
    {
        var info = FFProbe.Analyse(TestResources.HdrVideo);

        (info.PrimaryVideoStream).Should().NotBeNull();
        (info.PrimaryVideoStream.ColorRange).Should().Be("tv");
        (info.PrimaryVideoStream.ColorSpace).Should().Be("bt2020nc");
        (info.PrimaryVideoStream.ColorTransfer).Should().Be("arib-std-b67");
        (info.PrimaryVideoStream.ColorPrimaries).Should().Be("bt2020");
    }

    [Fact]
    public async Task Probe_Success_Subtitle_Async()
    {
        var info = await FFProbe.AnalyseAsync(TestResources.SrtSubtitle, cancellationToken: TestContext.Current.CancellationToken);
        (info.PrimarySubtitleStream).Should().NotBeNull();
        (info.SubtitleStreams).Should().HaveCount(1);
        (info.AudioStreams).Should().BeEmpty();
        (info.VideoStreams).Should().BeEmpty();
        // BitDepth is meaningless for subtitles
        (info.SubtitleStreams[0].BitDepth).Should().BeNull();
    }

    [Fact]
    public async Task Probe_Success_Disposition_Async()
    {
        var info = await FFProbe.AnalyseAsync(TestResources.Mp4Video, cancellationToken: TestContext.Current.CancellationToken);
        (info.PrimaryAudioStream).Should().NotBeNull();
        (info.PrimaryAudioStream.Disposition).Should().NotBeNull();
        (info.PrimaryAudioStream.Disposition["default"]).Should().BeTrue();
        (info.PrimaryAudioStream.Disposition["forced"]).Should().BeFalse();
    }

    [Fact]
    public async Task Probe_Success_Mp3AudioBitDepthNull_Async()
    {
        var info = await FFProbe.AnalyseAsync(TestResources.Mp3Audio, cancellationToken: TestContext.Current.CancellationToken);
        (info.PrimaryAudioStream).Should().NotBeNull();
        // mp3 is lossy, so bit depth is meaningless.
        (info.PrimaryAudioStream.BitDepth).Should().BeNull();
    }

    [Fact]
    public async Task Probe_Success_VocAudioBitDepth_Async()
    {
        var info = await FFProbe.AnalyseAsync(TestResources.AiffAudio, cancellationToken: TestContext.Current.CancellationToken);
        (info.PrimaryAudioStream).Should().NotBeNull();
        (info.PrimaryAudioStream.BitDepth).Should().Be(16);
    }

    [Fact]
    public async Task Probe_Success_MkvVideoBitDepth_Async()
    {
        var info = await FFProbe.AnalyseAsync(TestResources.MkvVideo, cancellationToken: TestContext.Current.CancellationToken);
        (info.PrimaryVideoStream).Should().NotBeNull();
        (info.PrimaryVideoStream.BitDepth).Should().Be(8);

        (info.PrimaryAudioStream).Should().NotBeNull();
        (info.PrimaryAudioStream.BitDepth).Should().BeNull();
    }

    [Fact]
    public async Task Probe_Success_24BitWavBitDepth_Async()
    {
        var info = await FFProbe.AnalyseAsync(TestResources.Wav24Bit, cancellationToken: TestContext.Current.CancellationToken);
        (info.PrimaryAudioStream).Should().NotBeNull();
        (info.PrimaryAudioStream.BitDepth).Should().Be(24);
    }

    [Fact]
    public async Task Probe_Success_32BitWavBitDepth_Async()
    {
        var info = await FFProbe.AnalyseAsync(TestResources.Wav32Bit, cancellationToken: TestContext.Current.CancellationToken);
        (info.PrimaryAudioStream).Should().NotBeNull();
        (info.PrimaryAudioStream.BitDepth).Should().Be(32);
    }

    [Fact]
    public void Probe_Success_Custom_Arguments()
    {
        var info = FFProbe.Analyse(TestResources.Mp4Video, customArguments: "-headers \"Hello: World\"");
        (info.Duration.Seconds).Should().Be(3);
    }

    [Fact]
    public async Task Parallel_FFProbe_Cancellation_Should_Throw_Only_OperationCanceledException()
    {
        // Warm up CodeBrix.VideoProcessing environment
        FFProbeHelper.VerifyFFProbeExists(GlobalFFOptions.Current);

        var mp4 = TestResources.Mp4Video;
        if (!File.Exists(mp4))
        {
            Assert.Skip($"Test video not found: {mp4}");
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        var tasks = Enumerable.Range(0, 50).Select(x => Task.Run(async () =>
        {
            await semaphore.WaitAsync(cts.Token);
            try
            {
                var analysis = await FFProbe.AnalyseAsync(mp4, cancellationToken: cts.Token);
                return analysis;
            }
            finally
            {
                semaphore.Release();
            }
        }, cts.Token)).ToList();

        // Wait for 2 tasks to finish, then cancel all
        await Task.WhenAny(tasks);
        await Task.WhenAny(tasks);
        await cts.CancelAsync();

        var exceptions = new List<Exception>();
        foreach (var task in tasks)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }
        }

        (exceptions).Should().NotBeEmpty();

        // Check that all exceptions are OperationCanceledException
        (exceptions).Should().AllBeOfType<OperationCanceledException>();
    }

    [Fact]
    public async Task FFProbe_Should_Throw_FFMpegException_When_Exits_With_Non_Zero_Code()
    {
        var input = TestResources.SrtSubtitle; //non media file
        await Assert.ThrowsAsync<FFMpegException>(async () => await FFProbe.AnalyseAsync(input,
            cancellationToken: TestContext.Current.CancellationToken, customArguments: "--some-invalid-argument"));
    }
}
