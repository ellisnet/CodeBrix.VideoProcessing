using System;
using CodeBrix.VideoProcessing.Core.Output;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class TranscodeRequestTests
{
    [Fact]
    public void default_output_keeps_everything_from_the_source()
    {
        //Arrange
        var request = new TranscodeRequest { InputPath = "in.mov" };
        request.Output.OutputPath = "out.mp4";

        //Act
        Action act = request.Validate;

        //Assert
        act.Should().NotThrow();
        request.Output.HasAudio.Should().BeTrue();
        request.Output.AudioSampleRate.Should().Be(0);
    }

    [Fact]
    public void Validate_requires_an_input_path()
        => Assert.Throws<ArgumentException>(new TranscodeRequest { Output = { OutputPath = "o.mp4" } }.Validate);

    [Fact]
    public void Validate_requires_an_output()
        => Assert.Throws<ArgumentNullException>(new TranscodeRequest { InputPath = "i", Output = null }.Validate);

    [Fact]
    public void Validate_rejects_keeping_only_one_dimension()
    {
        //Arrange
        var request = new TranscodeRequest { InputPath = "in.mov" };
        request.Output.OutputPath = "out.mp4";
        request.Output.Width = 640;

        //Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(request.Validate);
    }

    [Fact]
    public void Validate_still_checks_given_values()
    {
        //Arrange
        var request = new TranscodeRequest { InputPath = "in.mov" };
        request.Output.OutputPath = "out.mp4";
        request.Output.AudioChannels = 12;

        //Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(request.Validate);
    }
}
