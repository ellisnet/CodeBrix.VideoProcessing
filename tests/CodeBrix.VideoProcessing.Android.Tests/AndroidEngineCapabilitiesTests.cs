using System.Collections.Generic;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class AndroidEngineCapabilitiesTests
{
    private const int Flexible = MediaFormatKeys.ColorFormatYuv420Flexible;
    private const int P010 = MediaFormatKeys.ColorFormatYuvP010;

    // A fake codec list shaped like a phone's: hardware + software decoders, encoders, audio.
    private static List<CodecTypeInfo> PhoneCodecs(bool hevcDecoderListsP010, bool hevcEncoder = true) => new List<CodecTypeInfo>
    {
        new CodecTypeInfo("c2.qti.avc.decoder", false, "video/avc", new[] { Flexible, 21 }),
        new CodecTypeInfo("c2.qti.hevc.decoder", false, "video/hevc", hevcDecoderListsP010 ? new[] { Flexible, P010 } : new[] { Flexible }),
        new CodecTypeInfo("c2.android.aac.decoder", false, "audio/mp4a-latm", null),
        new CodecTypeInfo("c2.qti.avc.encoder", true, "video/avc", new[] { Flexible, P010 }),
        new CodecTypeInfo("c2.qti.hevc.encoder", true, hevcEncoder ? "video/hevc" : "video/x-unknown", new[] { Flexible, P010 })
    };

    [Fact]
    public void every_device_gets_the_managed_and_whole_file_capabilities()
    {
        //Act
        VideoProcessorCapabilities flags = AndroidEngineCapabilities.Compute(hasHevcEncoder: false, hasP010Decoder: false);

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
        AndroidEngineCapabilities.Compute(true, false).HasFlag(VideoProcessorCapabilities.HevcOutput).Should().BeTrue();
        AndroidEngineCapabilities.Compute(false, false).HasFlag(VideoProcessorCapabilities.HevcOutput).Should().BeFalse();
    }

    [Fact]
    public void high_bit_depth_follows_the_p010_decoder()
    {
        //Assert
        AndroidEngineCapabilities.Compute(false, true).HasFlag(VideoProcessorCapabilities.HighBitDepthFrameSource).Should().BeTrue();
        AndroidEngineCapabilities.Compute(true, false).HasFlag(VideoProcessorCapabilities.HighBitDepthFrameSource).Should().BeFalse();
    }

    [Theory]
    [InlineData(VideoProcessorCapabilities.AlphaFrameSource)]
    [InlineData(VideoProcessorCapabilities.I444FrameSource)]
    public void the_unsupported_frame_source_flags_are_never_set(VideoProcessorCapabilities flag)
        => AndroidEngineCapabilities.Compute(true, true).HasFlag(flag).Should().BeFalse();

    [Fact]
    public void a_codec_list_with_a_p010_video_decoder_sets_the_flag_and_names_it()
    {
        //Arrange
        List<CodecTypeInfo> codecs = PhoneCodecs(hevcDecoderListsP010: true);

        //Act
        VideoProcessorCapabilities flags = AndroidEngineCapabilities.Compute(codecs);
        IReadOnlyList<string> decoders = AndroidEngineCapabilities.FindP010Decoders(codecs);

        //Assert
        flags.HasFlag(VideoProcessorCapabilities.HighBitDepthFrameSource).Should().BeTrue();
        flags.HasFlag(VideoProcessorCapabilities.HevcOutput).Should().BeTrue();
        decoders.Should().Equal("c2.qti.hevc.decoder (video/hevc)");
    }

    [Fact]
    public void encoders_listing_p010_do_not_set_the_flag()
    {
        //Arrange
        List<CodecTypeInfo> codecs = PhoneCodecs(hevcDecoderListsP010: false, hevcEncoder: false);

        //Act
        VideoProcessorCapabilities flags = AndroidEngineCapabilities.Compute(codecs);

        //Assert
        flags.Should().Be(AndroidEngineCapabilities.Always);
        AndroidEngineCapabilities.FindP010Decoders(codecs).Should().BeEmpty();
    }

    [Fact]
    public void an_audio_type_listing_the_p010_number_is_not_a_video_decoder()
    {
        //Arrange
        var codecs = new[] { new CodecTypeInfo("odd.audio.decoder", false, "audio/raw", new[] { P010 }) };

        //Assert
        AndroidEngineCapabilities.FindP010Decoders(codecs).Should().BeEmpty();
        AndroidEngineCapabilities.Compute(codecs).HasFlag(VideoProcessorCapabilities.HighBitDepthFrameSource).Should().BeFalse();
    }

    [Fact]
    public void an_empty_or_missing_codec_list_gives_the_always_flags()
    {
        //Assert
        AndroidEngineCapabilities.Compute(new List<CodecTypeInfo>()).Should().Be(AndroidEngineCapabilities.Always);
        AndroidEngineCapabilities.Compute((IEnumerable<CodecTypeInfo>)null).Should().Be(AndroidEngineCapabilities.Always);
        AndroidEngineCapabilities.FindP010Decoders(null).Should().BeEmpty();
    }

    [Fact]
    public void every_p010_decoder_and_type_is_listed_in_codec_list_order()
    {
        //Arrange
        var codecs = new[]
        {
            new CodecTypeInfo("c2.android.av1.decoder", false, "video/av01", new[] { Flexible, P010 }),
            new CodecTypeInfo("c2.android.hevc.decoder", false, "video/hevc", new[] { P010 }),
            new CodecTypeInfo("c2.android.vp9.decoder", false, "video/x-vnd.on2.vp9", new[] { Flexible })
        };

        //Assert
        AndroidEngineCapabilities.FindP010Decoders(codecs).Should().Equal("c2.android.av1.decoder (video/av01)", "c2.android.hevc.decoder (video/hevc)");
    }

    [Fact]
    public void the_platform_choice_is_kept_when_it_lists_p010()
    {
        //Arrange
        var candidates = new[]
        {
            new CodecTypeInfo("c2.android.hevc.decoder", false, "video/hevc", new[] { P010 }),
            new CodecTypeInfo("c2.qti.hevc.decoder", false, "video/hevc", new[] { Flexible, P010 })
        };

        //Assert
        AndroidEngineCapabilities.ChooseP010Decoder(candidates, "video/hevc", "c2.qti.hevc.decoder").Should().Be("c2.qti.hevc.decoder");
    }

    [Fact]
    public void another_p010_decoder_of_the_type_replaces_a_platform_choice_without_it()
    {
        //Arrange
        var candidates = new[]
        {
            new CodecTypeInfo("c2.qti.hevc.decoder", false, "video/hevc", new[] { Flexible }),
            new CodecTypeInfo("c2.android.avc.decoder", false, "video/avc", new[] { P010 }),
            new CodecTypeInfo("c2.android.hevc.decoder", false, "video/hevc", new[] { Flexible, P010 })
        };

        //Assert
        AndroidEngineCapabilities.ChooseP010Decoder(candidates, "video/hevc", "c2.qti.hevc.decoder").Should().Be("c2.android.hevc.decoder");
    }

    [Fact]
    public void no_p010_decoder_of_the_type_gives_null()
    {
        //Arrange
        var candidates = new[]
        {
            new CodecTypeInfo("c2.qti.hevc.decoder", false, "video/hevc", new[] { Flexible }),
            new CodecTypeInfo("c2.qti.hevc.encoder", true, "video/hevc", new[] { P010 })
        };

        //Assert
        AndroidEngineCapabilities.ChooseP010Decoder(candidates, "video/hevc", "c2.qti.hevc.decoder").Should().BeNull();
        AndroidEngineCapabilities.ChooseP010Decoder(null, "video/hevc", null).Should().BeNull();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void external_tools_are_never_reported_on_android(bool hevcEncoder, bool p010Decoder)
        => AndroidEngineCapabilities.Compute(hevcEncoder, p010Decoder).HasFlag(VideoProcessorCapabilities.ExternalTools).Should().BeFalse();

    [Fact]
    public void external_tools_are_not_reported_for_a_whole_codec_list()
        => AndroidEngineCapabilities.Compute(PhoneCodecs(hevcDecoderListsP010: true)).HasFlag(VideoProcessorCapabilities.ExternalTools).Should().BeFalse();
}
