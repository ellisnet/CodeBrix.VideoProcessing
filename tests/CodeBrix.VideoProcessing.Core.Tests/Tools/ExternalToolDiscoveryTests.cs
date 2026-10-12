using System;
using CodeBrix.VideoProcessing.Core.Tools;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests.Tools;

public class ExternalToolDiscoveryTests
{
    [Fact]
    public void the_capability_is_bit_eleven()
        => ((int)VideoProcessorCapabilities.ExternalTools).Should().Be(1 << 11);

    [Fact]
    public void an_engine_with_the_capability_and_the_facet_hands_out_its_tools()
    {
        //Arrange
        var tools = new NullExternalTools();
        IVideoProcessor engine = new FakeToolHost(VideoProcessorCapabilities.ExternalTools, tools);

        //Act
        bool found = engine.TryGetExternalTools(out IExternalTools got);

        //Assert
        found.Should().BeTrue();
        got.Should().BeSameAs(tools);
        engine.RequireExternalTools("the test").Should().BeSameAs(tools);
    }

    [Fact]
    public void the_facet_without_the_capability_is_not_enough()
    {
        //Arrange
        IVideoProcessor engine = new FakeToolHost(VideoProcessorCapabilities.None, new NullExternalTools());

        //Act
        bool found = engine.TryGetExternalTools(out IExternalTools got);

        //Assert
        found.Should().BeFalse();
        got.Should().BeNull();
    }

    [Fact]
    public void the_capability_with_null_tools_is_not_enough()
        => new FakeToolHost(VideoProcessorCapabilities.ExternalTools, null).TryGetExternalTools(out _).Should().BeFalse();

    [Fact]
    public void the_capability_without_the_facet_is_not_enough()
        => new FakeVideoProcessor(capabilities: VideoProcessorCapabilities.ExternalTools).TryGetExternalTools(out _).Should().BeFalse();

    [Fact]
    public void RequireExternalTools_names_the_engine_the_operation_and_the_capability()
    {
        //Arrange
        IVideoProcessor engine = new FakeVideoProcessor("demo");

        //Act
        var exception = Assert.Throws<VideoProcessingException>(() => engine.RequireExternalTools("the ffmpeg flavours"));

        //Assert
        exception.Message.Should().Contain("'demo'");
        exception.Message.Should().Contain("the ffmpeg flavours");
        exception.Message.Should().Contain("ExternalTools");
    }

    [Fact]
    public void both_helpers_refuse_a_null_engine()
    {
        //Act + Assert
        Assert.Throws<ArgumentNullException>(() => VideoProcessorExtensions.TryGetExternalTools(null, out _));
        Assert.Throws<ArgumentNullException>(() => VideoProcessorExtensions.RequireExternalTools(null, "x"));
    }
}
