using System;
using CodeBrix.VideoProcessing.Core.Output;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class VideoProcessorExtensionsTests
{
    [Fact]
    public void Supports_requires_every_flag()
    {
        //Arrange
        var engine = new FakeVideoProcessor(capabilities: VideoProcessorCapabilities.DirectTrim | VideoProcessorCapabilities.Snapshot);

        //Assert
        engine.Supports(VideoProcessorCapabilities.DirectTrim).Should().BeTrue();
        engine.Supports(VideoProcessorCapabilities.DirectTrim | VideoProcessorCapabilities.Snapshot).Should().BeTrue();
        engine.Supports(VideoProcessorCapabilities.DirectTrim | VideoProcessorCapabilities.HevcOutput).Should().BeFalse();
        engine.Supports(VideoProcessorCapabilities.None).Should().BeTrue();
    }

    [Fact]
    public void Supports_throws_on_null()
        => Assert.Throws<ArgumentNullException>(() => VideoProcessorExtensions.Supports(null, VideoProcessorCapabilities.None));

    [Fact]
    public void RequireCapability_names_the_engine_operation_and_missing_flag()
    {
        //Arrange
        var engine = new FakeVideoProcessor("demo", VideoProcessorCapabilities.DirectTrim);

        //Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            engine.RequireCapability(VideoProcessorCapabilities.DirectTrim | VideoProcessorCapabilities.HevcOutput, "the test"));

        //Assert
        exception.Message.Should().Contain("'demo'");
        exception.Message.Should().Contain("the test");
        exception.Message.Should().Contain("HevcOutput");
        exception.Message.Should().NotContain("DirectTrim");
    }

    [Fact]
    public void optional_operations_throw_not_supported_without_their_flag()
    {
        //Arrange
        var engine = new FakeVideoProcessor();

        //Assert
        Assert.Throws<NotSupportedException>(() => engine.TrimFile(new TrimRequest { InputPath = "a", OutputPath = "b", To = TimeSpan.FromSeconds(1) }, null, TestContext.Current.CancellationToken));
        Assert.Throws<NotSupportedException>(() => engine.ConcatenateFiles(new ConcatenateRequest { InputPaths = { "a" }, OutputPath = "b" }, null, TestContext.Current.CancellationToken));
        Assert.Throws<NotSupportedException>(() => engine.TranscodeFile(new TranscodeRequest { InputPath = "a" }, null, TestContext.Current.CancellationToken));
        Assert.Throws<NotSupportedException>(() => engine.Snapshot("a", TimeSpan.Zero, TestContext.Current.CancellationToken));
    }
}
