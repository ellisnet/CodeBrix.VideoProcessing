using System;
using CodeBrix.VideoProcessing.Core.Tools;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests.Tools;

public class ExternalToolTypesTests
{
    [Fact]
    public void a_command_has_empty_arguments_and_kills_outright_by_default()
    {
        //Act
        var command = new ExternalToolCommand("ffmpeg");

        //Assert
        command.ToolName.Should().Be("ffmpeg");
        command.Arguments.Should().NotBeNull();
        command.Arguments.Count.Should().Be(0);
        command.KillGracePeriod.Should().Be(TimeSpan.Zero);
        command.WorkingDirectory.Should().BeNull();
        command.OnStandardOutputLine.Should().BeNull();
        command.OnStandardErrorLine.Should().BeNull();
    }

    [Fact]
    public void a_command_refuses_an_empty_tool_name_and_a_negative_grace_period()
    {
        //Act + Assert
        Assert.Throws<ArgumentException>(() => new ExternalToolCommand(null));
        Assert.Throws<ArgumentException>(() => new ExternalToolCommand(string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExternalToolCommand("x") { KillGracePeriod = TimeSpan.FromSeconds(-1) });
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(-1, false)]
    public void a_result_succeeds_only_on_exit_code_zero(int exitCode, bool succeeded)
        => new ExternalToolResult(exitCode, "tool", null, null).Succeeded.Should().Be(succeeded);

    [Fact]
    public void a_result_never_has_null_text()
    {
        //Act
        var result = new ExternalToolResult(0, null, null, null);

        //Assert
        result.RenderedCommand.Should().BeEmpty();
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().BeEmpty();
    }

    [Fact]
    public void a_tool_is_found_only_with_a_path()
    {
        //Assert
        new ExternalTool("ffmpeg", "/usr/bin/ffmpeg", "on the PATH").Found.Should().BeTrue();
        new ExternalTool("ffmpeg", "", "on the PATH").Found.Should().BeFalse();
        new ExternalTool("ffmpeg", null, null).LookedIn.Should().BeEmpty();
        new ExternalTool("ffmpeg", "/usr/bin/ffmpeg", "on the PATH").ToString().Should().Be("/usr/bin/ffmpeg");
        Assert.Throws<ArgumentException>(() => new ExternalTool(null, null, null));
    }
}
