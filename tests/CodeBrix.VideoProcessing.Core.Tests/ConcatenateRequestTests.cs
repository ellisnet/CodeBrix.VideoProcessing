using System;
using System.Collections.Generic;
using CodeBrix.VideoProcessing.Core.Output;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class ConcatenateRequestTests
{
    [Fact]
    public void Validate_accepts_inputs_and_an_output()
    {
        //Arrange
        var request = new ConcatenateRequest { InputPaths = { "a.mp4", "b.mp4" }, OutputPath = "ab.mp4" };

        //Act
        Action act = request.Validate;

        //Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_requires_an_input()
        => Assert.Throws<ArgumentException>(new ConcatenateRequest { OutputPath = "o" }.Validate);

    [Fact]
    public void Validate_rejects_a_null_list()
        => Assert.Throws<ArgumentException>(new ConcatenateRequest { InputPaths = null, OutputPath = "o" }.Validate);

    [Fact]
    public void Validate_rejects_an_empty_entry()
        => Assert.Throws<ArgumentException>(new ConcatenateRequest { InputPaths = new List<string> { "a", "" }, OutputPath = "o" }.Validate);
}
