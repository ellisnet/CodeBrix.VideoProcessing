using System;
using System.Collections.Generic;
using CodeBrix.VideoProcessing.Core.Encoding;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

// Drives the contract end to end through the fake engine and the fake encoder, the way a consumer such as an
// authoring library does: analyse, read frames, feed them to a writer or an encoder. Exercises the documented
// ownership and timestamp rules and the encoder's pull loop. (Registry scenarios live in VideoEncodersTests.)
public class ContractScenarioCoverage
{
    [Fact]
    public void frames_read_from_a_source_carry_constant_rate_timestamps()
    {
        //Arrange
        IVideoProcessor engine = new FakeVideoProcessor();
        var timestamps = new List<TimeSpan>();

        //Act
        using (IVideoFrameSource source = engine.OpenVideo("clip"))
        {
            while (source.TryReadFrame(out VideoFrameData frame))
            {
                timestamps.Add(frame.Timestamp);
                source.Info.FrameRate.TimestampOf(frame.FrameIndex).Should().Be(frame.Timestamp);
            }
        }

        //Assert
        timestamps.Should().HaveCount(5);
        timestamps[4].Should().Be(TimeSpan.FromMilliseconds(160));
    }

    [Fact]
    public void Seek_lands_on_the_first_frame_at_or_after_the_position()
    {
        //Arrange
        IVideoProcessor engine = new FakeVideoProcessor();
        using IVideoFrameSource source = engine.OpenVideo("clip");

        //Act
        source.Seek(TimeSpan.FromMilliseconds(41));
        source.TryReadFrame(out VideoFrameData frame);

        //Assert
        frame.FrameIndex.Should().Be(2);
    }

    [Fact]
    public void a_kept_frame_must_be_copied_because_sources_reuse_buffers()
    {
        //Arrange
        IVideoProcessor engine = new FakeVideoProcessor();
        using IVideoFrameSource source = engine.OpenVideo("clip");
        source.TryReadFrame(out VideoFrameData first);
        VideoFrameData kept = first.Copy();

        //Act
        source.TryReadFrame(out VideoFrameData _);

        //Assert
        kept.Y.GetRow(0)[0].Should().Be((byte)16);
        first.Y.GetRow(0)[0].Should().Be((byte)17);
    }

    [Fact]
    public void a_writer_stamps_frames_from_its_own_index_and_rate()
    {
        //Arrange
        var engine = new FakeVideoProcessor();
        MediaInfo info = engine.Analyse("clip", TestContext.Current.CancellationToken);
        var request = new StandardVideoRequest
        {
            OutputPath = "out.mp4",
            Width = info.PrimaryVideo.Width,
            Height = info.PrimaryVideo.Height,
            FrameRate = new FrameRate(50, 1)
        };
        var reports = new List<VideoProgress>();

        //Act
        using (IVideoFrameSource source = engine.OpenVideo("clip"))
        using (IStandardVideoWriter writer = engine.CreateWriter(request, new SynchronousProgress(reports)))
        {
            while (source.TryReadFrame(out VideoFrameData frame)) writer.WriteFrame(frame);
            writer.Finish();
        }

        //Assert
        FakeVideoProcessor.FakeWriter written = engine.Writers[0];
        written.FramesWritten.Should().Be(5);
        written.Frames[4].Timestamp.Should().Be(TimeSpan.FromMilliseconds(80));
        written.Frames[4].Y.GetRow(0)[0].Should().Be((byte)20);
        reports.Should().HaveCount(5);
        reports[4].Fraction.Should().BeNull();
    }

    [Fact]
    public void a_writer_refuses_frames_that_do_not_match_the_request()
    {
        //Arrange
        var engine = new FakeVideoProcessor();
        var request = new StandardVideoRequest { OutputPath = "o.mp4", Width = 8, Height = 8, FrameRate = new FrameRate(25, 1) };
        using IStandardVideoWriter writer = engine.CreateWriter(request);
        using IVideoFrameSource source = engine.OpenVideo("clip");
        source.TryReadFrame(out VideoFrameData frame);

        //Act + Assert
        Assert.Throws<ArgumentException>(() => writer.WriteFrame(frame));
    }

    [Fact]
    public void CreateWriter_refuses_hevc_without_the_capability()
    {
        //Arrange
        var engine = new FakeVideoProcessor();
        var request = new StandardVideoRequest
        {
            OutputPath = "o.mp4",
            Width = 8,
            Height = 8,
            FrameRate = new FrameRate(25, 1),
            Profile = StandardVideoProfile.Mp4HevcAac
        };

        //Act + Assert
        Assert.Throws<NotSupportedException>(() => engine.CreateWriter(request));
    }

    [Fact]
    public void Snapshot_returns_an_owned_frame()
    {
        //Arrange
        var engine = new FakeVideoProcessor(capabilities: VideoProcessorCapabilities.Snapshot);

        //Act
        VideoFrameData frame = engine.Snapshot("clip", TimeSpan.FromMilliseconds(80), TestContext.Current.CancellationToken);

        //Assert
        frame.FrameIndex.Should().Be(2);
        frame.Y.GetRow(0)[0].Should().Be((byte)18);
    }

    [Fact]
    public void an_encoder_pull_loop_delivers_every_frame_once_flushed()
    {
        //Arrange
        IVideoProcessor engine = new FakeVideoProcessor();
        IVideoEncoderFactory factory = new FakeVideoEncoderFactory(lookAhead: 2);
        var request = new VideoEncoderRequest { Width = 4, Height = 2, FrameRate = new FrameRate(50, 1) };
        var packets = new List<EncodedVideoPacket>();
        var packetsAfterEachEncode = new List<int>();
        ReadOnlyMemory<byte> configurationBeforeAnyPacket;
        ReadOnlyMemory<byte> configuration;
        long submitted;
        long produced;
        bool moreAfterTheEnd;
        EncodedVideoPacket afterTheEnd;

        //Act
        using (IVideoFrameSource source = engine.OpenVideo("clip"))
        using (IVideoEncoder encoder = factory.Create(request))
        {
            configurationBeforeAnyPacket = encoder.CodecConfigurationRecord;
            while (source.TryReadFrame(out VideoFrameData frame))
            {
                encoder.Encode(frame, forceKeyFrame: false);
                while (encoder.TryReadPacket(out EncodedVideoPacket packet)) packets.Add(packet);
                packetsAfterEachEncode.Add(packets.Count);
            }

            encoder.Flush();
            while (encoder.TryReadPacket(out EncodedVideoPacket packet)) packets.Add(packet);
            moreAfterTheEnd = encoder.TryReadPacket(out afterTheEnd);
            configuration = encoder.CodecConfigurationRecord;
            submitted = encoder.FramesSubmitted;
            produced = encoder.PacketsProduced;
        }

        //Assert
        configurationBeforeAnyPacket.IsEmpty.Should().BeTrue();
        configuration.IsEmpty.Should().BeFalse();
        packetsAfterEachEncode.Should().Equal(0, 0, 1, 2, 3);
        packets.Should().HaveCount(5);
        for (int i = 0; i < packets.Count; i++)
        {
            packets[i].FrameIndex.Should().Be(i);
            packets[i].Timestamp.Should().Be(request.FrameRate.TimestampOf(i));
            // The source reuses its buffers; the packet still carries the picture it was given.
            packets[i].Data.Span[1].Should().Be((byte)(16 + i));
        }

        packets[0].IsKeyFrame.Should().BeTrue();
        submitted.Should().Be(5);
        produced.Should().Be(5);
        moreAfterTheEnd.Should().BeFalse();
        afterTheEnd.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void an_encoder_refuses_frames_that_do_not_match_its_request_and_frames_after_Flush()
    {
        //Arrange
        IVideoEncoderFactory factory = new FakeVideoEncoderFactory();
        using IVideoEncoder encoder = factory.Create(new VideoEncoderRequest { Width = 2, Height = 2, FrameRate = new FrameRate(25, 1) });
        VideoFrameData eightBitGray = VideoFrameData.CreateGray(2, 2, VideoPlane.FromArray(new byte[4], 2, 2), TimeSpan.Zero, 0);
        VideoFrameData tenBitI420 = VideoFrameData.CreateI420(
            2,
            2,
            VideoPlane.FromArray(new byte[8], 4, 2),
            VideoPlane.FromArray(new byte[2], 2, 1),
            VideoPlane.FromArray(new byte[2], 2, 1),
            TimeSpan.Zero,
            0,
            bitDepth: 10);
        VideoFrameData good = VideoFrameData.CreateI420(
            2,
            2,
            VideoPlane.FromArray(new byte[4], 2, 2),
            VideoPlane.FromArray(new byte[1], 1, 1),
            VideoPlane.FromArray(new byte[1], 1, 1),
            TimeSpan.Zero,
            0);

        //Act + Assert
        Assert.Throws<ArgumentException>(() => encoder.Encode(eightBitGray, false));
        Assert.Throws<ArgumentException>(() => encoder.Encode(tenBitI420, false));
        Assert.Throws<ArgumentException>(() => encoder.Encode(default(VideoFrameData), false));
        encoder.Encode(good, false);
        encoder.Flush();
        encoder.Flush();
        Assert.Throws<InvalidOperationException>(() => encoder.Encode(good, false));
        encoder.FramesSubmitted.Should().Be(1);
    }

    [Fact]
    public void forced_and_interval_keyframes_are_marked_on_the_packets()
    {
        //Arrange
        IVideoEncoderFactory factory = new FakeVideoEncoderFactory(lookAhead: 0);
        var request = new VideoEncoderRequest { Width = 1, Height = 1, PixelLayout = VideoPixelLayout.Gray, FrameRate = new FrameRate(25, 1), MaxKeyFrameInterval = 4 };
        var keyframes = new List<long>();

        //Act
        using (IVideoEncoder encoder = factory.Create(request))
        {
            for (int i = 0; i < 10; i++)
            {
                encoder.Encode(VideoFrameData.CreateGray(1, 1, VideoPlane.FromArray(new byte[1], 1, 1), TimeSpan.Zero, 0), forceKeyFrame: i == 6);
                while (encoder.TryReadPacket(out EncodedVideoPacket packet))
                {
                    if (packet.IsKeyFrame) keyframes.Add(packet.FrameIndex);
                }
            }
        }

        //Assert
        keyframes.Should().Equal(0, 4, 6, 8);
    }

    [Fact]
    public void an_encoder_without_forced_keyframes_refuses_to_force_one()
    {
        //Arrange
        var capabilities = new VideoEncoderCapabilities
        {
            PixelLayouts = VideoEncoderPixelLayouts.Gray,
            BitDepths = VideoEncoderBitDepths.Depth8,
            SupportsForcedKeyFrames = false
        };
        IVideoEncoderFactory factory = new FakeVideoEncoderFactory(capabilities: capabilities);
        using IVideoEncoder encoder = factory.Create(new VideoEncoderRequest { Width = 1, Height = 1, PixelLayout = VideoPixelLayout.Gray, FrameRate = new FrameRate(25, 1) });
        VideoFrameData frame = VideoFrameData.CreateGray(1, 1, VideoPlane.FromArray(new byte[1], 1, 1), TimeSpan.Zero, 0);

        //Act + Assert
        Assert.Throws<NotSupportedException>(() => encoder.Encode(frame, forceKeyFrame: true));
    }

    [Fact]
    public void alpha_travels_as_a_second_full_range_gray_stream_in_lock_step()
    {
        //Arrange
        IVideoEncoderFactory factory = new FakeVideoEncoderFactory();
        var rate = new FrameRate(30, 1);
        var colourRequest = new VideoEncoderRequest { Width = 2, Height = 2, BitDepth = 10, FrameRate = rate, MaxKeyFrameInterval = 3 };
        VideoEncoderRequest alphaRequest = colourRequest.Clone();
        alphaRequest.PixelLayout = VideoPixelLayout.Gray;
        alphaRequest.ColorRange = VideoColorRange.Full;
        var colourPackets = new List<EncodedVideoPacket>();
        var alphaPackets = new List<EncodedVideoPacket>();

        //Act
        using (IVideoEncoder colour = factory.Create(colourRequest))
        using (IVideoEncoder alpha = factory.Create(alphaRequest))
        {
            for (int i = 0; i < 7; i++)
            {
                VideoFrameData frame = VideoFrameData.CreateI420(
                    2,
                    2,
                    VideoPlane.FromArray(new byte[8], 4, 2),
                    VideoPlane.FromArray(new byte[2], 2, 1),
                    VideoPlane.FromArray(new byte[2], 2, 1),
                    rate.TimestampOf(i),
                    i,
                    VideoPlane.FromArray(new byte[] { (byte)(100 + i), 3, 0, 0, 0, 0, 0, 0 }, 4, 2),
                    10);
                colour.Encode(frame, false);
                alpha.Encode(VideoFrameData.CreateGray(frame.Width, frame.Height, frame.A, frame.Timestamp, frame.FrameIndex, default, frame.BitDepth), false);
                while (colour.TryReadPacket(out EncodedVideoPacket packet)) colourPackets.Add(packet);
                while (alpha.TryReadPacket(out EncodedVideoPacket packet)) alphaPackets.Add(packet);
            }

            colour.Flush();
            alpha.Flush();
            while (colour.TryReadPacket(out EncodedVideoPacket packet)) colourPackets.Add(packet);
            while (alpha.TryReadPacket(out EncodedVideoPacket packet)) alphaPackets.Add(packet);
        }

        //Assert
        alphaPackets.Should().HaveCount(colourPackets.Count);
        for (int i = 0; i < colourPackets.Count; i++)
        {
            alphaPackets[i].Timestamp.Should().Be(colourPackets[i].Timestamp);
            alphaPackets[i].IsKeyFrame.Should().Be(colourPackets[i].IsKeyFrame);
            alphaPackets[i].Data.Span[1].Should().Be((byte)(100 + i));
        }
    }

    private sealed class SynchronousProgress : IProgress<VideoProgress>
    {
        private readonly List<VideoProgress> reports;

        public SynchronousProgress(List<VideoProgress> reports) => this.reports = reports;

        public void Report(VideoProgress value) => reports.Add(value);
    }
}
