using System;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Tests.Engine;
using CodeBrix.VideoProcessing.Tests.Resources;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests;

public class VideoStreamTests
{
    private static VideoStream ParseVideo(string streamJson)
    {
        string json = "{ \"streams\": [ { \"index\": 0, \"codec_type\": \"video\", \"codec_name\": \"h264\", \"width\": 64, \"height\": 48" +
                      (string.IsNullOrEmpty(streamJson) ? string.Empty : ", " + streamJson) +
                      " } ], \"format\": { \"format_name\": \"mov,mp4,m4a,3gp,3g2,mj2\", \"duration\": \"0:00:01.001000\" } }";
        return FFProbe.ParseJson(json).PrimaryVideoStream;
    }

    [Fact]
    public void ExactFrameRate_round_trips_30000_over_1001()
    {
        //Act
        VideoStream video = ParseVideo("\"r_frame_rate\": \"30000/1001\", \"avg_frame_rate\": \"30000/1001\"");

        //Assert
        video.ExactFrameRate.Should().Be(new FrameRate(30000, 1001));
        video.ExactFrameRate.ToString().Should().Be("30000/1001");
        video.RealFrameRate.Should().Be(new FrameRate(30000, 1001));
        video.FrameRate.Should().BeApproximately(29.97, 0.001);
    }

    [Fact]
    public void ExactFrameRate_prefers_the_average_rate()
    {
        //Act
        VideoStream video = ParseVideo("\"r_frame_rate\": \"60/1\", \"avg_frame_rate\": \"60000/2002\"");

        //Assert
        video.ExactFrameRate.Should().Be(new FrameRate(30000, 1001));
        video.RealFrameRate.Should().Be(new FrameRate(60, 1));
    }

    [Theory]
    [InlineData("\"r_frame_rate\": \"24000/1001\", \"avg_frame_rate\": \"0/0\"")]
    [InlineData("\"r_frame_rate\": \"24000/1001\"")]
    public void ExactFrameRate_falls_back_to_the_real_rate(string rates)
    {
        //Act
        VideoStream video = ParseVideo(rates);

        //Assert
        video.ExactFrameRate.Should().Be(new FrameRate(24000, 1001));
    }

    [Fact]
    public void ExactFrameRate_is_unspecified_when_neither_rate_is_known()
    {
        //Act
        VideoStream video = ParseVideo("\"r_frame_rate\": \"0/0\", \"avg_frame_rate\": \"0/0\"");

        //Assert
        video.ExactFrameRate.IsSpecified.Should().BeFalse();
        video.RealFrameRate.IsSpecified.Should().BeFalse();
    }

    [Fact]
    public void FrameCount_comes_from_nb_frames()
    {
        //Act
        VideoStream video = ParseVideo("\"nb_frames\": \"75\"");

        //Assert
        video.FrameCount.Should().Be(75);
    }

    [Fact]
    public void FrameCount_is_null_when_nb_frames_is_absent()
    {
        //Act
        VideoStream video = ParseVideo(null);

        //Assert
        video.FrameCount.Should().BeNull();
    }

    [Fact]
    public void DisplayMatrixRotation_is_the_raw_counter_clockwise_angle()
    {
        //Act
        VideoStream video = ParseVideo("\"side_data_list\": [ { \"side_data_type\": \"Display Matrix\", \"rotation\": -90 } ], \"tags\": { \"rotate\": \"90\" }");

        //Assert
        video.DisplayMatrixRotation.Should().Be(-90);
    }

    [Fact]
    public void DisplayMatrixRotation_is_null_without_a_display_matrix()
    {
        //Act
        VideoStream video = ParseVideo("\"tags\": { \"rotate\": \"90\" }");

        //Assert
        video.DisplayMatrixRotation.Should().BeNull();
        video.Rotation.Should().Be(90);
    }

    // A display matrix as ffmpeg builds it: av_display_rotation_set (counter-clockwise degrees), then
    // av_display_matrix_flip (hflip negates the first column, vflip the second). 16.16 fixed point, w = 1 << 30.
    private static long[] DisplayMatrix(int counterClockwiseDegrees, bool hflip, bool vflip)
    {
        double radians = -counterClockwiseDegrees * Math.PI / 180;
        long c = (long)Math.Round(Math.Cos(radians) * 65536);
        long s = (long)Math.Round(Math.Sin(radians) * 65536);
        long[] matrix = { c, -s, 0, s, c, 0, 0, 0, 1L << 30 };
        for (int i = 0; i < 9; i++)
        {
            if (hflip && i % 3 == 0) matrix[i] = -matrix[i];
            if (vflip && i % 3 == 1) matrix[i] = -matrix[i];
        }

        return matrix;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    [InlineData(-90)]
    public void A_rotation_alone_is_not_mirrored(int degrees)
    {
        //Arrange
        long[] matrix = DisplayMatrix(degrees, hflip: false, vflip: false);

        //Assert
        (MediaAnalysisUtils.DisplayMatrixDeterminant(matrix)).Should().BeGreaterThan(0);
        (MediaAnalysisUtils.IsMirroredDisplayMatrix(matrix)).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(0, false, true)]
    [InlineData(90, true, false)]
    [InlineData(90, false, true)]
    [InlineData(180, true, false)]
    [InlineData(180, false, true)]
    [InlineData(270, true, false)]
    [InlineData(270, false, true)]
    public void A_single_flip_on_any_rotation_is_mirrored(int degrees, bool hflip, bool vflip)
    {
        //Arrange
        long[] matrix = DisplayMatrix(degrees, hflip, vflip);

        //Assert
        (MediaAnalysisUtils.DisplayMatrixDeterminant(matrix)).Should().BeLessThan(0);
        (MediaAnalysisUtils.IsMirroredDisplayMatrix(matrix)).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Both_flips_are_a_rotation_and_not_mirrored(int degrees)
        => (MediaAnalysisUtils.IsMirroredDisplayMatrix(DisplayMatrix(degrees, hflip: true, vflip: true))).Should().BeFalse();

    [Fact]
    public void Full_range_matrix_entries_do_not_overflow_the_determinant()
    {
        //Arrange
        long[] matrix = { int.MaxValue, int.MinValue, 0, int.MaxValue, int.MaxValue, 0, 0, 0, 1L << 30 };

        //Assert
        (MediaAnalysisUtils.IsMirroredDisplayMatrix(matrix)).Should().BeFalse();
        (MediaAnalysisUtils.IsMirroredDisplayMatrix(null)).Should().BeFalse();
    }

    [Fact]
    public void ParseDisplayMatrix_reads_ffprobe_text()
    {
        //Act
        long[] matrix = MediaAnalysisUtils.ParseDisplayMatrix(
            "\n00000000:            0      -65536           0\n00000001:       -65536           0           0\n00000002:            0           0  1073741824\n");

        //Assert
        (matrix).Should().Equal(0L, -65536L, 0L, -65536L, 0L, 0L, 0L, 0L, 1073741824L);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\n00000000:            0      -65536           0\n")]
    [InlineData("\n00000000:            0      x           0\n00000001:       -65536           0           0\n00000002:            0           0  1073741824\n")]
    public void ParseDisplayMatrix_is_null_for_anything_but_nine_integers(string text)
        => (MediaAnalysisUtils.ParseDisplayMatrix(text)).Should().BeNull();

    [Fact]
    public void IsDisplayMatrixMirrored_reads_the_display_matrix_side_data()
    {
        //Act
        VideoStream mirrored = ParseVideo("\"side_data_list\": [ { \"side_data_type\": \"Display Matrix\", " +
            "\"displaymatrix\": \"\\n00000000:       -65536           0           0\\n00000001:            0       65536           0\\n00000002:            0           0  1073741824\\n\", " +
            "\"rotation\": -180 } ]");
        VideoStream rotated = ParseVideo("\"side_data_list\": [ { \"side_data_type\": \"Display Matrix\", " +
            "\"displaymatrix\": \"\\n00000000:            0       65536           0\\n00000001:       -65536           0           0\\n00000002:            0           0  1073741824\\n\", " +
            "\"rotation\": -90 } ]");
        VideoStream bare = ParseVideo("\"side_data_list\": [ { \"side_data_type\": \"Display Matrix\", \"rotation\": -90 } ]");
        VideoStream none = ParseVideo(null);

        //Assert
        (mirrored.IsDisplayMatrixMirrored).Should().BeTrue();
        (mirrored.DisplayMatrixRotation).Should().Be(-180);
        (rotated.IsDisplayMatrixMirrored).Should().BeFalse();
        (bare.IsDisplayMatrixMirrored).Should().BeFalse();
        (none.IsDisplayMatrixMirrored).Should().BeFalse();
    }

    [Fact]
    public void Duration_keeps_ffprobe_microseconds()
    {
        //Act
        VideoStream video = ParseVideo("\"duration\": \"0:00:03.003003\"");

        //Assert
        video.Duration.Should().Be(TimeSpan.FromTicks(30_030_030));
    }

    [Theory]
    [InlineData("3.003003", 30_030_030)]
    [InlineData("0:00:00.4", 4_000_000)]
    [InlineData("0:00:03", 30_000_000)]
    [InlineData("0:00:00.123456789", 1_234_568)]
    [InlineData("N/A", 0)]
    public void ParseDuration_reads_sexagesimal_and_plain_seconds_to_the_tick(string text, long ticks)
    {
        //Act
        TimeSpan duration = MediaAnalysisUtils.ParseDuration(text);

        //Assert
        duration.Ticks.Should().Be(ticks);
    }

    [Fact]
    public async Task AnalyseAsync_reports_the_exact_rate_and_frame_count_of_an_ntsc_mp4()
    {
        //Arrange
        using var clip = new TemporaryFile("ntsc.mp4");
        EngineTestMedia.CreateNtscClip(clip);

        //Act
        IMediaAnalysis analysis = await FFProbe.AnalyseAsync(clip, cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        VideoStream video = analysis.PrimaryVideoStream;
        video.ExactFrameRate.Should().Be(new FrameRate(30000, 1001));
        video.RealFrameRate.Should().Be(new FrameRate(30000, 1001));
        video.FrameCount.Should().Be(30);
        video.Duration.Should().Be(TimeSpan.FromTicks(10_010_000));
    }

    [Fact]
    public async Task AnalyseAsync_reports_no_frame_count_for_matroska()
    {
        //Arrange
        using var clip = new TemporaryFile("ntsc.mkv");
        EngineTestMedia.CreateNtscClip(clip);

        //Act
        IMediaAnalysis analysis = await FFProbe.AnalyseAsync(clip, cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        analysis.PrimaryVideoStream.ExactFrameRate.Should().Be(new FrameRate(30000, 1001));
        analysis.PrimaryVideoStream.FrameCount.Should().BeNull();
    }

    [Fact]
    public void Analyse_reports_the_fixture_frame_count_and_display_matrix()
    {
        //Act
        VideoStream plain = FFProbe.Analyse(TestResources.Mp4Video).PrimaryVideoStream;
        VideoStream rotated = FFProbe.Analyse(TestResources.Mp4VideoRotation).PrimaryVideoStream;
        VideoStream webm = FFProbe.Analyse(TestResources.WebmVideo).PrimaryVideoStream;

        //Assert
        plain.FrameCount.Should().Be(75);
        plain.ExactFrameRate.Should().Be(new FrameRate(25, 1));
        plain.DisplayMatrixRotation.Should().BeNull();
        rotated.DisplayMatrixRotation.Should().Be(90);
        webm.FrameCount.Should().BeNull();
        webm.ExactFrameRate.Should().Be(new FrameRate(30, 1));
    }
}
