using System;
using System.Buffers.Binary;
using System.Linq;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Engine;
using CodeBrix.VideoProcessing.Tests.Resources;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests.Engine;

public class FFmpegVideoFrameSourceTests
{
    private readonly FFmpegVideoProcessor engine = new FFmpegVideoProcessor();

    [Fact]
    public void TryReadFrame_delivers_every_frame_as_i420_at_a_constant_rate()
    {
        //Arrange
        using IVideoFrameSource source = engine.OpenVideo(TestResources.Mp4Video);
        long count = 0;
        VideoFrameData last = default;

        //Act
        while (source.TryReadFrame(out VideoFrameData frame))
        {
            frame.Layout.Should().Be(VideoPixelLayout.I420);
            frame.Y.RowLength.Should().Be(1280);
            frame.Y.Rows.Should().Be(720);
            frame.U.RowLength.Should().Be(640);
            frame.V.Rows.Should().Be(360);
            frame.FrameIndex.Should().Be(count);
            count++;
            last = frame;
        }

        //Assert
        count.Should().Be(75);
        last.Timestamp.Should().Be(new FrameRate(25, 1).TimestampOf(74));
        source.Info.EstimatedFrameCount.Should().Be(75);
        source.Info.FrameRate.Should().Be(new FrameRate(25, 1));
        source.Info.BitDepth.Should().Be(8);
        source.Info.HasAlpha.Should().BeFalse();
        source.TryReadFrame(out VideoFrameData after).Should().BeFalse();
        after.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Seek_lands_on_the_same_picture_as_reading_sequentially()
    {
        //Arrange
        var rate = new FrameRate(25, 1);
        byte[] sequential;
        using (IVideoFrameSource source = engine.OpenVideo(TestResources.Mp4Video))
        {
            VideoFrameData frame = default;
            for (int i = 0; i <= 37; i++) source.TryReadFrame(out frame);
            sequential = frame.Y.ToArray();
        }

        //Act
        using IVideoFrameSource seeking = engine.OpenVideo(TestResources.Mp4Video);
        seeking.TryReadFrame(out VideoFrameData _);
        seeking.Seek(rate.TimestampOf(37));
        seeking.TryReadFrame(out VideoFrameData sought);

        //Assert
        sought.FrameIndex.Should().Be(37);
        sought.Timestamp.Should().Be(rate.TimestampOf(37));
        sought.Y.ToArray().SequenceEqual(sequential).Should().BeTrue();
    }

    [Fact]
    public void StartTime_between_frames_starts_at_the_next_frame()
    {
        //Arrange
        using IVideoFrameSource source = engine.OpenVideo(TestResources.Mp4Video, new VideoSourceOptions { StartTime = TimeSpan.FromMilliseconds(1010) });
        long count = 0;

        //Act
        source.TryReadFrame(out VideoFrameData first);
        count++;
        while (source.TryReadFrame(out VideoFrameData _)) count++;

        //Assert
        first.FrameIndex.Should().Be(26);
        count.Should().Be(75 - 26);
    }

    [Fact]
    public void ten_bit_sources_are_delivered_as_8_bit_and_say_so()
    {
        //Arrange
        using IVideoFrameSource source = engine.OpenVideo(TestResources.HdrVideo);

        //Act
        source.TryReadFrame(out VideoFrameData frame);

        //Assert
        source.Info.SourceBitDepth.Should().Be(10);
        source.Info.IsBitDepthReduced.Should().BeTrue();
        source.Info.ColorMatrix.Should().Be(VideoColorMatrix.Bt2020NonConstant);
        frame.BitDepth.Should().Be(8);
        frame.Width.Should().Be(640);
    }

    [Fact]
    public void frames_stay_in_stored_orientation()
    {
        //Arrange
        using IVideoFrameSource source = engine.OpenVideo("./Resources/input_3sec_rotation_90deg.mp4");

        //Act
        source.TryReadFrame(out VideoFrameData frame);

        //Assert
        frame.Width.Should().Be(1280);
        frame.Height.Should().Be(720);
        source.Info.RotationDegrees.Should().Be(270);
    }

    [Fact]
    public void rgba_layout_delivers_one_packed_plane()
    {
        //Arrange
        using IVideoFrameSource source = engine.OpenVideo(TestResources.WebmVideo, new VideoSourceOptions { PixelLayout = VideoPixelLayout.Rgba });

        //Act
        source.TryReadFrame(out VideoFrameData frame);

        //Assert
        frame.Layout.Should().Be(VideoPixelLayout.Rgba);
        frame.Rgba.RowLength.Should().Be(640 * 4);
        frame.Rgba.GetRow(0)[3].Should().Be((byte)255);
        source.Info.HasAlpha.Should().BeTrue();
    }

    [Fact]
    public void rgb_sources_are_converted_with_an_explicit_bt709_matrix_and_say_so()
    {
        //Arrange
        using var clip = new TemporaryFile("red.mkv");
        EngineTestMedia.CreateRgbClip(clip, "red");
        using IVideoFrameSource source = engine.OpenVideo(clip);

        //Act
        source.TryReadFrame(out VideoFrameData frame);
        byte luma = frame.Y.GetRow(10)[10];
        // Pure red: BT.709 luma is 0.2126 of white; BT.601's is 0.299 - far enough apart to tell which was used.
        bool full = source.Info.ColorRange == VideoColorRange.Full;
        int bt709 = full ? (int)Math.Round(255 * 0.2126) : (int)Math.Round(16 + 219 * 0.2126);
        int bt601 = full ? (int)Math.Round(255 * 0.299) : (int)Math.Round(16 + 219 * 0.299);

        //Assert
        source.Info.ColorMatrix.Should().Be(VideoColorMatrix.Bt709);
        frame.Layout.Should().Be(VideoPixelLayout.I420);
        Math.Abs(luma - bt709).Should().BeLessThanOrEqualTo(2);
        Math.Abs(luma - bt601).Should().BeGreaterThan(8);
    }

    [Fact]
    public void rgba_delivery_reports_the_rgb_matrix_whatever_the_source_was()
    {
        //Arrange
        using var clip = new TemporaryFile("red.mkv");
        EngineTestMedia.CreateRgbClip(clip, "red");
        using IVideoFrameSource fromRgb = engine.OpenVideo(clip, new VideoSourceOptions { PixelLayout = VideoPixelLayout.Rgba });
        using IVideoFrameSource fromYuv = engine.OpenVideo(TestResources.WebmVideo, new VideoSourceOptions { PixelLayout = VideoPixelLayout.Rgba });

        //Act
        fromRgb.TryReadFrame(out VideoFrameData frame);
        ReadOnlySpan<byte> pixel = frame.Rgba.GetRow(10).Slice(40, 4);

        //Assert
        fromRgb.Info.ColorMatrix.Should().Be(VideoColorMatrix.Rgb);
        fromYuv.Info.ColorMatrix.Should().Be(VideoColorMatrix.Rgb);
        // lavfi's colour source rounds through its own conversion, so "red" arrives within a couple of steps.
        ((int)pixel[0]).Should().BeGreaterThanOrEqualTo(250);
        ((int)pixel[1]).Should().BeLessThanOrEqualTo(5);
        ((int)pixel[2]).Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public void IncludeAlpha_delivers_the_alpha_plane()
    {
        //Arrange
        using var clip = new TemporaryFile("alpha.mkv");
        EngineTestMedia.CreateAlphaClip(clip);
        using IVideoFrameSource source = engine.OpenVideo(clip, new VideoSourceOptions { IncludeAlpha = true });
        long count = 0;

        //Act
        source.TryReadFrame(out VideoFrameData frame);
        ReadOnlySpan<byte> alphaRow = frame.A.GetRow(10);
        byte atZero = alphaRow[0];
        byte atTen = alphaRow[10];
        byte atSixtyThree = alphaRow[63];
        count++;
        while (source.TryReadFrame(out VideoFrameData _)) count++;

        //Assert
        source.Info.HasAlpha.Should().BeTrue();
        source.Info.SourceHasAlpha.Should().BeTrue();
        frame.HasAlpha.Should().BeTrue();
        frame.A.RowLength.Should().Be(64);
        frame.A.Rows.Should().Be(48);
        atZero.Should().Be((byte)0);
        atTen.Should().Be((byte)40);
        atSixtyThree.Should().Be((byte)252);
        count.Should().Be(10);
    }

    [Fact]
    public void alpha_is_not_delivered_unless_asked_for()
    {
        //Arrange
        using var clip = new TemporaryFile("alpha.mkv");
        EngineTestMedia.CreateAlphaClip(clip);
        using IVideoFrameSource source = engine.OpenVideo(clip);

        //Act
        source.TryReadFrame(out VideoFrameData frame);

        //Assert
        frame.HasAlpha.Should().BeFalse();
        source.Info.HasAlpha.Should().BeFalse();
        source.Info.SourceHasAlpha.Should().BeTrue();
    }

    [Fact]
    public void TryReadFrame_after_dispose_throws()
    {
        //Arrange
        IVideoFrameSource source = engine.OpenVideo(TestResources.WebmVideo);
        source.Dispose();

        //Act + Assert
        Assert.Throws<ObjectDisposedException>(() => source.TryReadFrame(out VideoFrameData _));
    }

    private static int Sample16(VideoPlane plane, int row, int column) =>
        BinaryPrimitives.ReadUInt16LittleEndian(plane.GetRow(row).Slice(2 * column, 2));

    private static int MaxSample16(VideoPlane plane)
    {
        int max = 0;
        for (int row = 0; row < plane.Rows; row++)
        {
            for (int column = 0; column < plane.RowLength / 2; column++) max = Math.Max(max, Sample16(plane, row, column));
        }

        return max;
    }

    [Fact]
    public void i444_layout_delivers_full_size_chroma()
    {
        //Arrange
        using IVideoFrameSource source = engine.OpenVideo(TestResources.Mp4Video, new VideoSourceOptions { PixelLayout = VideoPixelLayout.I444 });

        //Act
        source.TryReadFrame(out VideoFrameData frame);

        //Assert
        source.Info.PixelLayout.Should().Be(VideoPixelLayout.I444);
        source.Info.BitDepth.Should().Be(8);
        frame.Layout.Should().Be(VideoPixelLayout.I444);
        frame.BitDepth.Should().Be(8);
        frame.Y.RowLength.Should().Be(1280);
        frame.U.RowLength.Should().Be(1280);
        frame.U.Rows.Should().Be(720);
        frame.V.RowLength.Should().Be(1280);
        frame.V.Rows.Should().Be(720);
    }

    [Fact]
    public void gray_layout_delivers_the_same_luma_as_i420()
    {
        //Arrange
        byte[] i420Luma;
        using (IVideoFrameSource i420 = engine.OpenVideo(TestResources.Mp4Video))
        {
            i420.TryReadFrame(out VideoFrameData first);
            i420Luma = first.Y.ToArray();
        }

        using IVideoFrameSource source = engine.OpenVideo(TestResources.Mp4Video, new VideoSourceOptions { PixelLayout = VideoPixelLayout.Gray });

        //Act
        source.TryReadFrame(out VideoFrameData frame);

        //Assert
        source.Info.PixelLayout.Should().Be(VideoPixelLayout.Gray);
        source.Info.ColorRange.Should().Be(VideoColorRange.Limited);
        frame.Layout.Should().Be(VideoPixelLayout.Gray);
        frame.Y.RowLength.Should().Be(1280);
        frame.Y.Rows.Should().Be(720);
        frame.U.IsEmpty.Should().BeTrue();
        frame.V.IsEmpty.Should().BeTrue();
        frame.HasAlpha.Should().BeFalse();
        frame.Y.ToArray().SequenceEqual(i420Luma).Should().BeTrue();
    }

    [Fact]
    public void ten_bit_sources_can_be_delivered_at_ten_bits()
    {
        //Arrange
        using IVideoFrameSource source = engine.OpenVideo(TestResources.HdrVideo, new VideoSourceOptions { BitDepth = 10 });

        //Act
        source.TryReadFrame(out VideoFrameData frame);

        //Assert
        source.Info.BitDepth.Should().Be(10);
        source.Info.SourceBitDepth.Should().Be(10);
        source.Info.IsBitDepthReduced.Should().BeFalse();
        frame.Layout.Should().Be(VideoPixelLayout.I420);
        frame.BitDepth.Should().Be(10);
        frame.BytesPerSample.Should().Be(2);
        frame.Y.RowLength.Should().Be(640 * 2);
        frame.U.RowLength.Should().Be(320 * 2);
        MaxSample16(frame.Y).Should().BeLessThanOrEqualTo(1023);
        MaxSample16(frame.Y).Should().BeGreaterThan(255);
    }

    [Theory]
    [InlineData(VideoPixelLayout.I420, 10, 1023)]
    [InlineData(VideoPixelLayout.I420, 12, 4095)]
    [InlineData(VideoPixelLayout.I444, 10, 1023)]
    [InlineData(VideoPixelLayout.I444, 12, 4095)]
    [InlineData(VideoPixelLayout.Gray, 10, 1023)]
    [InlineData(VideoPixelLayout.Gray, 12, 4095)]
    public void eight_bit_sources_widen_to_the_requested_depth_in_the_low_bits(VideoPixelLayout layout, int bitDepth, int maximum)
    {
        //Arrange
        using IVideoFrameSource source = engine.OpenVideo(TestResources.Mp4Video, new VideoSourceOptions { PixelLayout = layout, BitDepth = bitDepth });

        //Act
        source.TryReadFrame(out VideoFrameData frame);
        int max = MaxSample16(frame.Y);

        //Assert
        source.Info.BitDepth.Should().Be(bitDepth);
        source.Info.SourceBitDepth.Should().Be(8);
        frame.Layout.Should().Be(layout);
        frame.BitDepth.Should().Be(bitDepth);
        frame.Y.RowLength.Should().Be(1280 * 2);
        max.Should().BeLessThanOrEqualTo(maximum);
        max.Should().BeGreaterThan(maximum / 4);
    }

    [Theory]
    [InlineData(VideoPixelLayout.I420, 8)]
    [InlineData(VideoPixelLayout.I444, 8)]
    [InlineData(VideoPixelLayout.Gray, 8)]
    public void alpha_is_delivered_with_every_eight_bit_yuv_and_gray_layout(VideoPixelLayout layout, int bitDepth)
    {
        //Arrange
        using var clip = new TemporaryFile("alpha.mkv");
        EngineTestMedia.CreateAlphaClip(clip);
        using IVideoFrameSource source = engine.OpenVideo(clip, new VideoSourceOptions { PixelLayout = layout, BitDepth = bitDepth, IncludeAlpha = true });

        //Act
        source.TryReadFrame(out VideoFrameData frame);
        ReadOnlySpan<byte> alphaRow = frame.A.GetRow(10);

        //Assert
        source.Info.HasAlpha.Should().BeTrue();
        frame.Layout.Should().Be(layout);
        frame.A.RowLength.Should().Be(64);
        frame.A.Rows.Should().Be(48);
        alphaRow[0].Should().Be((byte)0);
        alphaRow[10].Should().Be((byte)40);
        alphaRow[63].Should().Be((byte)252);
    }

    [Theory]
    [InlineData(VideoPixelLayout.I420, 10, 1023)]
    [InlineData(VideoPixelLayout.I444, 10, 1023)]
    [InlineData(VideoPixelLayout.I444, 12, 4095)]
    [InlineData(VideoPixelLayout.Gray, 10, 1023)]
    [InlineData(VideoPixelLayout.Gray, 12, 4095)]
    public void alpha_is_delivered_at_the_frame_depth_and_full_range(VideoPixelLayout layout, int bitDepth, int maximum)
    {
        //Arrange
        using var clip = new TemporaryFile("alpha.mkv");
        EngineTestMedia.CreateAlphaClip(clip);
        using IVideoFrameSource source = engine.OpenVideo(clip, new VideoSourceOptions { PixelLayout = layout, BitDepth = bitDepth, IncludeAlpha = true });

        //Act
        source.TryReadFrame(out VideoFrameData frame);
        int atZero = Sample16(frame.A, 10, 0);
        int atSixtyThree = Sample16(frame.A, 10, 63);

        //Assert
        frame.Layout.Should().Be(layout);
        frame.BitDepth.Should().Be(bitDepth);
        frame.HasAlpha.Should().BeTrue();
        frame.A.RowLength.Should().Be(64 * 2);
        frame.A.Rows.Should().Be(48);
        atZero.Should().Be(0);
        atSixtyThree.Should().BeLessThanOrEqualTo(maximum);
        atSixtyThree.Should().BeGreaterThan(maximum * 240 / 255);
    }

    [Fact]
    public void twelve_bit_i420_with_alpha_is_refused()
    {
        //Arrange
        using var clip = new TemporaryFile("alpha.mkv");
        EngineTestMedia.CreateAlphaClip(clip);
        var options = new VideoSourceOptions { BitDepth = 12, IncludeAlpha = true };

        //Act
        var exception = Assert.Throws<NotSupportedException>(() => engine.OpenVideo(clip, options));

        //Assert
        exception.Message.Should().Contain("yuva420p12le");
    }

    [Fact]
    public void twelve_bit_i420_without_alpha_in_the_source_is_delivered()
    {
        //Arrange
        using IVideoFrameSource source = engine.OpenVideo(TestResources.WebmVideo, new VideoSourceOptions { BitDepth = 12, IncludeAlpha = true });

        //Act
        source.TryReadFrame(out VideoFrameData frame);

        //Assert
        frame.BitDepth.Should().Be(12);
        frame.HasAlpha.Should().BeFalse();
    }

    [Theory]
    [InlineData(VideoPixelLayout.I420, 8, false, "yuv420p")]
    [InlineData(VideoPixelLayout.I420, 8, true, "yuva420p")]
    [InlineData(VideoPixelLayout.I420, 10, false, "yuv420p10le")]
    [InlineData(VideoPixelLayout.I420, 10, true, "yuva420p10le")]
    [InlineData(VideoPixelLayout.I420, 12, false, "yuv420p12le")]
    [InlineData(VideoPixelLayout.I444, 8, true, "yuva444p")]
    [InlineData(VideoPixelLayout.I444, 12, false, "yuv444p12le")]
    [InlineData(VideoPixelLayout.I444, 12, true, "yuva444p12le")]
    [InlineData(VideoPixelLayout.Gray, 8, false, "yuv420p")]
    [InlineData(VideoPixelLayout.Gray, 10, false, "yuv420p10le")]
    [InlineData(VideoPixelLayout.Gray, 12, false, "yuv420p12le")]
    [InlineData(VideoPixelLayout.Gray, 8, true, "yuva420p")]
    [InlineData(VideoPixelLayout.Gray, 12, true, "yuva444p12le")]
    [InlineData(VideoPixelLayout.Rgba, 8, true, "rgba")]
    public void ChoosePixelFormat_maps_layout_depth_and_alpha_exactly(VideoPixelLayout layout, int bitDepth, bool alpha, string expected)
        => FFmpegVideoFrameSource.ChoosePixelFormat(layout, bitDepth, alpha).Should().Be(expected);

    [Fact]
    public void ChoosePixelFormat_refuses_what_ffmpeg_cannot_produce_exactly()
    {
        //Act + Assert
        Assert.Throws<NotSupportedException>(() => FFmpegVideoFrameSource.ChoosePixelFormat(VideoPixelLayout.I420, 12, true));
        Assert.Throws<NotSupportedException>(() => FFmpegVideoFrameSource.ChoosePixelFormat(VideoPixelLayout.Nv12, 8, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => FFmpegVideoFrameSource.ChoosePixelFormat(VideoPixelLayout.Rgba, 10, false));
    }
}
