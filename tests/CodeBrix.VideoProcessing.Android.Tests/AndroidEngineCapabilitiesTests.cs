using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class AndroidEngineCapabilitiesTests
{
    [Fact]
    public void every_device_gets_the_managed_and_whole_file_capabilities()
    {
        //Act
        VideoProcessorCapabilities flags = AndroidEngineCapabilities.Compute(hasHevcEncoder: false);

        //Assert
        flags.Should().Be(
            VideoProcessorCapabilities.RgbaInput | VideoProcessorCapabilities.Nv12Input | VideoProcessorCapabilities.GrayFrameSource |
            VideoProcessorCapabilities.Snapshot | VideoProcessorCapabilities.DirectTranscode | VideoProcessorCapabilities.DirectTrim |
            VideoProcessorCapabilities.DirectConcatenate);
    }

    [Fact]
    public void hevc_output_follows_the_device_encoder()
    {
        //Assert
        AndroidEngineCapabilities.Compute(true).HasFlag(VideoProcessorCapabilities.HevcOutput).Should().BeTrue();
        AndroidEngineCapabilities.Compute(false).HasFlag(VideoProcessorCapabilities.HevcOutput).Should().BeFalse();
    }

    [Theory]
    [InlineData(VideoProcessorCapabilities.AlphaFrameSource)]
    [InlineData(VideoProcessorCapabilities.I444FrameSource)]
    [InlineData(VideoProcessorCapabilities.HighBitDepthFrameSource)]
    public void the_unsupported_frame_source_flags_are_never_set(VideoProcessorCapabilities flag)
        => AndroidEngineCapabilities.Compute(true).HasFlag(flag).Should().BeFalse();
}
