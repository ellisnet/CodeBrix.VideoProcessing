using System;
using CodeBrix.VideoProcessing.Core.Tools;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests.Tools;

// The quoting contract is PINNED here: consumers record rendered lines and compare them byte for byte, and the
// rules reproduce the ffmpeg package's ported argument builder (paths in double quotes, everything else verbatim,
// one space between tokens).
public class ExternalToolArgumentsTests
{
    [Fact]
    public void no_arguments_render_as_the_empty_string()
    {
        //Act
        var arguments = new ExternalToolArguments();

        //Assert
        arguments.Render().Should().Be(string.Empty);
        arguments.Count.Should().Be(0);
    }

    [Fact]
    public void tokens_are_joined_by_one_space_in_order_with_nothing_at_either_end()
    {
        //Act
        string line = new ExternalToolArguments().Add("-y").Add("-c:v").Add("libsvtav1").Render();

        //Assert
        line.Should().Be("-y -c:v libsvtav1");
    }

    [Fact]
    public void Add_is_verbatim_for_multi_word_tokens_and_tokens_carrying_their_own_quotes()
    {
        //Arrange
        var arguments = new ExternalToolArguments()
            .Add("-map 0:v:0")
            .Add("-vf \"scale=w=1920:h=1080:flags=lanczos, format=yuv420p\"")
            .Add("-metadata title=\"A \\\"quoted\\\" clip\"");

        //Act
        string line = arguments.Render();

        //Assert
        line.Should().Be("-map 0:v:0 -vf \"scale=w=1920:h=1080:flags=lanczos, format=yuv420p\" -metadata title=\"A \\\"quoted\\\" clip\"");
        arguments.Count.Should().Be(3);
    }

    [Fact]
    public void a_null_or_empty_token_is_ignored_and_adds_no_separator()
    {
        //Act
        var arguments = new ExternalToolArguments().Add("-i").Add(string.Empty).Add(null).Add("-y");

        //Assert
        arguments.Render().Should().Be("-i -y");
        arguments.Count.Should().Be(2);
    }

    [Fact]
    public void a_whitespace_token_is_kept_verbatim()
        => new ExternalToolArguments().Add("a").Add(" ").Add("b").Render().Should().Be("a   b");

    [Fact]
    public void AddPath_wraps_the_path_in_double_quotes()
        => new ExternalToolArguments().Add("-i").AddPath("/clips/source.mkv").Render().Should().Be("-i \"/clips/source.mkv\"");

    [Fact]
    public void AddPath_keeps_spaces_inside_the_quotes()
        => new ExternalToolArguments().AddPath("/my clips/a b.mp4").Render().Should().Be("\"/my clips/a b.mp4\"");

    [Fact]
    public void AddPath_neither_escapes_nor_rewrites_the_path()
    {
        //Act
        string line = new ExternalToolArguments()
            .AddPath(@"C:\Videos\clip.mp4")
            .AddPath("relative/out.webm")
            .AddPath("it's.mp4")
            .Render();

        //Assert
        line.Should().Be("\"C:\\Videos\\clip.mp4\" \"relative/out.webm\" \"it's.mp4\"");
    }

    [Fact]
    public void AddPath_refuses_null_and_empty()
    {
        //Arrange
        var arguments = new ExternalToolArguments();

        //Act + Assert
        Assert.Throws<ArgumentNullException>(() => arguments.AddPath(null));
        Assert.Throws<ArgumentException>(() => arguments.AddPath(string.Empty));
        arguments.Count.Should().Be(0);
    }

    [Fact]
    public void AddPath_refuses_a_path_containing_a_double_quote()
    {
        //Arrange
        var arguments = new ExternalToolArguments().Add("-i");

        //Act
        var exception = Assert.Throws<ArgumentException>(() => arguments.AddPath("/clips/say \"hi\".mp4"));

        //Assert
        exception.ParamName.Should().Be("path");
        exception.Message.Should().Contain("/clips/say \"hi\".mp4");
        exception.Message.Should().Contain("double quote");
        arguments.Render().Should().Be("-i");
    }

    [Fact]
    public void AddPath_refuses_a_path_ending_in_a_backslash()
    {
        //Arrange
        var arguments = new ExternalToolArguments();

        //Act
        var exception = Assert.Throws<ArgumentException>(() => arguments.AddPath(@"C:\Videos\"));

        //Assert
        exception.ParamName.Should().Be("path");
        exception.Message.Should().Contain(@"C:\Videos\");
        exception.Message.Should().Contain("backslash");
        arguments.Count.Should().Be(0);
    }

    [Fact]
    public void a_whole_port_style_line_renders_byte_for_byte()
    {
        //Arrange - the shape of an authoring line the ffmpeg port renders today
        var arguments = new ExternalToolArguments()
            .Add("-autorotate")
            .Add("-i").AddPath("/clips/source.mkv")
            .Add("-map 0:v:0").Add("-map 0:a:0").Add("-map_chapters -1").Add("-map_metadata -1")
            .Add("-c:v libsvtav1").Add("-preset 6").Add("-crf 28").Add("-pix_fmt yuv420p").Add("-r 30").Add("-g 60")
            .Add("-vf \"scale=w=1920:h=1080:flags=lanczos, format=yuv420p\"")
            .Add("-c:a libopus").Add("-b:a 128k").Add("-ar 48000").Add("-ac 2").Add("-f webm").Add("-cues_to_front 1")
            .AddPath("/out/clip.cbv").Add("-y");

        //Act
        string line = arguments.Render();

        //Assert
        line.Should().Be(
            "-autorotate -i \"/clips/source.mkv\" -map 0:v:0 -map 0:a:0 -map_chapters -1 -map_metadata -1 "
            + "-c:v libsvtav1 -preset 6 -crf 28 -pix_fmt yuv420p -r 30 -g 60 "
            + "-vf \"scale=w=1920:h=1080:flags=lanczos, format=yuv420p\" "
            + "-c:a libopus -b:a 128k -ar 48000 -ac 2 -f webm -cues_to_front 1 \"/out/clip.cbv\" -y");
    }

    [Fact]
    public void AddRange_and_the_constructor_add_each_token_verbatim_and_skip_empties()
    {
        //Act
        var fromConstructor = new ExternalToolArguments(new[] { "-version", null, "-hide_banner" });
        var fromRange = new ExternalToolArguments().AddRange(new[] { "-a", "", "-b" }).AddRange(null);

        //Assert
        fromConstructor.Render().Should().Be("-version -hide_banner");
        fromRange.Render().Should().Be("-a -b");
        fromRange.Tokens.Should().Equal("-a", "-b");
    }

    [Fact]
    public void ToString_is_Render()
        => new ExternalToolArguments().Add("-x").AddPath("p").ToString().Should().Be("-x \"p\"");
}
