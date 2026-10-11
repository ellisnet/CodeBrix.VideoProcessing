using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core.Media;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class Mp4TrackTimingTests
{
    [Fact]
    public void the_bundled_clip_reads_as_exactly_30_fps_and_120_frames()
    {
        //Act
        List<Mp4TrackTimingInfo> tracks = Mp4TrackTiming.Read(HostTools.Clip);

        //Assert
        tracks.Count.Should().Be(2);
        Mp4TrackTimingInfo video = tracks.Find(t => t.Handler == "vide");
        video.FrameRate.Should().Be(new FrameRate(30, 1));
        video.SampleCount.Should().Be(120);
        ((double)video.Duration / video.Timescale).Should().BeApproximately(4.0, 0.001);
        tracks.Find(t => t.Handler == "soun").Should().NotBeNull();
    }

    [Fact]
    public void an_ntsc_clip_keeps_its_exact_rational()
    {
        Assert.SkipUnless(HostTools.FFmpegAvailable, "ffmpeg is not on PATH");

        //Arrange
        string folder = HostTools.NewFolder();
        string path = Path.Combine(folder, "ntsc.mp4");
        HostTools.FFmpeg($"-f lavfi -i testsrc=size=64x48:rate=30000/1001 -frames:v 30 -c:v libx264 -bf 2 -pix_fmt yuv420p \"{path}\"");

        //Act
        Mp4TrackTimingInfo video = Mp4TrackTiming.Read(path).Find(t => t.Handler == "vide");

        //Assert
        video.FrameRate.Should().Be(new FrameRate(30000, 1001));
        video.SampleCount.Should().Be(30);
        Directory.Delete(folder, true);
    }

    [Fact]
    public void reordered_decode_times_still_give_the_presentation_rate()
    {
        //Arrange - the shape MediaMuxer writes for an IPBB stream at 90 kHz: odd decode deltas, ctts restoring 30 fps.
        var stts = new List<(long, long)> { (1, 9000), (2, 9), (1, 8982), (2, 9), (1, 8982), (2, 9), (1, 3000) };
        // presentation order 0, 3, 1, 2, 6, 4, 5, 9, 7, 8 (frames of 3000 ticks); decode times from stts above.
        long[] decode = { 0, 9000, 9009, 9018, 18000, 18009, 18018, 27000, 27009, 27018 };
        long[] presentation = { 0, 9000, 3000, 6000, 18000, 12000, 15000, 27000, 21000, 24000 };
        var ctts = new List<(long, long)>();
        for (int i = 0; i < decode.Length; i++) ctts.Add((1, presentation[i] - decode[i]));

        //Act
        FrameRate rate = Mp4TrackTiming.RateFrom(stts, ctts, 90000);

        //Assert
        rate.Should().Be(new FrameRate(30, 1));
    }

    [Fact]
    public void a_constant_table_gives_timescale_over_delta()
        => Mp4TrackTiming.RateFrom(new List<(long, long)> { (100, 1001), (1, 1001) }, 30000).Should().Be(new FrameRate(30000, 1001));

    [Fact]
    public void a_variable_table_gives_the_snapped_average()
        => Mp4TrackTiming.RateFrom(new List<(long, long)> { (1, 300), (1, 450), (1, 600), (1, 450), (1, 300), (1, 600), (1, 300) }, 9000).Should().Be(new FrameRate(20, 1));

    [Fact]
    public void empty_or_unreadable_input_gives_nothing()
    {
        //Arrange
        string folder = HostTools.NewFolder();
        string path = Path.Combine(folder, "junk.mp4");
        File.WriteAllText(path, "not an mp4 at all");

        //Assert
        Mp4TrackTiming.RateFrom(new List<(long, long)>(), 1000).IsSpecified.Should().BeFalse();
        Mp4TrackTiming.Read(path).Should().BeEmpty();
        Mp4TrackTiming.Read(Path.Combine(folder, "missing.mp4")).Should().BeEmpty();
        Directory.Delete(folder, true);
    }
}
