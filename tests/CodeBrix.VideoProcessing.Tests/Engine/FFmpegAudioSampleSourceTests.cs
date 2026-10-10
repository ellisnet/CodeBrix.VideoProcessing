using System;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Engine;
using CodeBrix.VideoProcessing.Tests.Resources;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests.Engine;

public class FFmpegAudioSampleSourceTests
{
    private readonly FFmpegVideoProcessor engine = new FFmpegVideoProcessor();

    private static long ReadAll(IAudioSampleSource source, out float peak)
    {
        float[] buffer = new float[4096 * source.Info.Channels];
        long frames = 0;
        peak = 0;
        int read;
        while ((read = source.Read(buffer)) > 0)
        {
            frames += read;
            for (int i = 0; i < read * source.Info.Channels; i++) peak = Math.Max(peak, Math.Abs(buffer[i]));
        }

        return frames;
    }

    [Fact]
    public void Read_delivers_the_whole_stream_at_the_source_format()
    {
        //Arrange
        using IAudioSampleSource source = engine.OpenAudio(TestResources.Mp4WithoutVideo);

        //Act
        long frames = ReadAll(source, out float peak);

        //Assert
        source.Info.SampleRate.Should().Be(44100);
        source.Info.Channels.Should().Be(2);
        source.Info.SourceCodecName.Should().Be("aac");
        ((double)frames).Should().BeApproximately(44100 * 10.008, 2048);
        peak.Should().BeGreaterThan(0.01f);
    }

    [Fact]
    public void Read_resamples_and_remixes_on_request()
    {
        //Arrange
        using IAudioSampleSource source = engine.OpenAudio(TestResources.Mp4WithoutVideo, new AudioSourceOptions { SampleRate = 16000, Channels = 1 });

        //Act
        long frames = ReadAll(source, out float _);

        //Assert
        source.Info.SampleRate.Should().Be(16000);
        source.Info.Channels.Should().Be(1);
        ((double)frames).Should().BeApproximately(16000 * 10.008, 1024);
    }

    [Fact]
    public void Seek_skips_the_start()
    {
        //Arrange
        using IAudioSampleSource source = engine.OpenAudio(TestResources.Mp4WithoutVideo, new AudioSourceOptions { SampleRate = 8000, Channels = 1 });

        //Act
        source.Seek(TimeSpan.FromSeconds(8));
        long frames = ReadAll(source, out float _);

        //Assert
        ((double)frames).Should().BeApproximately(8000 * 2.008, 400);
    }

    [Fact]
    public void Read_rejects_a_buffer_that_is_not_whole_frames()
    {
        //Arrange
        using IAudioSampleSource source = engine.OpenAudio(TestResources.Mp4WithoutVideo);

        //Act + Assert
        Assert.Throws<ArgumentException>(() => source.Read(new float[3]));
    }
}
