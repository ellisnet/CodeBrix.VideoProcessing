using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using CodeBrix.VideoProcessing.Core.Tools;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests.Tools;

// The runner is exercised with tiny POSIX shell scripts written to a temp folder for each test (never repository
// files), run as "/bin/sh <script>". Skipped on Windows, where /bin/sh does not exist.
public class ExternalToolRunnerTests
{
    private const string Shell = "/bin/sh";

    private static void RequirePosixShell()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("The runner tests use /bin/sh scripts.");
    }

    private static ExternalToolCommand ShellCommand(string script, Action<string> onOutput = null, Action<string> onError = null,
        TimeSpan gracePeriod = default, string workingDirectory = null) =>
        new ExternalToolCommand("sh", new ExternalToolArguments().AddPath(script))
        {
            OnStandardOutputLine = onOutput,
            OnStandardErrorLine = onError,
            KillGracePeriod = gracePeriod,
            WorkingDirectory = workingDirectory
        };

    [Fact]
    public void output_and_error_are_captured_when_there_are_no_callbacks()
    {
        //Arrange
        RequirePosixShell();
        using var folder = new ToolTestFolder();
        string script = folder.Write("hello.sh", "echo hello\necho world\necho oops 1>&2\n");

        //Act
        ExternalToolResult result = ExternalToolRunner.Run(Shell, ShellCommand(script), TestContext.Current.CancellationToken);

        //Assert
        result.ExitCode.Should().Be(0);
        result.Succeeded.Should().BeTrue();
        result.StandardOutput.Should().Be("hello\nworld");
        result.StandardError.Should().Be("oops");
        result.RenderedCommand.Should().Be("/bin/sh \"" + script + "\"");
    }

    [Fact]
    public void lines_stream_to_the_callbacks_in_order_and_are_not_captured()
    {
        //Arrange
        RequirePosixShell();
        using var folder = new ToolTestFolder();
        string script = folder.Write("chatty.sh", "i=1\nwhile [ $i -le 500 ]; do echo out$i; echo err$i 1>&2; i=$((i+1)); done\n");
        var output = new List<string>();
        var error = new List<string>();

        //Act
        ExternalToolResult result = ExternalToolRunner.Run(Shell, ShellCommand(script, output.Add, error.Add), TestContext.Current.CancellationToken);

        //Assert
        output.Should().HaveCount(500);
        error.Should().HaveCount(500);
        for (int i = 0; i < 500; i++)
        {
            output[i].Should().Be("out" + (i + 1).ToString(CultureInfo.InvariantCulture));
            error[i].Should().Be("err" + (i + 1).ToString(CultureInfo.InvariantCulture));
        }

        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().BeEmpty();
    }

    [Fact]
    public void a_large_standard_error_does_not_stall_the_run()
    {
        //Arrange - far more than a pipe buffer on standard error while nothing is read from standard output
        RequirePosixShell();
        using var folder = new ToolTestFolder();
        string script = folder.Write("flood.sh",
            "i=0\nwhile [ $i -lt 4000 ]; do echo 'frame=  100 fps= 25 q=28.0 size=     256kB time=00:00:04.00 bitrate= 524.3kbits/s' 1>&2; i=$((i+1)); done\necho done\n");

        //Act
        ExternalToolResult result = ExternalToolRunner.Run(Shell, ShellCommand(script), TestContext.Current.CancellationToken);

        //Assert
        result.StandardOutput.Should().Be("done");
        result.StandardError.Split('\n').Should().HaveCount(4000);
    }

    [Fact]
    public void a_non_zero_exit_is_a_result_not_an_exception()
    {
        //Arrange
        RequirePosixShell();
        using var folder = new ToolTestFolder();
        string script = folder.Write("fail.sh", "echo bad input 1>&2\nexit 3\n");

        //Act
        ExternalToolResult result = ExternalToolRunner.Run(Shell, ShellCommand(script), TestContext.Current.CancellationToken);

        //Assert
        result.ExitCode.Should().Be(3);
        result.Succeeded.Should().BeFalse();
        result.StandardError.Should().Be("bad input");
    }

    [Fact]
    public void the_working_directory_is_honoured()
    {
        //Arrange
        RequirePosixShell();
        using var folder = new ToolTestFolder();
        string work = folder.Folder("work");
        string script = folder.Write("where.sh", "pwd -P\n");
        string expected = new System.IO.DirectoryInfo(work).ResolveLinkTarget(true)?.FullName ?? work;

        //Act
        ExternalToolResult result = ExternalToolRunner.Run(Shell, ShellCommand(script, workingDirectory: work), TestContext.Current.CancellationToken);

        //Assert
        result.StandardOutput.Should().EndWith("/work");
        result.StandardOutput.Should().Be(expected);
    }

    [Fact]
    public void cancellation_kills_the_process_tree_and_throws()
    {
        //Arrange - the script starts a sleeping CHILD, reports its id, then waits for it
        RequirePosixShell();
        using var folder = new ToolTestFolder();
        string script = folder.Write("sleepy.sh", "sleep 60 &\necho $!\nwait\n");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int childId = 0;
        var stopwatch = new Stopwatch();
        ExternalToolCommand command = ShellCommand(script, line =>
        {
            childId = int.Parse(line, CultureInfo.InvariantCulture);
            stopwatch.Start();
            cancellation.Cancel();
        });

        //Act
        var exception = Assert.ThrowsAny<OperationCanceledException>(() => ExternalToolRunner.Run(Shell, command, cancellation.Token));
        stopwatch.Stop();

        //Assert
        exception.CancellationToken.Should().Be(cancellation.Token);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
        childId.Should().BeGreaterThan(0);
        WaitUntilGone(childId, TimeSpan.FromSeconds(2)).Should().BeTrue();
    }

    [Fact]
    public void a_grace_period_delays_the_kill()
    {
        //Arrange
        RequirePosixShell();
        using var folder = new ToolTestFolder();
        string script = folder.Write("sleepy.sh", "echo started\nexec sleep 60\n");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var stopwatch = new Stopwatch();
        ExternalToolCommand command = ShellCommand(script, _ =>
        {
            stopwatch.Start();
            cancellation.Cancel();
        }, gracePeriod: TimeSpan.FromMilliseconds(400));

        //Act
        Assert.ThrowsAny<OperationCanceledException>(() => ExternalToolRunner.Run(Shell, command, cancellation.Token));
        stopwatch.Stop();

        //Assert
        stopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(350));
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void an_already_cancelled_token_throws_before_anything_starts()
    {
        //Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        //Act + Assert
        Assert.ThrowsAny<OperationCanceledException>(() =>
            ExternalToolRunner.Run("/no/such/tool", new ExternalToolCommand("tool"), cancellation.Token));
    }

    [Fact]
    public void a_tool_that_cannot_be_started_names_the_tool_and_the_path()
    {
        //Arrange
        string missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cbvp-no-such-dir", "no-such-tool");

        //Act
        var exception = Assert.Throws<VideoProcessingException>(() =>
            ExternalToolRunner.Run(missing, new ExternalToolCommand("no-such-tool"), TestContext.Current.CancellationToken));

        //Assert
        exception.Message.Should().Contain("'no-such-tool'");
        exception.Message.Should().Contain(missing);
        exception.InnerException.Should().NotBeNull();
    }

    [Fact]
    public void a_throwing_callback_stops_the_run_and_its_exception_surfaces()
    {
        //Arrange
        RequirePosixShell();
        using var folder = new ToolTestFolder();
        string script = folder.Write("lines.sh", "echo first\nexec sleep 60\n");
        var stopwatch = Stopwatch.StartNew();

        //Act
        var exception = Assert.Throws<InvalidOperationException>(() => ExternalToolRunner.Run(Shell,
            ShellCommand(script, _ => throw new InvalidOperationException("consumer failed")), TestContext.Current.CancellationToken));

        //Assert
        exception.Message.Should().Be("consumer failed");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Run_validates_its_arguments()
    {
        //Act + Assert
        Assert.Throws<ArgumentException>(() => ExternalToolRunner.Run(null, new ExternalToolCommand("x"), TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ExternalToolRunner.Run(string.Empty, new ExternalToolCommand("x"), TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => ExternalToolRunner.Run(Shell, null, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/usr/bin/ffmpeg", "-version", "/usr/bin/ffmpeg -version")]
    [InlineData("/opt/my tools/ffmpeg", "-version", "\"/opt/my tools/ffmpeg\" -version")]
    [InlineData("ffmpeg", "", "ffmpeg")]
    public void RenderCommand_quotes_the_tool_path_only_when_it_has_whitespace(string toolPath, string token, string expected)
        => ExternalToolRunner.RenderCommand(toolPath, new ExternalToolArguments().Add(token)).Should().Be(expected);

    [Fact]
    public void RenderCommand_with_no_arguments_object_is_the_path()
        => ExternalToolRunner.RenderCommand("/bin/tool", null).Should().Be("/bin/tool");

    private static bool WaitUntilGone(int processId, TimeSpan limit)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < limit)
        {
            if (!IsRunning(processId)) return true;
            Thread.Sleep(50);
        }

        return !IsRunning(processId);
    }

    // Gone, or a zombie waiting to be reaped (it no longer runs).
    private static bool IsRunning(int processId)
    {
        string status = "/proc/" + processId.ToString(CultureInfo.InvariantCulture) + "/stat";
        if (OperatingSystem.IsLinux())
        {
            try
            {
                string stat = System.IO.File.ReadAllText(status);
                int close = stat.LastIndexOf(')');
                return close < 0 || close + 2 >= stat.Length || stat[close + 2] != 'Z';
            }
            catch (System.IO.IOException)
            {
                return false;
            }
        }

        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
