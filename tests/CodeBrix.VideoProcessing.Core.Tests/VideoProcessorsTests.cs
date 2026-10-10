using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

// Every test that touches the process-wide registry lives in this one class, so xUnit runs them one at a time
// and no other class can see a half-built registry.
public class VideoProcessorsTests : IDisposable
{
    public VideoProcessorsTests() => VideoProcessors.Clear();

    public void Dispose() => VideoProcessors.Clear();

    [Fact]
    public void Current_throws_when_nothing_is_registered()
    {
        //Act
        var exception = Assert.Throws<VideoProcessorNotRegisteredException>(() => VideoProcessors.Current);

        //Assert
        exception.Message.Should().Contain("CodeBrix.VideoProcessing.MitLicenseForever");
        exception.Message.Should().Contain("CodeBrixVideoProcessing.Register()");
        exception.Message.Should().Contain("CodeBrix.VideoProcessing.Android");
        exception.Message.Should().Contain("CodeBrixVideoProcessingAndroid.Register()");
    }

    [Fact]
    public void not_registered_exception_is_a_video_processing_exception()
        => new VideoProcessorNotRegisteredException().Should().BeAssignableTo<VideoProcessingException>();

    [Fact]
    public void IsRegistered_is_false_when_empty()
        => VideoProcessors.IsRegistered.Should().BeFalse();

    [Fact]
    public void Register_makes_the_engine_current()
    {
        //Arrange
        var engine = new FakeVideoProcessor();

        //Act
        VideoProcessors.Register(engine);

        //Assert
        VideoProcessors.IsRegistered.Should().BeTrue();
        VideoProcessors.Current.Should().BeSameAs(engine);
    }

    [Fact]
    public void Register_is_idempotent_per_instance()
    {
        //Arrange
        var engine = new FakeVideoProcessor();

        //Act
        VideoProcessors.Register(engine);
        VideoProcessors.Register(engine);

        //Assert
        VideoProcessors.RegisteredProcessors.Should().HaveCount(1);
    }

    [Fact]
    public void Register_throws_on_null()
        => Assert.Throws<ArgumentNullException>(() => VideoProcessors.Register(null));

    [Fact]
    public void Current_is_the_most_recently_registered_engine()
    {
        //Arrange
        var first = new FakeVideoProcessor("first");
        var second = new FakeVideoProcessor("second");

        //Act
        VideoProcessors.Register(first);
        VideoProcessors.Register(second);
        VideoProcessors.Register(first);

        //Assert
        VideoProcessors.Current.Should().BeSameAs(second);
        VideoProcessors.RegisteredProcessors.Should().HaveCount(2);
        VideoProcessors.RegisteredProcessors[0].Should().BeSameAs(first);
    }

    [Fact]
    public void TryGetCurrent_returns_false_and_null_when_empty()
    {
        //Act
        bool found = VideoProcessors.TryGetCurrent(out IVideoProcessor processor);

        //Assert
        found.Should().BeFalse();
        processor.Should().BeNull();
    }

    [Fact]
    public void TryGetCurrent_returns_the_engine()
    {
        //Arrange
        var engine = new FakeVideoProcessor();
        VideoProcessors.Register(engine);

        //Act
        bool found = VideoProcessors.TryGetCurrent(out IVideoProcessor processor);

        //Assert
        found.Should().BeTrue();
        processor.Should().BeSameAs(engine);
    }

    [Fact]
    public void Unregister_falls_back_to_the_previous_engine()
    {
        //Arrange
        var first = new FakeVideoProcessor("first");
        var second = new FakeVideoProcessor("second");
        VideoProcessors.Register(first);
        VideoProcessors.Register(second);

        //Act
        bool removed = VideoProcessors.Unregister(second);

        //Assert
        removed.Should().BeTrue();
        VideoProcessors.Current.Should().BeSameAs(first);
        VideoProcessors.Unregister(second).Should().BeFalse();
    }

    [Fact]
    public void Register_is_safe_from_many_threads()
    {
        //Arrange
        var engine = new FakeVideoProcessor();

        //Act
        System.Threading.Tasks.Parallel.For(0, 64, _ => VideoProcessors.Register(engine));

        //Assert
        VideoProcessors.RegisteredProcessors.Should().HaveCount(1);
    }
}
