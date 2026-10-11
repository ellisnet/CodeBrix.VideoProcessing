using System;
using System.Collections.Generic;
using CodeBrix.VideoProcessing.Android.Managed;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class AudioResamplerTests
{
    private static float[] Tone(int rate, int frames, double frequency)
    {
        float[] samples = new float[frames];
        for (int i = 0; i < frames; i++) samples[i] = (float)Math.Sin(2 * Math.PI * frequency * i / rate);
        return samples;
    }

    private static double ZeroCrossingFrequency(IReadOnlyList<float> samples, int rate)
    {
        int crossings = 0;
        for (int i = 1; i < samples.Count; i++)
        {
            if (samples[i - 1] < 0 && samples[i] >= 0) crossings++;
        }

        return crossings * (double)rate / (samples.Count - 1);
    }

    [Fact]
    public void equal_rates_pass_through_unchanged()
    {
        //Arrange
        var resampler = new AudioResampler(48000, 48000, 2);
        var output = new List<float>();

        //Act
        resampler.Process(new[] { 1f, 2f, 3f, 4f }, output);
        resampler.Flush(output);

        //Assert
        resampler.IsPassThrough.Should().BeTrue();
        output.Should().Equal(1f, 2f, 3f, 4f);
    }

    [Fact]
    public void doubling_the_rate_interpolates_midpoints()
    {
        //Arrange
        var resampler = new AudioResampler(1000, 2000, 1);
        var output = new List<float>();

        //Act
        resampler.Process(new[] { 0f, 1f, 3f }, output);
        resampler.Flush(output);

        //Assert
        output.Should().Equal(0f, 0.5f, 1f, 2f, 3f, 3f);
    }

    [Theory]
    [InlineData(48000, 22050, 48000)]
    [InlineData(44100, 48000, 44100)]
    [InlineData(22050, 8000, 1000)]
    public void the_output_length_is_the_input_length_at_the_new_rate(int input, int output, int frames)
    {
        //Arrange
        var resampler = new AudioResampler(input, output, 2);
        var produced = new List<float>();
        float[] samples = new float[frames * 2];

        //Act
        resampler.Process(samples, produced);
        resampler.Flush(produced);

        //Assert
        long expected = (long)Math.Ceiling((double)frames * output / input);
        (produced.Count / 2).Should().Be((int)expected);
    }

    [Fact]
    public void chunked_input_gives_the_same_output_as_one_block()
    {
        //Arrange
        float[] tone = Tone(44100, 4410, 1000);
        var whole = new List<float>();
        var chunked = new List<float>();
        var one = new AudioResampler(44100, 48000, 1);
        var many = new AudioResampler(44100, 48000, 1);

        //Act
        one.Process(tone, whole);
        one.Flush(whole);
        for (int start = 0; start < tone.Length; start += 333)
        {
            many.Process(tone.AsSpan(start, Math.Min(333, tone.Length - start)), chunked);
        }

        many.Flush(chunked);

        //Assert
        chunked.Should().Equal(whole);
    }

    [Fact]
    public void a_tone_keeps_its_frequency()
    {
        //Arrange
        var resampler = new AudioResampler(48000, 22050, 1);
        var output = new List<float>();

        //Act
        resampler.Process(Tone(48000, 48000, 440), output);
        resampler.Flush(output);

        //Assert
        ZeroCrossingFrequency(output, 22050).Should().BeApproximately(440, 2);
    }

    [Fact]
    public void partial_frames_are_refused()
        => Assert.Throws<ArgumentException>(() => new AudioResampler(48000, 44100, 2).Process(new float[3], new List<float>()));
}
