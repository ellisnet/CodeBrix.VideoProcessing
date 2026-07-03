using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Exceptions;
using CodeBrix.VideoProcessing.Extend;
using CodeBrix.VideoProcessing.Pipes;
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
public class AudioTest
{
    private const int BaseTimeoutMilliseconds = 30_000;
    [Fact]
    public void Audio_Remove()
    {
        using var outputFile = new TemporaryFile("out.mp4");

        FFMpeg.Mute(TestResources.Mp4Video, outputFile);
        var analysis = FFProbe.Analyse(outputFile);

        (analysis.VideoStreams).Should().NotBeEmpty();
        (analysis.AudioStreams).Should().BeEmpty();
    }

    [Fact]
    public void Audio_Save()
    {
        using var outputFile = new TemporaryFile("out.mp3");

        FFMpeg.ExtractAudio(TestResources.Mp4Video, outputFile);
        var analysis = FFProbe.Analyse(outputFile);

        (analysis.AudioStreams).Should().NotBeEmpty();
        (analysis.VideoStreams).Should().BeEmpty();
    }

    [Fact]
    public async Task Audio_FromRaw()
    {
        await using var file = File.Open(TestResources.RawAudio, FileMode.Open);
        var memoryStream = new MemoryStream();
        await FFMpegArguments
            .FromPipeInput(new StreamPipeSource(file), options => options.ForceFormat("s16le"))
            .OutputToPipe(new StreamPipeSink(memoryStream), options => options.ForceFormat("mp3"))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessAsynchronously();
    }

    [Fact]
    public void Audio_Add()
    {
        using var outputFile = new TemporaryFile("out.mp4");

        var success = FFMpeg.ReplaceAudio(TestResources.Mp4WithoutAudio, TestResources.Mp3Audio, outputFile);
        var videoAnalysis = FFProbe.Analyse(TestResources.Mp4WithoutAudio);
        var audioAnalysis = FFProbe.Analyse(TestResources.Mp3Audio);
        var outputAnalysis = FFProbe.Analyse(outputFile);

        (success).Should().BeTrue();
        (outputAnalysis.Duration.TotalSeconds).Should().BeApproximately(Math.Max(videoAnalysis.Duration.TotalSeconds, audioAnalysis.Duration.TotalSeconds), 0.15);
        (File.Exists(outputFile)).Should().BeTrue();
    }

    [Fact]
    public void Image_AddAudio()
    {
        using var outputFile = new TemporaryFile("out.mp4");
        FFMpeg.PosterWithAudio(TestResources.PngImage, TestResources.Mp3Audio, outputFile);
        var analysis = FFProbe.Analyse(TestResources.Mp3Audio);
        (analysis.Duration.TotalSeconds).Should().BeGreaterThan(0);
        (File.Exists(outputFile)).Should().BeTrue();
    }

    [Fact]
    public void Audio_ToAAC_Args_Pipe()
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        var samples = new List<IAudioSample> { new PcmAudioSampleWrapper([0, 0]), new PcmAudioSampleWrapper([0, 0]) };

        var audioSamplesSource = new RawAudioPipeSource(samples) { Channels = 2, Format = "s8", SampleRate = 8000 };

        var success = FFMpegArguments
            .FromPipeInput(audioSamplesSource)
            .OutputToFile(outputFile, false, opt => opt
                .WithAudioCodec(AudioCodec.Aac))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously();
        (success).Should().BeTrue();
    }

    [Fact]
    public void Audio_ToLibVorbis_Args_Pipe()
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        var samples = new List<IAudioSample> { new PcmAudioSampleWrapper([0, 0]), new PcmAudioSampleWrapper([0, 0]) };

        var audioSamplesSource = new RawAudioPipeSource(samples) { Channels = 2, Format = "s8", SampleRate = 8000 };

        var success = FFMpegArguments
            .FromPipeInput(audioSamplesSource)
            .OutputToFile(outputFile, false, opt => opt
                .WithAudioCodec(AudioCodec.LibVorbis))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously();
        (success).Should().BeTrue();
    }

    [Fact]
    public async Task Audio_ToAAC_Args_Pipe_Async()
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        var samples = new List<IAudioSample> { new PcmAudioSampleWrapper([0, 0]), new PcmAudioSampleWrapper([0, 0]) };

        var audioSamplesSource = new RawAudioPipeSource(samples) { Channels = 2, Format = "s8", SampleRate = 8000 };

        var success = await FFMpegArguments
            .FromPipeInput(audioSamplesSource)
            .OutputToFile(outputFile, false, opt => opt
                .WithAudioCodec(AudioCodec.Aac))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessAsynchronously();
        (success).Should().BeTrue();
    }

    [Fact]
    public void Audio_ToAAC_Args_Pipe_ValidDefaultConfiguration()
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        var samples = new List<IAudioSample> { new PcmAudioSampleWrapper([0, 0]), new PcmAudioSampleWrapper([0, 0]) };

        var audioSamplesSource = new RawAudioPipeSource(samples);

        var success = FFMpegArguments
            .FromPipeInput(audioSamplesSource)
            .OutputToFile(outputFile, false, opt => opt
                .WithAudioCodec(AudioCodec.Aac))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously();
        (success).Should().BeTrue();
    }

    [Fact]
    public void Audio_ToAAC_Args_Pipe_InvalidChannels()
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        var audioSamplesSource = new RawAudioPipeSource(new List<IAudioSample>()) { Channels = 0 };

        Assert.Throws<FFMpegException>(() => FFMpegArguments
            .FromPipeInput(audioSamplesSource)
            .OutputToFile(outputFile, false, opt => opt
                .WithAudioCodec(AudioCodec.Aac))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously());
    }

    [Fact]
    public void Audio_ToAAC_Args_Pipe_InvalidFormat()
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        var audioSamplesSource = new RawAudioPipeSource(new List<IAudioSample>()) { Format = "s8le" };

        Assert.Throws<FFMpegException>(() => FFMpegArguments
            .FromPipeInput(audioSamplesSource)
            .OutputToFile(outputFile, false, opt => opt
                .WithAudioCodec(AudioCodec.Aac))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously());
    }

    [Fact]
    public void Audio_ToAAC_Args_Pipe_InvalidSampleRate()
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        var audioSamplesSource = new RawAudioPipeSource(new List<IAudioSample>()) { SampleRate = 0 };

        Assert.Throws<FFMpegException>(() => FFMpegArguments
            .FromPipeInput(audioSamplesSource)
            .OutputToFile(outputFile, false, opt => opt
                .WithAudioCodec(AudioCodec.Aac))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously());
    }

    [Fact]
    public void Audio_Pan_ToMono()
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        var success = FFMpegArguments.FromFileInput(TestResources.Mp3Audio)
            .OutputToFile(outputFile, true,
                argumentOptions => argumentOptions
                    .WithAudioFilters(filter => filter.Pan(1, "c0 < 0.9 * c0 + 0.1 * c1")))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously();

        var mediaAnalysis = FFProbe.Analyse(outputFile);

        (success).Should().BeTrue();
        (mediaAnalysis.AudioStreams).Should().HaveCount(1);
        (mediaAnalysis.PrimaryAudioStream!.ChannelLayout).Should().Be("mono");
    }

    [Fact]
    public void Audio_Pan_ToMonoNoDefinitions()
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        var success = FFMpegArguments.FromFileInput(TestResources.Mp3Audio)
            .OutputToFile(outputFile, true,
                argumentOptions => argumentOptions
                    .WithAudioFilters(filter => filter.Pan(1)))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously();

        var mediaAnalysis = FFProbe.Analyse(outputFile);

        (success).Should().BeTrue();
        (mediaAnalysis.AudioStreams).Should().HaveCount(1);
        (mediaAnalysis.PrimaryAudioStream!.ChannelLayout).Should().Be("mono");
    }

    [Fact]
    public void Audio_Pan_ToMonoChannelsToOutputDefinitionsMismatch()
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        Assert.Throws<ArgumentException>(() => FFMpegArguments.FromFileInput(TestResources.Mp3Audio)
            .OutputToFile(outputFile, true,
                argumentOptions => argumentOptions
                    .WithAudioFilters(filter => filter.Pan(1, "c0=c0", "c1=c1")))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously());
    }

    [Fact]
    public void Audio_Pan_ToMonoChannelsLayoutToOutputDefinitionsMismatch()
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        Assert.Throws<FFMpegException>(() => FFMpegArguments.FromFileInput(TestResources.Mp3Audio)
            .OutputToFile(outputFile, true,
                argumentOptions => argumentOptions
                    .WithAudioFilters(filter => filter.Pan("mono", "c0=c0", "c1=c1")))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously());
    }

    [Fact]
    public void Audio_DynamicNormalizer_WithDefaultValues()
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        var success = FFMpegArguments.FromFileInput(TestResources.Mp3Audio)
            .OutputToFile(outputFile, true,
                argumentOptions => argumentOptions
                    .WithAudioFilters(filter => filter.DynamicNormalizer()))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously();

        (success).Should().BeTrue();
    }

    [Fact]
    public void Audio_DynamicNormalizer_WithNonDefaultValues()
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        var success = FFMpegArguments.FromFileInput(TestResources.Mp3Audio)
            .OutputToFile(outputFile, true,
                argumentOptions => argumentOptions
                    .WithAudioFilters(filter => filter.DynamicNormalizer(250, 7, 0.9, 2, 1, false, true, true, 0.5)))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously();

        (success).Should().BeTrue();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(32)]
    [InlineData(8)]
    public void Audio_DynamicNormalizer_FilterWindow(int filterWindow)
    {
        using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

        Assert.Throws<ArgumentOutOfRangeException>(() => FFMpegArguments
            .FromFileInput(TestResources.Mp3Audio)
            .OutputToFile(outputFile, true,
                argumentOptions => argumentOptions
                    .WithAudioFilters(filter => filter.DynamicNormalizer(filterWindow: filterWindow)))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously());
    }
}
