using System;
using CodeBrix.VideoProcessing.Core.Encoding;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class VideoEncoderCapabilitiesTests
{
    private static readonly VideoEncoderCapabilities Av1Like = new VideoEncoderCapabilities
    {
        PixelLayouts = VideoEncoderPixelLayouts.I420 | VideoEncoderPixelLayouts.I444 | VideoEncoderPixelLayouts.Gray,
        BitDepths = VideoEncoderBitDepths.Depth8 | VideoEncoderBitDepths.Depth10,
        SupportsLossless = false,
        SupportsForcedKeyFrames = true
    };

    private static VideoEncoderRequest Request(VideoPixelLayout layout, int bitDepth, bool lossless = false) => new VideoEncoderRequest
    {
        Width = 16,
        Height = 16,
        FrameRate = new FrameRate(25, 1),
        PixelLayout = layout,
        BitDepth = bitDepth,
        Lossless = lossless
    };

    [Theory]
    [InlineData(VideoPixelLayout.I420, VideoEncoderPixelLayouts.I420)]
    [InlineData(VideoPixelLayout.Nv12, VideoEncoderPixelLayouts.Nv12)]
    [InlineData(VideoPixelLayout.Rgba, VideoEncoderPixelLayouts.Rgba)]
    [InlineData(VideoPixelLayout.I444, VideoEncoderPixelLayouts.I444)]
    [InlineData(VideoPixelLayout.Gray, VideoEncoderPixelLayouts.Gray)]
    [InlineData((VideoPixelLayout)42, VideoEncoderPixelLayouts.None)]
    public void ToFlag_maps_every_layout(VideoPixelLayout layout, VideoEncoderPixelLayouts expected)
        => VideoEncoderCapabilities.ToFlag(layout).Should().Be(expected);

    [Theory]
    [InlineData(8, VideoEncoderBitDepths.Depth8)]
    [InlineData(10, VideoEncoderBitDepths.Depth10)]
    [InlineData(12, VideoEncoderBitDepths.Depth12)]
    [InlineData(16, VideoEncoderBitDepths.None)]
    public void ToFlag_maps_every_depth(int bitDepth, VideoEncoderBitDepths expected)
        => VideoEncoderCapabilities.ToFlag(bitDepth).Should().Be(expected);

    [Fact]
    public void Supports_checks_layouts_and_depths()
    {
        //Assert
        Av1Like.SupportsLayout(VideoPixelLayout.Gray).Should().BeTrue();
        Av1Like.SupportsLayout(VideoPixelLayout.Rgba).Should().BeFalse();
        Av1Like.SupportsLayout((VideoPixelLayout)42).Should().BeFalse();
        Av1Like.SupportsBitDepth(10).Should().BeTrue();
        Av1Like.SupportsBitDepth(12).Should().BeFalse();
        Av1Like.SupportsBitDepth(0).Should().BeFalse();
    }

    [Fact]
    public void ThrowIfUnsupported_accepts_a_supported_request()
        => Av1Like.ThrowIfUnsupported(Request(VideoPixelLayout.I444, 10));

    [Fact]
    public void ThrowIfUnsupported_names_what_is_missing()
    {
        //Act
        var layout = Assert.Throws<NotSupportedException>(() => Av1Like.ThrowIfUnsupported(Request(VideoPixelLayout.Nv12, 8)));
        var depth = Assert.Throws<NotSupportedException>(() => Av1Like.ThrowIfUnsupported(Request(VideoPixelLayout.I420, 12)));
        var lossless = Assert.Throws<NotSupportedException>(() => Av1Like.ThrowIfUnsupported(Request(VideoPixelLayout.I420, 8, lossless: true)));

        //Assert
        layout.Message.Should().Contain("Nv12");
        depth.Message.Should().Contain("12-bit");
        lossless.Message.Should().Contain("lossless");
    }

    [Fact]
    public void ThrowIfUnsupported_rejects_null()
        => Assert.Throws<ArgumentNullException>(() => Av1Like.ThrowIfUnsupported(null));

    [Fact]
    public void ToString_lists_the_capabilities()
    {
        //Act
        string text = Av1Like.ToString();

        //Assert
        text.Should().Contain("Gray");
        text.Should().Contain("Depth10");
    }

    [Fact]
    public void codec_ids_are_lower_case()
    {
        //Assert
        VideoCodecIds.Av1.Should().Be("av1");
        VideoCodecIds.H264.Should().Be("h264");
        VideoCodecIds.Hevc.Should().Be("hevc");
    }
}
