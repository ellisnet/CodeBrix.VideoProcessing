using System;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

// Every test that touches the process-wide VideoProcessors registry lives in this one class (its tests run one at a
// time), and each registers its OWN fake instance, so no assumption is made about what else is registered.
public class EngineRegistrationTests
{
    [Fact]
    public void the_instance_is_created_lazily_and_once()
    {
        //Arrange
        int created = 0;
        var registration = new EngineRegistration(() => { created++; return new StubProcessor(false); });

        //Act
        created.Should().Be(0);
        IVideoProcessor first = registration.Processor;
        IVideoProcessor second = registration.Processor;

        //Assert
        created.Should().Be(1);
        ReferenceEquals(first, second).Should().BeTrue();
        registration.IsRegistered.Should().BeFalse();
    }

    [Fact]
    public void register_is_idempotent_and_makes_the_instance_current()
    {
        //Arrange
        var registration = new EngineRegistration(() => new StubProcessor(false));
        int before = VideoProcessors.RegisteredProcessors.Count;

        //Act
        registration.Register();
        registration.Register();

        //Assert
        registration.IsRegistered.Should().BeTrue();
        ReferenceEquals(VideoProcessors.Current, registration.Processor).Should().BeTrue();
        VideoProcessors.RegisteredProcessors.Count.Should().Be(before + 1);
    }

    [Fact]
    public void a_null_factory_is_refused()
        => Assert.Throws<ArgumentNullException>(() => new EngineRegistration(null));
}
