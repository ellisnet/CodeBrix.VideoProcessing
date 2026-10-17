using CodeBrix.VideoProcessing.Core.Frames;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class VideoSourceInfoTests
{
    [Fact]
    public void is_mirrored_defaults_to_false()
        => new VideoSourceInfo().IsMirrored.Should().BeFalse();

    [Fact]
    public void is_mirrored_is_kept_beside_the_rotation()
    {
        //Act
        var info = new VideoSourceInfo { RotationDegrees = 90, IsMirrored = true };

        //Assert
        info.IsMirrored.Should().BeTrue();
        info.RotationDegrees.Should().Be(90);
    }

    [Fact]
    public void a_copy_made_with_an_initializer_keeps_is_mirrored_independently()
    {
        //Arrange
        var mirrored = new VideoSourceInfo { Width = 64, Height = 48, IsMirrored = true };

        //Act
        var copy = new VideoSourceInfo { Width = mirrored.Width, Height = mirrored.Height, IsMirrored = mirrored.IsMirrored };
        var plain = new VideoSourceInfo { Width = mirrored.Width, Height = mirrored.Height };

        //Assert
        copy.IsMirrored.Should().BeTrue();
        plain.IsMirrored.Should().BeFalse();
        mirrored.IsMirrored.Should().BeTrue();
    }
}
