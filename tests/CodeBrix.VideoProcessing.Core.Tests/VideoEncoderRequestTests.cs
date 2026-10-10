using System;
using System.Collections.Generic;
using CodeBrix.VideoProcessing.Core.Encoding;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class VideoEncoderRequestTests
{
    private static VideoEncoderRequest Valid() => new VideoEncoderRequest
    {
        Width = 640,
        Height = 360,
        FrameRate = new FrameRate(30000, 1001)
    };

    [Fact]
    public void defaults_are_the_documented_ones()
    {
        //Arrange
        var request = new VideoEncoderRequest();

        //Assert
        request.PixelLayout.Should().Be(VideoPixelLayout.I420);
        request.BitDepth.Should().Be(8);
        request.FrameRate.IsSpecified.Should().BeFalse();
        request.ColorPrimaries.Should().Be(VideoColorPrimaries.Bt709);
        request.ColorTransfer.Should().Be(VideoColorTransfer.Bt709);
        request.ColorMatrix.Should().Be(VideoColorMatrix.Bt709);
        request.ColorRange.Should().Be(VideoColorRange.Limited);
        request.Quality.Should().Be(VideoQuality.EngineDefault);
        request.Lossless.Should().BeFalse();
        request.Threads.Should().Be(0);
        request.MaxKeyFrameInterval.Should().Be(0);
        request.Speed.Should().Be(0);
        request.CodecOptions.Should().BeEmpty();
    }

    [Fact]
    public void Validate_accepts_a_complete_request()
    {
        //Arrange
        VideoEncoderRequest request = Valid();
        request.PixelLayout = VideoPixelLayout.Gray;
        request.BitDepth = 12;
        request.ColorRange = VideoColorRange.Full;
        request.ColorPrimaries = VideoColorPrimaries.Unspecified;
        request.Quality = VideoQuality.ConstantQuality(20);
        request.Threads = 8;
        request.MaxKeyFrameInterval = 120;
        request.Speed = 6;
        request.CodecOptions["tune"] = "ssim";

        //Act + Assert
        request.Validate();
    }

    [Fact]
    public void Validate_accepts_odd_sizes()
    {
        //Arrange
        VideoEncoderRequest request = Valid();
        request.Width = 641;
        request.Height = 361;

        //Act + Assert
        request.Validate();
    }

    [Theory]
    [InlineData(0, 360, "Width")]
    [InlineData(-2, 360, "Width")]
    [InlineData(640, 0, "Height")]
    public void Validate_requires_a_positive_size(int width, int height, string paramName)
    {
        //Arrange
        VideoEncoderRequest request = Valid();
        request.Width = width;
        request.Height = height;

        //Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(request.Validate);

        //Assert
        exception.ParamName.Should().Be(paramName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(16)]
    public void Validate_rejects_unsupported_bit_depths(int bitDepth)
    {
        //Arrange
        VideoEncoderRequest request = Valid();
        request.BitDepth = bitDepth;

        //Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(request.Validate);

        //Assert
        exception.ParamName.Should().Be("BitDepth");
    }

    [Fact]
    public void Validate_rejects_deep_rgba()
    {
        //Arrange
        VideoEncoderRequest request = Valid();
        request.PixelLayout = VideoPixelLayout.Rgba;
        request.BitDepth = 10;

        //Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(request.Validate);
    }

    [Fact]
    public void Validate_requires_a_frame_rate()
    {
        //Arrange
        VideoEncoderRequest request = Valid();
        request.FrameRate = FrameRate.Unspecified;

        //Act
        var exception = Assert.Throws<ArgumentException>(request.Validate);

        //Assert
        exception.ParamName.Should().Be("FrameRate");
    }

    [Fact]
    public void Validate_rejects_undefined_enum_values()
    {
        //Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(new VideoEncoderRequest { Width = 2, Height = 2, FrameRate = new FrameRate(1, 1), PixelLayout = (VideoPixelLayout)9 }.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(new VideoEncoderRequest { Width = 2, Height = 2, FrameRate = new FrameRate(1, 1), ColorPrimaries = (VideoColorPrimaries)99 }.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(new VideoEncoderRequest { Width = 2, Height = 2, FrameRate = new FrameRate(1, 1), ColorTransfer = (VideoColorTransfer)99 }.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(new VideoEncoderRequest { Width = 2, Height = 2, FrameRate = new FrameRate(1, 1), ColorMatrix = (VideoColorMatrix)99 }.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(new VideoEncoderRequest { Width = 2, Height = 2, FrameRate = new FrameRate(1, 1), ColorRange = (VideoColorRange)99 }.Validate);
    }

    [Fact]
    public void Validate_rejects_a_quality_with_lossless()
    {
        //Arrange
        VideoEncoderRequest request = Valid();
        request.Lossless = true;
        request.Quality = VideoQuality.TargetBitrate(1000);

        //Act
        var exception = Assert.Throws<ArgumentException>(request.Validate);

        //Assert
        exception.ParamName.Should().Be("Quality");
    }

    [Fact]
    public void Validate_accepts_lossless_with_the_default_quality()
    {
        //Arrange
        VideoEncoderRequest request = Valid();
        request.Lossless = true;

        //Act + Assert
        request.Validate();
    }

    [Theory]
    [InlineData(-1, 0, 0, "Threads")]
    [InlineData(0, -1, 0, "MaxKeyFrameInterval")]
    [InlineData(0, 0, -1, "Speed")]
    public void Validate_rejects_negative_tuning_values(int threads, int interval, int speed, string paramName)
    {
        //Arrange
        VideoEncoderRequest request = Valid();
        request.Threads = threads;
        request.MaxKeyFrameInterval = interval;
        request.Speed = speed;

        //Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(request.Validate);

        //Assert
        exception.ParamName.Should().Be(paramName);
    }

    [Fact]
    public void Validate_rejects_bad_codec_options()
    {
        //Arrange
        VideoEncoderRequest nullOptions = Valid();
        nullOptions.CodecOptions = null;
        VideoEncoderRequest blankName = Valid();
        blankName.CodecOptions[" "] = "1";
        VideoEncoderRequest nullValue = Valid();
        nullValue.CodecOptions["tune"] = null;

        //Act + Assert
        Assert.Throws<ArgumentException>(nullOptions.Validate).ParamName.Should().Be("CodecOptions");
        Assert.Throws<ArgumentException>(blankName.Validate).ParamName.Should().Be("CodecOptions");
        Assert.Throws<ArgumentException>(nullValue.Validate).ParamName.Should().Be("CodecOptions");
    }

    [Fact]
    public void codec_option_names_are_case_sensitive()
    {
        //Arrange
        VideoEncoderRequest request = Valid();

        //Act
        request.CodecOptions["Tune"] = "a";
        request.CodecOptions["tune"] = "b";

        //Assert
        request.CodecOptions.Should().HaveCount(2);
    }

    [Fact]
    public void Clone_copies_every_value_and_the_codec_options_independently()
    {
        //Arrange
        VideoEncoderRequest request = Valid();
        request.PixelLayout = VideoPixelLayout.I444;
        request.BitDepth = 10;
        request.Lossless = true;
        request.Speed = 4;
        request.CodecOptions["tune"] = "ssim";

        //Act
        VideoEncoderRequest copy = request.Clone();
        request.CodecOptions["tune"] = "psnr";
        request.CodecOptions["extra"] = "1";

        //Assert
        copy.Should().NotBeSameAs(request);
        copy.Width.Should().Be(640);
        copy.FrameRate.Should().Be(new FrameRate(30000, 1001));
        copy.PixelLayout.Should().Be(VideoPixelLayout.I444);
        copy.BitDepth.Should().Be(10);
        copy.Lossless.Should().BeTrue();
        copy.Speed.Should().Be(4);
        copy.CodecOptions.Should().HaveCount(1);
        copy.CodecOptions["tune"].Should().Be("ssim");
    }

    [Fact]
    public void Create_keeps_its_own_copy_of_the_request()
    {
        //Arrange
        var factory = new FakeVideoEncoderFactory();
        VideoEncoderRequest request = Valid();

        //Act
        using IVideoEncoder encoder = factory.Create(request);
        request.Width = 2;

        //Assert
        encoder.Request.Should().NotBeSameAs(request);
        encoder.Request.Width.Should().Be(640);
    }

    [Fact]
    public void Create_validates_the_request_and_checks_the_capabilities()
    {
        //Arrange
        var factory = new FakeVideoEncoderFactory();
        VideoEncoderRequest invalid = Valid();
        invalid.Width = 0;
        VideoEncoderRequest unsupported = Valid();
        unsupported.BitDepth = 12;

        //Act + Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => factory.Create(invalid));
        Assert.Throws<NotSupportedException>(() => factory.Create(unsupported));
        Assert.Throws<ArgumentNullException>(() => factory.Create(null));
    }
}
