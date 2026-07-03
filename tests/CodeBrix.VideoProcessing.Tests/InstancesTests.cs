using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Instances;
using CodeBrix.VideoProcessing.Instances.Exceptions;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests;

// Ported from the Instances 3.0.2 test suite (NUnit) to xUnit v3 + SilverAssertions.
// These exercise the vendored CodeBrix.VideoProcessing.Instances process wrapper by
// launching real `dotnet` processes and a small "waiting" helper program.
public class InstancesTests : IAsyncLifetime
{
    private const string WaitingProgramDll = "./waiting-program/CodeBrix.VideoProcessing.Tests.WaitingProgram.dll";

    public async ValueTask InitializeAsync()
    {
        if (!File.Exists(WaitingProgramDll))
        {
            await Instance.FinishAsync("dotnet",
                "publish ../../../../CodeBrix.VideoProcessing.Tests.WaitingProgram -c Release -o ./waiting-program");
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task PublishesExitedEventOnError()
    {
        var arguments = new ProcessArguments("dotnet", "run --project Nopes");
        var completionSource = new TaskCompletionSource<IProcessResult>();
        arguments.Exited += (_, args) => completionSource.TrySetResult(args);

        arguments.Start();
        var result = await completionSource.Task;
        result.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public void StaticFinishSuccessTest()
    {
        var outputReceived = false;
        var processResult = Instance.Finish("dotnet", "--list-runtimes", delegate { outputReceived = true; });
        outputReceived.Should().BeTrue();
        processResult.ExitCode.Should().Be(0);
    }

    [Fact]
    public void StaticFinishErrorTest()
    {
        var outputReceived = false;
        // A failing process is expected to emit something; which stream it lands on
        // varies across dotnet SDK versions, so accept output on either stdout or stderr.
        var processResult = Instance.Finish("dotnet", "run --project Nopes",
            delegate { outputReceived = true; },
            delegate { outputReceived = true; });
        outputReceived.Should().BeTrue();
        processResult.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task AsyncStaticFinishSuccessTest()
    {
        var outputReceived = false;
        var processResult = await Instance.FinishAsync("dotnet", "--list-runtimes", TestContext.Current.CancellationToken, delegate { outputReceived = true; });
        outputReceived.Should().BeTrue();
        processResult.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task AsyncStaticFinishErrorTest()
    {
        var outputReceived = false;
        // See StaticFinishErrorTest: accept failing output on either stream.
        var processResult = await Instance.FinishAsync("dotnet", "run --project Nopes", TestContext.Current.CancellationToken,
            delegate { outputReceived = true; },
            delegate { outputReceived = true; });
        outputReceived.Should().BeTrue();
        processResult.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task PublishesExitedEventOnSuccess()
    {
        var processArguments = new ProcessArguments("dotnet", "--list-runtimes");
        var completionSource = new TaskCompletionSource<IProcessResult>();
        processArguments.Exited += (_, args) => completionSource.TrySetResult(args);

        processArguments.Start();
        var result = await completionSource.Task;

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public void PublishesErrorEvents()
    {
        var processArguments = new ProcessArguments("dotnet", "run --project Nopes");
        var dataReceived = false;
        processArguments.ErrorDataReceived += (_, _) => dataReceived = true;

        using var instance = processArguments.Start();
        instance.WaitForExit();

        dataReceived.Should().BeTrue();
    }

    [Fact]
    public async Task PublishesDataEvents()
    {
        var processArguments = new ProcessArguments("dotnet", "--list-runtimes");
        var dataReceived = false;
        processArguments.OutputDataReceived += (_, _) => dataReceived = true;

        using var instance = processArguments.Start();
        await instance.WaitForExitAsync(TestContext.Current.CancellationToken);

        dataReceived.Should().BeTrue();
    }

    [Fact]
    public async Task IgnoreEmptyLinesWork()
    {
        var processArguments = new ProcessArguments("dotnet", "--help") { IgnoreEmptyLines = false };

        using var instance = processArguments.Start();
        await instance.WaitForExitAsync(TestContext.Current.CancellationToken);
        var linesIncludingNewline = instance.OutputData.Count;

        processArguments.IgnoreEmptyLines = true;
        using var instance2 = processArguments.Start();
        await instance2.WaitForExitAsync(TestContext.Current.CancellationToken);
        var linesExcludingNewline = instance2.OutputData.Count;

        linesExcludingNewline.Should().BeLessThan(linesIncludingNewline);
    }

    [Fact]
    public void SecondErrorTest()
    {
        var processArguments = new ProcessArguments("dotnet", "run --project Nopes") { IgnoreEmptyLines = true };

        using var instance = processArguments.Start();
        var result = instance.WaitForExit();
        // The exact stderr wording differs between SDK versions; assert that a failing
        // process captures error output and reports a non-zero exit code.
        instance.ErrorData.Should().NotBeEmpty();
        result.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public void ResultMatchesInstance()
    {
        var processArguments = new ProcessArguments("dotnet", "--help") { IgnoreEmptyLines = false };

        using var instance = processArguments.Start();
        var result = instance.WaitForExit();

        result.ExitCode.Should().Be(0);
        result.ErrorData.Should().Equal(instance.ErrorData);
        result.OutputData.Should().Equal(instance.OutputData);
    }

    [Fact]
    public async Task BasicErrorTest()
    {
        var processArguments = new ProcessArguments("dotnet", "run --project Nopes");

        using var instance = processArguments.Start();
        var result = await instance.WaitForExitAsync(TestContext.Current.CancellationToken);

        result.ExitCode.Should().NotBe(0);
        instance.ErrorData.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SecondOutputTest()
    {
        using var instance = Instance.Start("dotnet", "--help");
        var result = await instance.WaitForExitAsync(TestContext.Current.CancellationToken);

        result.ExitCode.Should().Be(0);
        result.OutputData.Any(line => line.Contains("run")).Should().BeTrue();
        instance.ErrorData.Should().BeEmpty();
    }

    [Fact]
    public void BasicOutputTest()
    {
        var processArguments = new ProcessArguments("dotnet", "--version");

        var result = processArguments.StartAndWaitForExit();

        result.OutputData.Should().NotBeEmpty();
        result.ErrorData.Should().BeEmpty();
    }

    [Fact]
    public async Task BufferCapacitiesCapsOutput()
    {
        var processArguments = new ProcessArguments("dotnet", "--help") { DataBufferCapacity = 3 };
        var result = await processArguments.StartAndWaitForExitAsync(TestContext.Current.CancellationToken);
        result.OutputData.Count.Should().Be(3);
        result.ErrorData.Should().BeEmpty();
    }

    [Fact]
    public void ThrowsOnFileNotFound()
    {
        Assert.Throws<InstanceFileNotFoundException>(() =>
        {
            Instance.Finish("akjsdhfaklsjdhfasldkjh", "--version");
        });
    }

    [Fact]
    public async Task VerifyCancellationStopsProcess()
    {
        var processArguments = GetWaitingProcessArguments();

        var started = DateTime.UtcNow;
        var instance = processArguments.Start();
        var cancel = new CancellationTokenSource();
        cancel.CancelAfter(100);
        await instance.WaitForExitAsync(cancel.Token);

        var elapsed = DateTime.UtcNow.Subtract(started).TotalSeconds;
        elapsed.Should().BeGreaterThan(0.09);
    }

    [Fact]
    public async Task VerifyCancellationAlreadyExitedProcess()
    {
        var processArguments = GetWaitingProcessArguments();

        var instance = processArguments.Start();
        await instance.SendInputAsync("ok");

        using var tokenSource = new CancellationTokenSource();
        var result = await instance.WaitForExitAsync(tokenSource.Token);

        Action act = () => tokenSource.Cancel();
        act.Should().NotThrow();
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public void VerifyKillStopsProcess()
    {
        var processArguments = GetWaitingProcessArguments();

        var started = DateTime.UtcNow;
        var instance = processArguments.Start();
        _ = Task.Delay(100, TestContext.Current.CancellationToken).ContinueWith(_ => instance.Kill());
        instance.WaitForExit();

        var elapsed = DateTime.UtcNow.Subtract(started).TotalSeconds;
        elapsed.Should().BeGreaterThan(0.09);
    }

    [Fact]
    public async Task DoubleKillReturnsSameResult()
    {
        var processArguments = GetWaitingProcessArguments();

        var instance = processArguments.Start();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        var result1 = instance.Kill();
        var result2 = instance.Kill();

        result1.ExitCode.Should().Be(result2.ExitCode);
        result1.OutputData.Should().Equal(result2.OutputData);
        result1.ErrorData.Should().Equal(result2.ErrorData);
    }

    [Fact]
    public async Task DoubleWaitForExitReturnsSameResult()
    {
        var processArguments = GetWaitingProcessArguments();

        var instance = processArguments.Start();
        _ = Task.Delay(100, TestContext.Current.CancellationToken).ContinueWith(_ => instance.SendInput("ok"), TestContext.Current.CancellationToken);
        var result1 = instance.WaitForExit();
        var result2 = instance.WaitForExit();

        result1.ExitCode.Should().Be(result2.ExitCode);
        result1.OutputData.Should().Equal(result2.OutputData);
        result1.ErrorData.Should().Equal(result2.ErrorData);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task DoubleWaitForExitAsyncReturnsSameResult()
    {
        var processArguments = GetWaitingProcessArguments();

        var instance = processArguments.Start();
        _ = Task.Delay(100, TestContext.Current.CancellationToken).ContinueWith(_ => instance.SendInput("ok"), TestContext.Current.CancellationToken);
        var result1 = await instance.WaitForExitAsync(TestContext.Current.CancellationToken);
        var result2 = await instance.WaitForExitAsync(TestContext.Current.CancellationToken);

        result1.ExitCode.Should().Be(result2.ExitCode);
        result1.OutputData.Should().Equal(result2.OutputData);
        result1.ErrorData.Should().Equal(result2.ErrorData);
    }

    [Fact]
    public async Task VerifySendInputBehaviour()
    {
        var processArguments = GetWaitingProcessArguments();

        var started = DateTime.UtcNow;
        var instance = processArguments.Start();

        _ = Task.Delay(100, TestContext.Current.CancellationToken).ContinueWith(_ => instance.SendInput("ok"), TestContext.Current.CancellationToken);
        await instance.WaitForExitAsync(TestContext.Current.CancellationToken);

        var elapsed = DateTime.UtcNow.Subtract(started).TotalSeconds;
        elapsed.Should().BeGreaterThan(0.09);
    }

    [Fact]
    public async Task VerifySendInputAsyncBehaviour()
    {
        var processArguments = GetWaitingProcessArguments();

        var started = DateTime.UtcNow;
        var instance = processArguments.Start();

        _ = Task.Delay(100, TestContext.Current.CancellationToken).ContinueWith(_ => instance.SendInputAsync("ok"));
        await instance.WaitForExitAsync(TestContext.Current.CancellationToken);

        var elapsed = DateTime.UtcNow.Subtract(started).TotalSeconds;
        elapsed.Should().BeGreaterThan(0.09);
    }

    private static ProcessArguments GetWaitingProcessArguments()
    {
        return new ProcessArguments("dotnet", WaitingProgramDll);
    }
}
