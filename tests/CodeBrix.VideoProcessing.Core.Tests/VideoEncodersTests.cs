using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Core.Encoding;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

// Every test that touches the process-wide encoder registry lives in this one class, so xUnit runs them one at a
// time and no other class can see a half-built registry.
public class VideoEncodersTests : IDisposable
{
    public VideoEncodersTests() => VideoEncoders.Clear();

    public void Dispose() => VideoEncoders.Clear();

    [Fact]
    public void Get_throws_for_av1_when_nothing_is_registered_and_names_the_package()
    {
        //Act
        var exception = Assert.Throws<VideoEncoderNotRegisteredException>(() => VideoEncoders.Get(VideoCodecIds.Av1));

        //Assert
        exception.CodecId.Should().Be("av1");
        exception.Message.Should().Contain("'av1'");
        exception.Message.Should().Contain("CodeBrix.VideoEncoding.Av1");
        exception.Message.Should().Contain("CodeBrixVideoEncodingAv1.Register()");
    }

    [Fact]
    public void Get_throws_for_another_codec_and_names_the_codec_id()
    {
        //Act
        var exception = Assert.Throws<VideoEncoderNotRegisteredException>(() => VideoEncoders.Get(VideoCodecIds.H264));

        //Assert
        exception.CodecId.Should().Be("h264");
        exception.Message.Should().Contain("'h264'");
        exception.Message.Should().NotContain("CodeBrixVideoEncodingAv1");
    }

    [Fact]
    public void not_registered_exception_is_a_video_processing_exception()
    {
        //Arrange
        var exception = new VideoEncoderNotRegisteredException();

        //Assert
        exception.Should().BeAssignableTo<VideoProcessingException>();
        exception.Message.Should().Be(VideoEncoderNotRegisteredException.DefaultMessage);
        exception.CodecId.Should().BeNull();
    }

    [Fact]
    public void encoding_exception_is_a_video_processing_exception_that_keeps_its_cause()
    {
        //Arrange
        var cause = new InvalidOperationException("native failure");

        //Act
        var exception = new VideoEncodingException("encode failed", cause);

        //Assert
        exception.Should().BeAssignableTo<VideoProcessingException>();
        exception.InnerException.Should().BeSameAs(cause);
        new VideoEncodingException().Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void nothing_is_registered_at_first()
    {
        //Assert
        VideoEncoders.IsRegistered(VideoCodecIds.Av1).Should().BeFalse();
        VideoEncoders.RegisteredFactories.Should().BeEmpty();
    }

    [Fact]
    public void Register_makes_the_factory_available_by_codec_id()
    {
        //Arrange
        var factory = new FakeVideoEncoderFactory();

        //Act
        VideoEncoders.Register(factory);

        //Assert
        VideoEncoders.IsRegistered(VideoCodecIds.Av1).Should().BeTrue();
        VideoEncoders.IsRegistered(VideoCodecIds.Hevc).Should().BeFalse();
        VideoEncoders.Get(VideoCodecIds.Av1).Should().BeSameAs(factory);
        VideoEncoders.TryGet(VideoCodecIds.Av1, out IVideoEncoderFactory found).Should().BeTrue();
        found.Should().BeSameAs(factory);
    }

    [Fact]
    public void codec_ids_are_compared_ignoring_case()
    {
        //Arrange
        VideoEncoders.Register(new FakeVideoEncoderFactory("AV1"));

        //Assert
        VideoEncoders.IsRegistered("av1").Should().BeTrue();
        VideoEncoders.IsRegistered("Av1").Should().BeTrue();
    }

    [Fact]
    public void Register_is_idempotent_per_instance()
    {
        //Arrange
        var factory = new FakeVideoEncoderFactory();

        //Act
        VideoEncoders.Register(factory);
        VideoEncoders.Register(factory);

        //Assert
        VideoEncoders.RegisteredFactories.Should().HaveCount(1);
    }

    [Fact]
    public void the_latest_registration_wins_for_a_codec_id()
    {
        //Arrange
        var first = new FakeVideoEncoderFactory(displayName: "first");
        var second = new FakeVideoEncoderFactory(displayName: "second");

        //Act
        VideoEncoders.Register(first);
        VideoEncoders.Register(second);
        VideoEncoders.Register(first);

        //Assert
        VideoEncoders.Get(VideoCodecIds.Av1).Should().BeSameAs(second);
        VideoEncoders.RegisteredFactories.Should().HaveCount(2);
        VideoEncoders.RegisteredFactories[0].Should().BeSameAs(first);
    }

    [Fact]
    public void factories_for_different_codecs_live_side_by_side()
    {
        //Arrange
        var av1 = new FakeVideoEncoderFactory(VideoCodecIds.Av1);
        var hevc = new FakeVideoEncoderFactory(VideoCodecIds.Hevc);

        //Act
        VideoEncoders.Register(av1);
        VideoEncoders.Register(hevc);

        //Assert
        VideoEncoders.Get(VideoCodecIds.Av1).Should().BeSameAs(av1);
        VideoEncoders.Get(VideoCodecIds.Hevc).Should().BeSameAs(hevc);
    }

    [Fact]
    public void Register_rejects_null_and_factories_without_a_codec_id()
    {
        //Act + Assert
        Assert.Throws<ArgumentNullException>(() => VideoEncoders.Register(null));
        Assert.Throws<ArgumentException>(() => VideoEncoders.Register(new FakeVideoEncoderFactory(" ")));
        Assert.Throws<ArgumentException>(() => VideoEncoders.Register(new FakeVideoEncoderFactory(null)));
    }

    [Fact]
    public void lookups_reject_a_null_codec_id()
    {
        //Act + Assert
        Assert.Throws<ArgumentNullException>(() => VideoEncoders.Get(null));
        Assert.Throws<ArgumentNullException>(() => VideoEncoders.IsRegistered(null));
        Assert.Throws<ArgumentNullException>(() => VideoEncoders.TryGet(null, out IVideoEncoderFactory _));
    }

    [Fact]
    public void TryGet_returns_false_and_null_when_missing()
    {
        //Act
        bool found = VideoEncoders.TryGet(VideoCodecIds.Av1, out IVideoEncoderFactory factory);

        //Assert
        found.Should().BeFalse();
        factory.Should().BeNull();
    }

    [Fact]
    public void Unregister_falls_back_to_the_previous_factory()
    {
        //Arrange
        var first = new FakeVideoEncoderFactory(displayName: "first");
        var second = new FakeVideoEncoderFactory(displayName: "second");
        VideoEncoders.Register(first);
        VideoEncoders.Register(second);

        //Act
        bool removed = VideoEncoders.Unregister(second);

        //Assert
        removed.Should().BeTrue();
        VideoEncoders.Get(VideoCodecIds.Av1).Should().BeSameAs(first);
        VideoEncoders.Unregister(second).Should().BeFalse();
    }

    [Fact]
    public void Register_and_Get_are_safe_from_many_threads()
    {
        //Arrange
        var shared = new FakeVideoEncoderFactory();
        var others = new List<FakeVideoEncoderFactory>();
        for (int i = 0; i < 16; i++) others.Add(new FakeVideoEncoderFactory(VideoCodecIds.Hevc, "hevc " + i));

        //Act
        Parallel.For(0, 64, i =>
        {
            VideoEncoders.Register(shared);
            VideoEncoders.Register(others[i % others.Count]);
            VideoEncoders.Get(VideoCodecIds.Av1).Should().BeSameAs(shared);
            VideoEncoders.TryGet(VideoCodecIds.Hevc, out IVideoEncoderFactory _).Should().BeTrue();
        });

        //Assert
        VideoEncoders.RegisteredFactories.Should().HaveCount(17);
    }

    [Fact]
    public void a_registered_encoder_is_found_by_codec_id_and_encodes_frames()
    {
        //Arrange
        VideoEncoders.Register(new FakeVideoEncoderFactory());
        var request = new VideoEncoderRequest { Width = 2, Height = 2, PixelLayout = VideoPixelLayout.Gray, FrameRate = new FrameRate(30, 1) };
        var packets = new List<EncodedVideoPacket>();

        //Act
        using (IVideoEncoder encoder = VideoEncoders.Get(VideoCodecIds.Av1).Create(request))
        {
            for (int i = 0; i < 3; i++)
            {
                encoder.Encode(VideoFrameData.CreateGray(2, 2, VideoPlane.FromArray(new byte[] { (byte)i, 0, 0, 0 }, 2, 2), TimeSpan.Zero, 0), false);
                while (encoder.TryReadPacket(out EncodedVideoPacket packet)) packets.Add(packet);
            }

            encoder.Flush();
            while (encoder.TryReadPacket(out EncodedVideoPacket packet)) packets.Add(packet);
        }

        //Assert
        packets.Should().HaveCount(3);
        packets[2].Timestamp.Should().Be(new FrameRate(30, 1).TimestampOf(2));
    }
}
