using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Engine;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests.Engine;

public class FFmpegNamesTests
{
    [Theory]
    [InlineData("yuva420p", true)]
    [InlineData("yuva444p10le", true)]
    [InlineData("rgba", true)]
    [InlineData("bgra", true)]
    [InlineData("argb", true)]
    [InlineData("gbrap", true)]
    [InlineData("ya8", true)]
    [InlineData("pal8", true)]
    [InlineData("yuv420p", false)]
    [InlineData("yuv420p10le", false)]
    [InlineData("rgb24", false)]
    [InlineData("gray", false)]
    [InlineData(null, false)]
    public void HasAlpha_reads_the_pixel_format(string pixelFormat, bool expected)
        => FFmpegNames.HasAlpha(pixelFormat).Should().Be(expected);

    [Theory]
    [InlineData("yuv420p", 8)]
    [InlineData("yuv420p10le", 10)]
    [InlineData("yuv422p12be", 12)]
    [InlineData("p010le", 10)]
    [InlineData("rgb48le", 16)]
    [InlineData("gray", 8)]
    public void BitDepthOf_reads_the_pixel_format(string pixelFormat, int expected)
        => FFmpegNames.BitDepthOf(pixelFormat).Should().Be(expected);

    [Theory]
    [InlineData("yuv420p", VideoChromaSubsampling.Yuv420)]
    [InlineData("nv12", VideoChromaSubsampling.Yuv420)]
    [InlineData("yuv422p10le", VideoChromaSubsampling.Yuv422)]
    [InlineData("yuv444p", VideoChromaSubsampling.Yuv444)]
    [InlineData("gray10le", VideoChromaSubsampling.Monochrome)]
    [InlineData("rgba", VideoChromaSubsampling.Rgb)]
    [InlineData("gbrp", VideoChromaSubsampling.Rgb)]
    [InlineData("pal8", VideoChromaSubsampling.Other)]
    [InlineData(null, VideoChromaSubsampling.Unknown)]
    public void ToChromaSubsampling_reads_the_pixel_format(string pixelFormat, VideoChromaSubsampling expected)
        => FFmpegNames.ToChromaSubsampling(pixelFormat).Should().Be(expected);

    [Theory]
    [InlineData("tv", "yuv420p", VideoColorRange.Limited)]
    [InlineData("pc", "yuv420p", VideoColorRange.Full)]
    [InlineData("unknown", "yuvj420p", VideoColorRange.Full)]
    [InlineData(null, "yuv420p", VideoColorRange.Unspecified)]
    public void ToColorRange_uses_the_tag_then_the_jpeg_formats(string range, string pixelFormat, VideoColorRange expected)
        => FFmpegNames.ToColorRange(range, pixelFormat).Should().Be(expected);

    [Theory]
    [InlineData("bt709", VideoColorMatrix.Bt709)]
    [InlineData("smpte170m", VideoColorMatrix.Bt601)]
    [InlineData("bt470bg", VideoColorMatrix.Bt601)]
    [InlineData("bt2020nc", VideoColorMatrix.Bt2020NonConstant)]
    [InlineData("gbr", VideoColorMatrix.Rgb)]
    [InlineData("unknown", VideoColorMatrix.Unspecified)]
    [InlineData("ycgco", VideoColorMatrix.Other)]
    public void ToColorMatrix_maps_ffmpeg_names(string name, VideoColorMatrix expected)
        => FFmpegNames.ToColorMatrix(name).Should().Be(expected);

    [Theory]
    [InlineData("smpte2084", VideoColorTransfer.Pq)]
    [InlineData("arib-std-b67", VideoColorTransfer.Hlg)]
    [InlineData("iec61966-2-1", VideoColorTransfer.Srgb)]
    [InlineData("bt709", VideoColorTransfer.Bt709)]
    public void ToColorTransfer_maps_ffmpeg_names(string name, VideoColorTransfer expected)
        => FFmpegNames.ToColorTransfer(name).Should().Be(expected);

    [Theory]
    [InlineData(-90, 270)]
    [InlineData(90, 90)]
    [InlineData(180, 180)]
    [InlineData(-180, 180)]
    [InlineData(450, 90)]
    [InlineData(-89.9, 270)]
    public void NormaliseQuarterTurn_wraps_and_snaps(double degrees, int expected)
        => FFmpegNames.NormaliseQuarterTurn(degrees).Should().Be(expected);
}
