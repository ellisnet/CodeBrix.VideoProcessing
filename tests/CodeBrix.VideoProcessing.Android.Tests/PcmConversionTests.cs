using System;
using CodeBrix.VideoProcessing.Android.Managed;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class PcmConversionTests
{
    [Fact]
    public void pcm16_round_trips_and_clamps()
    {
        //Arrange
        float[] samples = { 0f, 0.5f, -0.5f, 1f, -1f, 2f, -2f, float.NaN };
        byte[] bytes = new byte[samples.Length * 2];
        float[] back = new float[samples.Length];

        //Act
        PcmConversion.ToPcm16(samples, bytes);
        int count = PcmConversion.ToFloat(bytes, MediaFormatKeys.PcmEncoding16Bit, back);

        //Assert
        count.Should().Be(samples.Length);
        back[0].Should().Be(0f);
        back[1].Should().BeApproximately(0.5f, 0.0001f);
        back[2].Should().BeApproximately(-0.5f, 0.0001f);
        back[3].Should().BeApproximately(32767f / 32768f, 0.00001f);
        back[5].Should().Be(back[3]);
        back[6].Should().Be(back[4]);
        back[7].Should().Be(0f);
    }

    [Theory]
    [InlineData(MediaFormatKeys.PcmEncoding8Bit, 1)]
    [InlineData(MediaFormatKeys.PcmEncoding16Bit, 2)]
    [InlineData(MediaFormatKeys.PcmEncoding24BitPacked, 3)]
    [InlineData(MediaFormatKeys.PcmEncoding32Bit, 4)]
    [InlineData(MediaFormatKeys.PcmEncodingFloat, 4)]
    [InlineData(-1, 2)]
    public void bytes_per_sample_follow_the_encoding(int encoding, int bytes)
        => PcmConversion.BytesPerSample(encoding).Should().Be(bytes);

    [Fact]
    public void other_encodings_decode_to_the_expected_values()
    {
        //Arrange
        float[] destination = new float[2];

        //Act + Assert
        PcmConversion.ToFloat(new byte[] { 0, 255 }, MediaFormatKeys.PcmEncoding8Bit, destination);
        destination[0].Should().Be(-1f);
        destination[1].Should().BeApproximately(127f / 128f, 0.0001f);

        PcmConversion.ToFloat(new byte[] { 0, 0, 0x40, 0, 0, 0xC0 }, MediaFormatKeys.PcmEncoding24BitPacked, destination);
        destination[0].Should().Be(0.5f);
        destination[1].Should().Be(-0.5f);

        byte[] floats = new byte[8];
        BitConverter.TryWriteBytes(floats.AsSpan(0, 4), 0.25f);
        BitConverter.TryWriteBytes(floats.AsSpan(4, 4), -0.75f);
        PcmConversion.ToFloat(floats, MediaFormatKeys.PcmEncodingFloat, destination);
        destination[0].Should().Be(0.25f);
        destination[1].Should().Be(-0.75f);
    }
}
