using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SilverAssertions;
using CodeBrix.VideoProcessing.Tests.Utilities;

namespace CodeBrix.VideoProcessing.Tests;
public class FFMpegOptionsTest
{
    [Fact]
    public void Options_Initialized()
    {
        (GlobalFFOptions.Current).Should().NotBeNull();
    }

    [Fact]
    public void Options_Defaults_Configured()
    {
        (new FFOptions().BinaryFolder).Should().Be("");
    }

    [Fact]
    public void Options_Loaded_From_File()
    {
        var options = System.Text.Json.JsonSerializer.Deserialize<FFOptions>(
            File.ReadAllText("ffmpeg.config.json"),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        (options.BinaryFolder).Should().Be(GlobalFFOptions.Current.BinaryFolder);
    }

    [Fact]
    public void ZZZ_Options_Set_Programmatically()
    {
        try
        {
            GlobalFFOptions.Configure(new FFOptions { BinaryFolder = "Whatever" });
            (GlobalFFOptions.Current.BinaryFolder).Should().Be("Whatever");
        }
        finally
        {
            GlobalFFOptions.Configure(new FFOptions());
        }
    }
}
