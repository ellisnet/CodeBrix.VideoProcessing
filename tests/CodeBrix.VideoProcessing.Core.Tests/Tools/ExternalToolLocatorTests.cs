using System;
using System.IO;
using CodeBrix.VideoProcessing.Core.Tools;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests.Tools;

// The lookup is tested through the internal overload that takes the PATH value and the platform as parameters, so no
// test changes the process's real PATH.
public class ExternalToolLocatorTests
{
    [Fact]
    public void the_folder_is_searched_before_the_PATH()
    {
        //Arrange
        using var folder = new ToolTestFolder();
        string binaries = folder.Folder("bin");
        string onPath = folder.Folder("path");
        string inFolder = folder.Write("bin/mytool", "#!/bin/sh\n", executable: true);
        folder.Write("path/mytool", "#!/bin/sh\n", executable: true);

        //Act
        ExternalTool tool = ExternalToolLocator.Locate("mytool", new[] { binaries }, onPath, windows: false);

        //Assert
        tool.Found.Should().BeTrue();
        tool.Path.Should().Be(inFolder);
        tool.Name.Should().Be("mytool");
        tool.LookedIn.Should().Be($"in '{binaries}' and then on the PATH");
    }

    [Fact]
    public void the_PATH_is_searched_in_order_when_the_folder_lacks_the_tool()
    {
        //Arrange
        using var folder = new ToolTestFolder();
        string binaries = folder.Folder("bin");
        string first = folder.Folder("first");
        string second = folder.Folder("second");
        string expected = folder.Write("second/mytool", "#!/bin/sh\n", executable: true);
        string pathVariable = string.Join(Path.PathSeparator, "", first, second);

        //Act
        ExternalTool tool = ExternalToolLocator.Locate("mytool", new[] { binaries }, pathVariable, windows: false);

        //Assert
        tool.Path.Should().Be(expected);
    }

    [Fact]
    public void a_PATH_entry_without_execute_permission_is_skipped_on_unix()
    {
        //Arrange
        if (OperatingSystem.IsWindows()) Assert.Skip("Execute permission bits are a Unix notion.");
        using var folder = new ToolTestFolder();
        string first = folder.Folder("first");
        string second = folder.Folder("second");
        folder.Write("first/mytool", "not runnable", executable: false);
        string expected = folder.Write("second/mytool", "#!/bin/sh\n", executable: true);

        //Act
        ExternalTool tool = ExternalToolLocator.Locate("mytool", null, first + Path.PathSeparator + second, windows: false);

        //Assert
        tool.Path.Should().Be(expected);
    }

    [Fact]
    public void a_tool_that_is_nowhere_is_not_found_and_says_where_it_was_looked_for()
    {
        //Arrange
        using var folder = new ToolTestFolder();
        string binaries = folder.Folder("bin");

        //Act
        ExternalTool tool = ExternalToolLocator.Locate("no-such-tool", new[] { binaries }, folder.Folder("path"), windows: false);

        //Assert
        tool.Found.Should().BeFalse();
        tool.Path.Should().BeNull();
        tool.LookedIn.Should().Be($"in '{binaries}' and then on the PATH");
        tool.ToString().Should().Be($"no-such-tool (not found, looked for in '{binaries}' and then on the PATH)");
    }

    [Fact]
    public void an_empty_PATH_and_no_folder_finds_nothing()
    {
        //Act
        ExternalTool tool = ExternalToolLocator.Locate("sh", Array.Empty<string>(), string.Empty, windows: false);

        //Assert
        tool.Found.Should().BeFalse();
        tool.LookedIn.Should().Be("on the PATH");
    }

    [Fact]
    public void on_windows_the_folder_also_offers_the_exe_and_the_PATH_uses_PATHEXT()
    {
        //Arrange
        using var folder = new ToolTestFolder();
        string binaries = folder.Folder("bin");
        string onPath = folder.Folder("path");
        string exe = folder.Write("bin/ffmpeg.exe", "MZ");
        string cmd = folder.Write("path/helper.CMD", "@echo off");

        //Act
        ExternalTool inFolder = ExternalToolLocator.Locate("ffmpeg", new[] { binaries }, onPath, windows: true);
        ExternalTool onPathTool = ExternalToolLocator.Locate("helper", new[] { binaries }, onPath, windows: true);

        //Assert
        inFolder.Path.Should().Be(exe);
        onPathTool.Path.Should().Be(cmd);
    }

    [Fact]
    public void a_name_with_a_directory_separator_is_checked_as_a_path()
    {
        //Arrange
        using var folder = new ToolTestFolder();
        string script = folder.Write("tools/run-me", "#!/bin/sh\n", executable: true);

        //Act
        ExternalTool found = ExternalToolLocator.Locate(script, new[] { folder.Folder("elsewhere") }, string.Empty, windows: false);
        ExternalTool missing = ExternalToolLocator.Locate(script + "-not", null, string.Empty, windows: false);

        //Assert
        found.Path.Should().Be(script);
        found.LookedIn.Should().Be($"at '{script}'");
        missing.Found.Should().BeFalse();
    }

    [Fact]
    public void the_public_lookup_reads_the_real_PATH()
    {
        //Arrange
        if (OperatingSystem.IsWindows()) Assert.Skip("Looks for 'sh', which a Unix PATH has.");

        //Act
        ExternalTool tool = ExternalToolLocator.Locate("sh", (string)null);

        //Assert
        tool.Found.Should().BeTrue();
        tool.LookedIn.Should().Be("on the PATH");
        Path.IsPathRooted(tool.Path).Should().BeTrue();
    }

    [Fact]
    public void the_lookup_description_names_every_folder_in_order()
    {
        //Assert
        ExternalToolLocator.DescribeLookup(null).Should().Be("on the PATH");
        ExternalToolLocator.DescribeLookup(new[] { "", null }).Should().Be("on the PATH");
        ExternalToolLocator.DescribeLookup(new[] { "/a" }).Should().Be("in '/a' and then on the PATH");
        ExternalToolLocator.DescribeLookup(new[] { "/a/x64", "/a" }).Should().Be("in '/a/x64', then in '/a', and then on the PATH");
    }

    [Fact]
    public void an_empty_tool_name_is_refused()
    {
        //Act + Assert
        Assert.Throws<ArgumentException>(() => ExternalToolLocator.Locate(null, "/bin"));
        Assert.Throws<ArgumentException>(() => ExternalToolLocator.Locate(string.Empty, "/bin"));
    }
}
