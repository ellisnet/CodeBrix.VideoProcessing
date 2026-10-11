using CodeBrix.VideoProcessing.Android.Managed;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class AudioChannelMixerTests
{
    [Fact]
    public void mono_is_copied_to_every_output_channel()
    {
        //Arrange
        float[] output = new float[6];

        //Act
        AudioChannelMixer.Mix(new[] { 0.1f, 0.2f, 0.3f }, 1, output, 2, 3);

        //Assert
        output.Should().Equal(0.1f, 0.1f, 0.2f, 0.2f, 0.3f, 0.3f);
    }

    [Fact]
    public void a_mono_output_averages_every_source_channel()
    {
        //Arrange
        float[] output = new float[2];

        //Act
        AudioChannelMixer.Mix(new[] { 0.2f, 0.4f, -1f, 1f }, 2, output, 1, 2);

        //Assert
        output[0].Should().BeApproximately(0.3f, 1e-6f);
        output[1].Should().Be(0f);
    }

    [Fact]
    public void extra_source_channels_are_dropped_and_extra_outputs_are_silent()
    {
        //Arrange
        float[] down = new float[2];
        float[] up = new float[4];

        //Act
        AudioChannelMixer.Mix(new[] { 1f, 2f, 3f, 4f, 5f, 6f }, 6, down, 2, 1);
        AudioChannelMixer.Mix(new[] { 1f, 2f }, 2, up, 4, 1);

        //Assert
        down.Should().Equal(1f, 2f);
        up.Should().Equal(1f, 2f, 0f, 0f);
    }

    [Fact]
    public void the_same_count_is_copied()
    {
        //Arrange
        float[] output = new float[4];

        //Act
        AudioChannelMixer.Mix(new[] { 1f, 2f, 3f, 4f }, 2, output, 2, 2);

        //Assert
        output.Should().Equal(1f, 2f, 3f, 4f);
    }
}
