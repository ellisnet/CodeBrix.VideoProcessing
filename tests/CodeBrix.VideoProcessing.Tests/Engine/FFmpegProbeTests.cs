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

    [Fact]
    public void Parse_maps_subtitle_streams_and_chapters_and_drops_placeholders()
    {
        //Arrange
        const string json = """
        {
          "streams": [
            { "index": 0, "codec_type": "video", "codec_name": "h264", "width": 64, "height": 48, "r_frame_rate": "25/1" },
            { "index": 1, "codec_type": "subtitle", "codec_name": "webvtt", "tags": { "language": "ger", "title": "Deutsch" } },
            { "index": 2, "codec_type": "subtitle", "codec_name": "ass", "tags": { "language": "und" } }
          ],
          "chapters": [
            { "id": 7, "time_base": "1/1000", "start": 0, "start_time": "0:00:00.000000", "end": 1500, "end_time": "0:00:01.500000", "tags": { "title": "One" } },
            { "id": 8, "time_base": "1/1000", "start": 1500, "start_time": "0:00:01.500000", "end": 3000, "end_time": "0:00:03.000000" }
          ],
          "format": { "format_name": "matroska,webm", "duration": "3.000000" }
        }
        """;

        //Act
        MediaInfo info = FFmpegProbe.Parse(json);

        //Assert
        info.SubtitleStreams.Should().HaveCount(2);
        info.SubtitleStreams[0].Index.Should().Be(1);
        info.SubtitleStreams[0].CodecName.Should().Be("webvtt");
        info.SubtitleStreams[0].Language.Should().Be("ger");
        info.SubtitleStreams[0].Title.Should().Be("Deutsch");
        info.SubtitleStreams[1].Language.Should().BeNull();
        info.SubtitleStreams[1].Title.Should().BeNull();
        info.Chapters.Should().HaveCount(2);
        info.Chapters[0].Title.Should().Be("One");
        info.Chapters[0].End.TotalSeconds.Should().Be(1.5);
        info.Chapters[1].Index.Should().Be(1);
        info.Chapters[1].Title.Should().BeNull();
        info.Chapters[1].Tags.Should().BeEmpty();
        info.Chapters[1].Duration.TotalSeconds.Should().Be(1.5);
    }
}
