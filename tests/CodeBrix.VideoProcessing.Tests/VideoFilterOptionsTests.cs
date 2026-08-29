using System;
using System.Drawing;
using CodeBrix.VideoProcessing.Arguments;
using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Exceptions;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests;

public class VideoFilterOptionsTests
{
    private static string BuildVideoFilterArguments(Action<VideoFilterOptions> filters)
    {
        return FFMpegArguments
            .FromFileInput("input.mp4", false)
            .OutputToFile("output.mp4", false, opt => opt.WithVideoFilters(filters))
            .Arguments;
    }

    [Fact]
    public void custom_raw_filter_text_is_emitted_verbatim()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Custom("negate"));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"negate\" \"output.mp4\"");
    }

    [Fact]
    public void custom_raw_filter_text_is_not_escaped()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Custom(@"drawtext=text='a\, b'"));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"drawtext=text='a\\, b'\" \"output.mp4\"");
    }

    [Fact]
    public void custom_option_string_keeps_colons_and_equals()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Custom("drawbox", "x=10:y=10:w=100:h=100:color=red@0.5"));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"drawbox=x=10:y=10:w=100:h=100:color=red@0.5\" \"output.mp4\"");
    }

    [Fact]
    public void custom_option_string_escapes_only_the_chain_separators()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Custom("myfilter", "text=hello, world;next[x]"));

        //Assert
        (str).Should().Be(@"-i ""input.mp4"" -vf ""myfilter=text=hello\, world\;next\[x\]"" ""output.mp4""");
    }

    [Fact]
    public void custom_option_string_that_is_empty_emits_the_bare_filter_name()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Custom("hflip", string.Empty));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"hflip\" \"output.mp4\"");
    }

    [Fact]
    public void custom_key_value_options_fully_escape_every_value()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Custom("lut3d", ("file", "/media/lut, warm/warm.cube"), ("interp", "nearest")));

        //Assert
        (str).Should().Be(@"-i ""input.mp4"" -vf ""lut3d=file=/media/lut\\\, warm/warm.cube:interp=nearest"" ""output.mp4""");
    }

    [Fact]
    public void custom_key_value_options_are_not_escaped_a_second_time_by_the_chain_builder()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Custom("myfilter", ("text", "a,b")));

        //Assert - a value escaped by the argument must reach ffmpeg as \\\, and never as \\\\,
        (str).Should().Be(@"-i ""input.mp4"" -vf ""myfilter=text=a\\\,b"" ""output.mp4""");
    }

    [Theory]
    [InlineData(30d, "fps=30")]
    [InlineData(10d, "fps=10")]
    [InlineData(29.97d, "fps=29.97")]
    [InlineData(23.976d, "fps=23.976")]
    public void fps_formats_numbers_with_the_invariant_culture(double fps, string expectedFilter)
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Fps(fps));

        //Assert
        (str).Should().Be($"-i \"input.mp4\" -vf \"{expectedFilter}\" \"output.mp4\"");
    }

    [Theory]
    [InlineData("ntsc")]
    [InlineData("pal")]
    [InlineData("30000/1001")]
    public void fps_accepts_an_ffmpeg_expression(string fps)
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Fps(fps));

        //Assert
        (str).Should().Be($"-i \"input.mp4\" -vf \"fps={fps}\" \"output.mp4\"");
    }

    [Fact]
    public void lut3d_escapes_a_windows_path()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Lut3D(@"C:\luts\warm.cube"));

        //Assert
        (str).Should().Be(@"-i ""input.mp4"" -vf ""lut3d=file=C\\\:\\\\luts\\\\warm.cube:interp=tetrahedral"" ""output.mp4""");
    }

    [Fact]
    public void lut3d_escapes_a_path_containing_a_comma()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Lut3D("/media/lut, warm/warm.cube", Lut3DInterpolation.Trilinear));

        //Assert
        (str).Should().Be(@"-i ""input.mp4"" -vf ""lut3d=file=/media/lut\\\, warm/warm.cube:interp=trilinear"" ""output.mp4""");
    }

    [Theory]
    [InlineData(Lut3DInterpolation.Nearest, "nearest")]
    [InlineData(Lut3DInterpolation.Trilinear, "trilinear")]
    [InlineData(Lut3DInterpolation.Tetrahedral, "tetrahedral")]
    [InlineData(Lut3DInterpolation.Pyramid, "pyramid")]
    [InlineData(Lut3DInterpolation.Prism, "prism")]
    public void lut3d_emits_every_interpolation_name(Lut3DInterpolation interpolation, string expectedName)
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Lut3D("/luts/warm.cube", interpolation));

        //Assert
        (str).Should().Be($"-i \"input.mp4\" -vf \"lut3d=file=/luts/warm.cube:interp={expectedName}\" \"output.mp4\"");
    }

    [Fact]
    public void format_emits_the_named_pixel_format()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Format("yuv420p"));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"format=yuv420p\" \"output.mp4\"");
    }

    [Fact]
    public void zscale_emits_the_options_that_were_set()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .ZScale(zScale =>
            {
                zScale.Transfer = ZScaleTransfer.Linear;
                zScale.Npl = 100;
            }));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"zscale=t=linear:npl=100\" \"output.mp4\"");
    }

    [Fact]
    public void zscale_emits_every_option_in_a_stable_order()
    {
        //Arrange
        var options = new ZScaleOptions
        {
            Size = new Size(1280, 720),
            Transfer = ZScaleTransfer.Smpte2084,
            Primaries = ZScalePrimaries.Bt2020,
            Matrix = ZScaleMatrix.Bt2020Ncl,
            Range = ZScaleRange.Tv,
            Npl = 100,
            DitherMode = ZScaleDither.ErrorDiffusion
        };

        //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions.ZScale(options));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"zscale=s=1280x720:t=smpte2084:p=bt2020:m=bt2020nc:r=tv:npl=100:d=error_diffusion\" \"output.mp4\"");
    }

    [Fact]
    public void zscale_emits_width_and_height_when_no_size_is_given()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .ZScale(zScale =>
            {
                zScale.Width = 1280;
                zScale.Height = 720;
                zScale.Transfer = ZScaleTransfer.Bt709;
            }));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"zscale=w=1280:h=720:t=bt709\" \"output.mp4\"");
    }

    [Fact]
    public void zscale_throws_when_no_option_was_set()
    {
        //Arrange
        var processor = FFMpegArguments
            .FromFileInput("input.mp4", false)
            .OutputToFile("output.mp4", false, opt => opt.WithVideoFilters(filterOptions => filterOptions
                .ZScale(zScale => { })));

        //Act //Assert
        Assert.Throws<FFMpegArgumentException>(() => processor.Arguments);
    }

    [Fact]
    public void tonemap_emits_the_algorithm_on_its_own()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Tonemap(TonemapAlgorithm.Hable));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"tonemap=hable\" \"output.mp4\"");
    }

    [Fact]
    public void tonemap_emits_the_optional_numbers_that_were_given()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Tonemap(TonemapAlgorithm.Hable, desat: 0));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"tonemap=hable:desat=0\" \"output.mp4\"");
    }

    [Fact]
    public void tonemap_emits_param_desat_and_peak_in_order()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Tonemap(TonemapAlgorithm.Mobius, 0.3, 2, 100));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"tonemap=mobius:param=0.3:desat=2:peak=100\" \"output.mp4\"");
    }

    [Theory]
    [InlineData(TonemapAlgorithm.None, "none")]
    [InlineData(TonemapAlgorithm.Clip, "clip")]
    [InlineData(TonemapAlgorithm.Linear, "linear")]
    [InlineData(TonemapAlgorithm.Gamma, "gamma")]
    [InlineData(TonemapAlgorithm.Reinhard, "reinhard")]
    [InlineData(TonemapAlgorithm.Hable, "hable")]
    [InlineData(TonemapAlgorithm.Mobius, "mobius")]
    public void tonemap_emits_every_algorithm_name(TonemapAlgorithm algorithm, string expectedName)
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions.Tonemap(algorithm));

        //Assert
        (str).Should().Be($"-i \"input.mp4\" -vf \"tonemap={expectedName}\" \"output.mp4\"");
    }

    [Fact]
    public void rotate_formats_radians_with_the_invariant_culture()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Rotate(1.5707963));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"rotate=1.5707963\" \"output.mp4\"");
    }

    [Fact]
    public void rotate_accepts_an_ffmpeg_expression()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Rotate("PI/2"));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"rotate=PI/2\" \"output.mp4\"");
    }

    [Fact]
    public void the_existing_typed_filters_still_emit_the_same_text()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Transpose(Transposition.CounterClockwise90)
            .Scale(200, 300));

        //Assert - the pre-escaping marker must not change what the older arguments emit
        (str).Should().Be("-i \"input.mp4\" -vf \"transpose=2, scale=200:300\" \"output.mp4\"");
    }

    [Fact]
    public void several_new_filters_join_into_one_chain()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Lut3D("/luts/warm.cube")
            .Fps(10)
            .Format("yuv420p"));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"lut3d=file=/luts/warm.cube:interp=tetrahedral, fps=10, format=yuv420p\" \"output.mp4\"");
    }

    [Fact]
    public void a_typed_filter_and_a_custom_filter_share_one_chain()
    {
        //Arrange //Act
        var str = BuildVideoFilterArguments(filterOptions => filterOptions
            .Scale(200, 300)
            .Custom("negate")
            .Fps("ntsc"));

        //Assert
        (str).Should().Be("-i \"input.mp4\" -vf \"scale=200:300, negate, fps=ntsc\" \"output.mp4\"");
    }
}
