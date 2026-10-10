using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Engine;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests.Engine;

public class FFmpegProbeTests
{
    [Theory]
    [InlineData("30000/1001", "30000/1001", 30000, 1001)]
    [InlineData("90000/1", "25/1", 25, 1)]
    [InlineData("0/0", "24/1", 24, 1)]
    [InlineData("25/1", "0/0", 25, 1)]
    public void ChooseFrameRate_prefers_the_real_rate_unless_it_is_absurd(string real, string average, int numerator, int denominator)
        => FFmpegProbe.ChooseFrameRate(real, average).Should().Be(new FrameRate(numerator, denominator));

    [Fact]
    public void ChooseFrameRate_is_unspecified_when_nothing_is_known()
        => FFmpegProbe.ChooseFrameRate("0/0", null).IsSpecified.Should().BeFalse();

    [Fact]
    public void Parse_maps_a_report_and_skips_attached_pictures()
    {
        //Arrange
        const string json = """
        {
          "streams": [
            { "index": 0, "codec_type": "video", "codec_name": "vp9", "width": 64, "height": 48, "pix_fmt": "yuv420p",
              "r_frame_rate": "25/1", "avg_frame_rate": "25/1", "tags": { "alpha_mode": "1", "DURATION": "00:00:00.400000000" } },
            { "index": 1, "codec_type": "audio", "codec_name": "flac", "sample_rate": "44100", "channels": 2, "bits_per_raw_sample": "24",
              "duration": "1.5" },
            { "index": 2, "codec_type": "video", "codec_name": "mjpeg", "width": 10, "height": 10, "disposition": { "attached_pic": 1 } }
          ],
          "format": { "format_name": "matroska,webm", "duration": "1.500000", "tags": { "TITLE": "x" } }
        }
        """;

        //Act
        MediaInfo info = FFmpegProbe.Parse(json);

        //Assert
        info.VideoStreams.Should().HaveCount(1);
        info.PrimaryVideo.HasAlpha.Should().BeTrue();
        info.PrimaryVideo.Duration.TotalMilliseconds.Should().Be(400);
        info.PrimaryAudio.BitDepth.Should().Be(24);
        info.PrimaryAudio.Duration.TotalSeconds.Should().Be(1.5);
        info.Tags["title"].Should().Be("x");
        info.Duration.TotalSeconds.Should().Be(1.5);
    }
}
