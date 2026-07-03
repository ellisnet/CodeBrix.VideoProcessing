using CodeBrix.VideoProcessing.Exceptions;
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
public class PixelFormatTests
{
    [Fact]
    public void PixelFormats_Enumerate()
    {
        var formats = FFMpeg.GetPixelFormats();
        (formats).Should().NotBeEmpty();
    }

    [Fact]
    public void PixelFormats_TryGetExisting()
    {
        (FFMpeg.TryGetPixelFormat("yuv420p", out _)).Should().BeTrue();
    }

    [Fact]
    public void PixelFormats_TryGetNotExisting()
    {
        (FFMpeg.TryGetPixelFormat("yuv420pppUnknown", out _)).Should().BeFalse();
    }

    [Fact]
    public void PixelFormats_GetExisting()
    {
        var fmt = FFMpeg.GetPixelFormat("yuv420p");
        (fmt.Components == 3 && fmt.BitsPerPixel == 12).Should().BeTrue();
    }

    [Fact]
    public void PixelFormats_GetNotExisting()
    {
        Assert.Throws<FFMpegException>(() => FFMpeg.GetPixelFormat("yuv420pppUnknown"));
    }
}
