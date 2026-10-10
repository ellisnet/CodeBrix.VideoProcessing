using System;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class StandardVideoRequestTests
{
    private static StandardVideoRequest Valid() => new StandardVideoRequest
    {
        OutputPath = "out.mp4",
        Width = 640,
        Height = 360,
        FrameRate = new FrameRate(30, 1)
    };

    [Fact]
    public void defaults_are_the_documented_ones()
    {
        //Arrange
        var request = new StandardVideoRequest();

        //Assert
        request.Profile.Should().Be(StandardVideoProfile.Mp4H264Aac);
        request.InputPixelLayout.Should().Be(VideoPixelLayout.I420);
        request.Quality.Mode.Should().Be(VideoQualityMode.EngineDefault);
        request.FastStart.Should().BeTrue();
        request.HasAudio.Should().BeFalse();
        request.AudioSampleRate.Should().Be(48000);
        request.AudioChannels.Should().Be(2);
        request.ColorRange.Should().Be(VideoColorRange.Limited);
        request.ColorMatrix.Should().Be(VideoColorMatrix.Bt709);
    }

    [Fact]
    public void Validate_accepts_a_complete_request()
    {
        //Arrange
        var request = Valid();
        request.HasAudio = true;
        request.Quality = VideoQuality.ConstantQuality(20);
        request.RotationDegrees = 270;

        //Act
        Action act = request.Validate;

        //Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_requires_an_output_path()
    {
        //Arrange
        var request = Valid();
        request.OutputPath = " ";

        //Act
        var exception = Assert.Throws<ArgumentException>(request.Validate);

        //Assert
        exception.ParamName.Should().Be("OutputPath");
    }

    [Theory]
    [InlineData(641, 360, "Width")]
    [InlineData(640, 0, "Height")]
    [InlineData(0, 0, "Width")]
    public void Validate_requires_a_positive_even_size(int width, int height, string paramName)
    {
        //Arrange
        var request = Valid();
        request.Width = width;
        request.Height = height;

        //Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(request.Validate);

        //Assert
        exception.ParamName.Should().Be(paramName);
    }

    [Fact]
    public void Validate_requires_a_frame_rate()
    {
        //Arrange
        var request = Valid();
        request.FrameRate = FrameRate.Unspecified;

        //Act
        var exception = Assert.Throws<ArgumentException>(request.Validate);

        //Assert
        exception.ParamName.Should().Be("FrameRate");
    }

    [Theory]
    [InlineData(45)]
    [InlineData(-90)]
    [InlineData(360)]
    public void Validate_rejects_rotations_other_than_quarter_turns(int degrees)
    {
        //Arrange
        var request = Valid();
        request.RotationDegrees = degrees;

        //Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(request.Validate);
    }

    [Fact]
    public void Validate_rejects_a_negative_keyframe_interval()
    {
        //Arrange
        var request = Valid();
        request.KeyframeIntervalFrames = -1;

        //Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(request.Validate);
    }

    [Theory]
    [InlineData(4000, 2, "AudioSampleRate")]
    [InlineData(48000, 0, "AudioChannels")]
    [InlineData(48000, 9, "AudioChannels")]
    public void Validate_checks_audio_only_when_there_is_audio(int sampleRate, int channels, string paramName)
    {
        //Arrange
        var request = Valid();
        request.AudioSampleRate = sampleRate;
        request.AudioChannels = channels;

        //Act
        request.Validate();
        request.HasAudio = true;
        var exception = Assert.Throws<ArgumentOutOfRangeException>(request.Validate);

        //Assert
        exception.ParamName.Should().Be(paramName);
    }

    [Fact]
    public void Validate_rejects_undefined_enum_values()
    {
        //Arrange
        var request = Valid();
        request.Profile = (StandardVideoProfile)42;

        //Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(request.Validate);
    }

    [Fact]
    public void Clone_copies_every_value()
    {
        //Arrange
        var request = Valid();
        request.Title = "t";

        //Act
        StandardVideoRequest clone = request.Clone();
        request.Width = 2;

        //Assert
        clone.Width.Should().Be(640);
        clone.Title.Should().Be("t");
    }

    [Theory]
    [InlineData(VideoPixelLayout.I444)]
    [InlineData(VideoPixelLayout.Gray)]
    public void Validate_rejects_layouts_the_writer_does_not_take(VideoPixelLayout layout)
    {
        //Arrange
        StandardVideoRequest request = Valid();
        request.InputPixelLayout = layout;

        //Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(request.Validate);

        //Assert
        exception.ParamName.Should().Be("InputPixelLayout");
    }
}
