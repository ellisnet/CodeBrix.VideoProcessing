using System;
using CodeBrix.VideoProcessing.Core.Output;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class TrimRequestTests
{
    [Fact]
    public void Validate_accepts_a_forward_range()
    {
        //Arrange
        var request = new TrimRequest { InputPath = "i", OutputPath = "o", From = TimeSpan.FromSeconds(1), To = TimeSpan.FromSeconds(2) };

        //Act
        Action act = request.Validate;

        //Assert
        act.Should().NotThrow();
        request.SnapToKeyframes.Should().BeFalse();
    }

    [Fact]
    public void Validate_rejects_an_empty_range()
        => Assert.Throws<ArgumentOutOfRangeException>(new TrimRequest { InputPath = "i", OutputPath = "o", From = TimeSpan.FromSeconds(2), To = TimeSpan.FromSeconds(2) }.Validate);

    [Fact]
    public void Validate_rejects_a_negative_start()
        => Assert.Throws<ArgumentOutOfRangeException>(new TrimRequest { InputPath = "i", OutputPath = "o", From = TimeSpan.FromSeconds(-1), To = TimeSpan.FromSeconds(2) }.Validate);

    [Fact]
    public void Validate_requires_both_paths()
        => Assert.Throws<ArgumentException>(new TrimRequest { InputPath = "i", To = TimeSpan.FromSeconds(2) }.Validate);
}
