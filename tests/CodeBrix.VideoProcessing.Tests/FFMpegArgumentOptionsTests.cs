using System;
using CodeBrix.VideoProcessing.Arguments;
using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Exceptions;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests;

public class FFMpegArgumentOptionsTests
{
    private static string BuildOutputArguments(Action<FFMpegArgumentOptions> addArguments)
    {
        return FFMpegArguments
            .FromFileInput("input.mp4", false)
            .OutputToFile("output.mp4", false, addArguments)
            .Arguments;
    }

    [Fact]
    public void with_speed_preset_accepts_a_number()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt.WithSpeedPreset(8));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -preset 8 \"output.mp4\"");
    }

    [Fact]
    public void with_speed_preset_still_accepts_the_word_presets()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt.WithSpeedPreset(Speed.VeryFast));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -preset veryfast \"output.mp4\"");
    }

    [Fact]
    public void with_encoder_params_joins_the_pairs_with_colons()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt
            .WithEncoderParams("svtav1-params", ("tune", "0"), ("film-grain", "8")));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -svtav1-params tune=0:film-grain=8 \"output.mp4\"");
    }

    [Fact]
    public void with_encoder_params_normalises_a_leading_dash()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt
            .WithEncoderParams("-svtav1-params", ("tune", "0")));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -svtav1-params tune=0 \"output.mp4\"");
    }

    [Fact]
    public void with_encoder_params_quotes_a_value_containing_whitespace()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt
            .WithEncoderParams("x264-params", ("opts", "a b")));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -x264-params \"opts=a b\" \"output.mp4\"");
    }

    [Fact]
    public void auto_rotate_disabled_is_emitted_before_the_input()
    {
        //Arrange //Act
        var str = FFMpegArguments
            .FromFileInput("input.mp4", false, opt => opt.AutoRotate(false))
            .OutputToFile("output.mp4", false)
            .Arguments;

        //Assert
        (str).Should().Be("-noautorotate -i \"input.mp4\" \"output.mp4\"");
    }

    [Fact]
    public void auto_rotate_enabled_is_emitted_before_the_input()
    {
        //Arrange //Act
        var str = FFMpegArguments
            .FromFileInput("input.mp4", false, opt => opt.AutoRotate())
            .OutputToFile("output.mp4", false)
            .Arguments;

        //Assert
        (str).Should().Be("-autorotate -i \"input.mp4\" \"output.mp4\"");
    }

    [Fact]
    public void auto_rotate_is_emitted_before_the_input_it_belongs_to()
    {
        //Arrange //Act
        var str = FFMpegArguments
            .FromFileInput("first.mp4", false, opt => opt.AutoRotate(false))
            .AddFileInput("second.mp4", false)
            .OutputToFile("output.mp4", false)
            .Arguments;

        //Assert
        (str).Should().Be("-noautorotate -i \"first.mp4\" -i \"second.mp4\" \"output.mp4\"");
    }

    [Fact]
    public void with_subtitle_codec_emits_a_subtitle_codec_option()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt.WithSubtitleCodec("webvtt"));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -c:s webvtt \"output.mp4\"");
    }

    [Fact]
    public void with_metadata_quotes_the_value()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt.WithMetadata("title", "A whole name"));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -metadata title=\"A whole name\" \"output.mp4\"");
    }

    [Fact]
    public void with_stream_metadata_without_a_channel_uses_a_plain_index()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt.WithStreamMetadata(0, "language", "eng"));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -metadata:s:0 language=\"eng\" \"output.mp4\"");
    }

    [Fact]
    public void with_stream_metadata_with_a_channel_uses_a_typed_specifier()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt
            .WithStreamMetadata(Channel.Subtitle, 1, "title", "English (forced)"));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -metadata:s:s:1 title=\"English (forced)\" \"output.mp4\"");
    }

    [Fact]
    public void with_stream_metadata_escapes_an_embedded_double_quote()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt
            .WithStreamMetadata(Channel.Audio, 0, "title", "He said \"hello\""));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -metadata:s:a:0 title=\"He said \\\"hello\\\"\" \"output.mp4\"");
    }

    [Fact]
    public void with_disposition_joins_the_flags_with_a_plus()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt
            .WithDisposition(Channel.Subtitle, 0, Disposition.Default, Disposition.Forced));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -disposition:s:0 default+forced \"output.mp4\"");
    }

    [Fact]
    public void with_disposition_uses_the_ffmpeg_snake_case_flag_names()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt
            .WithDisposition(Channel.Subtitle, 2, Disposition.HearingImpaired, Disposition.VisualImpaired, Disposition.CleanEffects));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -disposition:s:2 hearing_impaired+visual_impaired+clean_effects \"output.mp4\"");
    }

    [Fact]
    public void with_disposition_none_clears_every_flag()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt
            .WithDisposition(Channel.Video, 0, Disposition.None));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -disposition:v:0 0 \"output.mp4\"");
    }

    [Fact]
    public void with_disposition_without_flags_clears_every_flag()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt.WithDisposition(Channel.Audio, 1));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -disposition:a:1 0 \"output.mp4\"");
    }

    [Fact]
    public void with_disposition_without_a_channel_uses_a_plain_index()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt.WithDisposition(3, Disposition.AttachedPic));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -disposition:3 attached_pic \"output.mp4\"");
    }

    [Theory]
    [InlineData(true, "-cues_to_front 1")]
    [InlineData(false, "-cues_to_front 0")]
    public void cues_to_front_emits_one_or_zero(bool enabled, string expected)
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt.CuesToFront(enabled));

        //Assert
        (str).Should().Be($"-i \"input.mp4\" {expected} \"output.mp4\"");
    }

    [Fact]
    public void cues_to_front_defaults_to_enabled()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt.CuesToFront());

        //Assert
        (str).Should().Be("-i \"input.mp4\" -cues_to_front 1 \"output.mp4\"");
    }

    [Fact]
    public void reserve_index_space_emits_the_byte_count()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt.ReserveIndexSpace(65536));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -reserve_index_space 65536 \"output.mp4\"");
    }

    [Fact]
    public void a_second_video_filter_chain_throws()
    {
        //Arrange //Act //Assert
        var exception = Assert.Throws<FFMpegArgumentException>(() => FFMpegArguments
            .FromFileInput("input.mp4", false)
            .OutputToFile("output.mp4", false, opt => opt
                .WithVideoFilters(filterOptions => filterOptions.Scale(200, 300))
                .WithVideoFilters(filterOptions => filterOptions.Fps(10))));

        (exception.Message.Contains("-vf")).Should().BeTrue();
    }

    [Fact]
    public void a_second_audio_filter_chain_throws()
    {
        //Arrange //Act //Assert
        var exception = Assert.Throws<FFMpegArgumentException>(() => FFMpegArguments
            .FromFileInput("input.mp3", false)
            .OutputToFile("output.mp3", false, opt => opt
                .WithAudioFilters(filterOptions => filterOptions.Pan("mono"))
                .WithAudioFilters(filterOptions => filterOptions.DynamicNormalizer())));

        (exception.Message.Contains("-af")).Should().BeTrue();
    }

    [Fact]
    public void one_video_filter_chain_with_several_filters_is_allowed()
    {
        //Arrange //Act
        var str = BuildOutputArguments(opt => opt
            .WithVideoFilters(filterOptions => filterOptions
                .Scale(200, 300)
                .Fps(10)
                .Format("yuv420p")));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"scale=200:300, fps=10, format=yuv420p\" \"output.mp4\"");
    }

    [Fact]
    public void two_outputs_may_each_have_their_own_video_filter_chain()
    {
        //Arrange //Act
        var str = FFMpegArguments
            .FromFileInput("input.mp4", false)
            .MultiOutput(outputs => outputs
                .OutputToFile("first.mp4", false, opt => opt
                    .WithVideoFilters(filterOptions => filterOptions.Fps(10)))
                .OutputToFile("second.mp4", false, opt => opt
                    .WithVideoFilters(filterOptions => filterOptions.Fps(25))))
            .Arguments;

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"fps=10\" \"first.mp4\" -vf \"fps=25\" \"second.mp4\"");
    }

    [Fact]
    public void the_webm_profile_authoring_command_line_is_expressible()
    {
        //Arrange //Act
        var str = FFMpegArguments
            .FromFileInput("input.mp4", false, opt => opt.AutoRotate(false))
            .AddFileInput("captions.en.vtt", false)
            .OutputToFile("output.webm", false, opt => opt
                .SelectStream(0, 0, Channel.Video)
                .SelectStream(0, 0, Channel.Audio)
                .SelectStream(0, 1, Channel.Subtitle)
                .WithVideoCodec("libsvtav1")
                .WithSpeedPreset(8)
                .WithEncoderParams("svtav1-params", ("tune", "0"), ("film-grain", "8"))
                .ForcePixelFormat("yuv420p")
                .WithAudioCodec("libopus")
                .WithSubtitleCodec("webvtt")
                .WithStreamMetadata(Channel.Subtitle, 0, "language", "eng")
                .WithDisposition(Channel.Subtitle, 0, Disposition.Default, Disposition.Forced)
                .CuesToFront()
                .ForceFormat("webm"))
            .Arguments;

        //Assert
        (str).Should().Be(
            "-noautorotate -i \"input.mp4\" -i \"captions.en.vtt\" -map 0:v:0 -map 0:a:0 -map 1:s:0 "
            + "-c:v libsvtav1 -preset 8 -svtav1-params tune=0:film-grain=8 -pix_fmt yuv420p -c:a libopus "
            + "-c:s webvtt -metadata:s:s:0 language=\"eng\" -disposition:s:0 default+forced "
            + "-cues_to_front 1 -f webm \"output.webm\"");
    }
}
