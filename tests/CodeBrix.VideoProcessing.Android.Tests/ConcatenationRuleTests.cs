using System.Collections.Generic;
using CodeBrix.VideoProcessing.Android.Managed;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class ConcatenationRuleTests
{
    private static TrackDescription Video(int width = 320, byte csd = 1) =>
        new TrackDescription { Mime = "video/avc", Width = width, Height = 240, Csd0 = new byte[] { 0x67, csd }, Csd1 = new byte[] { 0x68 } };

    private static TrackDescription Audio(int rate = 48000, int channels = 2) =>
        new TrackDescription { Mime = "audio/mp4a-latm", SampleRate = rate, ChannelCount = channels, Csd0 = new byte[] { 0x11, 0x90 } };

    [Fact]
    public void identical_inputs_can_be_copied()
    {
        //Arrange
        var inputs = new List<(TrackDescription, TrackDescription)> { (Video(), Audio()), (Video(), Audio()), (Video(), Audio()) };

        //Act
        bool copy = ConcatenationRule.CanCopy(inputs, out string reason);

        //Assert
        copy.Should().BeTrue();
        reason.Should().BeNull();
    }

    [Theory]
    [InlineData("size")]
    [InlineData("csd")]
    [InlineData("rate")]
    [InlineData("channels")]
    [InlineData("noaudio")]
    [InlineData("novideo")]
    [InlineData("codec")]
    public void any_difference_forces_a_re_encode(string difference)
    {
        //Arrange
        (TrackDescription, TrackDescription) second = difference switch
        {
            "size" => (Video(width: 640), Audio()),
            "csd" => (Video(csd: 2), Audio()),
            "rate" => (Video(), Audio(rate: 44100)),
            "channels" => (Video(), Audio(channels: 1)),
            "noaudio" => (Video(), null),
            "novideo" => (null, Audio()),
            _ => (new TrackDescription { Mime = "video/hevc", Width = 320, Height = 240, Csd0 = new byte[] { 0x67, 1 }, Csd1 = new byte[] { 0x68 } }, Audio())
        };
        var inputs = new List<(TrackDescription, TrackDescription)> { (Video(), Audio()), second };

        //Act
        bool copy = ConcatenationRule.CanCopy(inputs, out string reason);

        //Assert
        copy.Should().BeFalse();
        reason.ToLowerInvariant().Should().Contain("input 1");
    }

    [Fact]
    public void empty_input_lists_and_trackless_inputs_cannot_be_copied()
    {
        //Assert
        ConcatenationRule.CanCopy(new List<(TrackDescription, TrackDescription)>(), out _).Should().BeFalse();
        ConcatenationRule.CanCopy(new List<(TrackDescription, TrackDescription)> { (null, null) }, out _).Should().BeFalse();
    }

    [Fact]
    public void audio_only_inputs_can_be_copied()
        => ConcatenationRule.CanCopy(new List<(TrackDescription, TrackDescription)> { (null, Audio()), (null, Audio()) }, out _).Should().BeTrue();
}
