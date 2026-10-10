using System;
using CodeBrix.VideoProcessing.Core;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests.Engine;

public class CodeBrixVideoProcessingTests
{
    [Fact]
    public void Register_makes_the_ffmpeg_engine_current()
    {
        //Act
        CodeBrixVideoProcessing.Register();

        //Assert
        OperatingSystem.IsAndroid().Should().BeFalse();
        CodeBrixVideoProcessing.IsRegistered.Should().BeTrue();
        VideoProcessors.Current.Should().BeSameAs(CodeBrixVideoProcessing.Processor);
        VideoProcessors.Current.Name.Should().Be("ffmpeg");
    }

    [Fact]
    public void Register_is_idempotent()
    {
        //Act
        CodeBrixVideoProcessing.Register();
        CodeBrixVideoProcessing.Register();

        //Assert
        int count = 0;
        foreach (IVideoProcessor processor in VideoProcessors.RegisteredProcessors)
        {
            if (ReferenceEquals(processor, CodeBrixVideoProcessing.Processor)) count++;
        }

        count.Should().Be(1);
    }
}
