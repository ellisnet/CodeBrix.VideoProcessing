using System;
using CodeBrix.VideoProcessing.Core.Encoding;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Core.Tests;

public class EncodedVideoPacketTests
{
    [Fact]
    public void constructor_holds_the_values()
    {
        //Act
        var packet = new EncodedVideoPacket(new byte[] { 1, 2, 3 }, TimeSpan.FromSeconds(2), 50, true);

        //Assert
        packet.Data.ToArray().Should().Equal(1, 2, 3);
        packet.Timestamp.Should().Be(TimeSpan.FromSeconds(2));
        packet.FrameIndex.Should().Be(50);
        packet.IsKeyFrame.Should().BeTrue();
        packet.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void default_packet_is_empty()
    {
        //Arrange
        EncodedVideoPacket packet = default;

        //Assert
        packet.IsEmpty.Should().BeTrue();
        packet.ToString().Should().Be("(empty packet)");
    }

    [Fact]
    public void constructor_rejects_empty_data()
    {
        //Act
        var exception = Assert.Throws<ArgumentException>(() => new EncodedVideoPacket(ReadOnlyMemory<byte>.Empty, TimeSpan.Zero, 0, true));

        //Assert
        exception.ParamName.Should().Be("data");
    }

    [Fact]
    public void constructor_rejects_a_negative_timestamp()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new EncodedVideoPacket(new byte[1], TimeSpan.FromTicks(-1), 0, false));

    [Fact]
    public void constructor_rejects_a_negative_frame_index()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new EncodedVideoPacket(new byte[1], TimeSpan.Zero, -1, false));

    [Fact]
    public void ToString_describes_the_packet()
    {
        //Arrange
        var packet = new EncodedVideoPacket(new byte[4], TimeSpan.Zero, 7, true);

        //Act
        string text = packet.ToString();

        //Assert
        text.Should().Contain("#7");
        text.Should().Contain("4 bytes");
        text.Should().Contain("key");
    }
}
