using System;
using System.Buffers.Binary;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>Conversions between the PCM encodings Android's audio codecs use and interleaved 32-bit float.</summary>
internal static class PcmConversion
{
    /// <summary>Bytes per sample of an AudioFormat.ENCODING_PCM_* value (16-bit when unknown).</summary>
    internal static int BytesPerSample(int pcmEncoding) => pcmEncoding switch
    {
        MediaFormatKeys.PcmEncoding8Bit => 1,
        MediaFormatKeys.PcmEncoding24BitPacked => 3,
        MediaFormatKeys.PcmEncoding32Bit => 4,
        MediaFormatKeys.PcmEncodingFloat => 4,
        _ => 2
    };

    /// <summary>Decodes little-endian PCM bytes of the given encoding to floats; returns the number of samples written.</summary>
    /// <param name="source">Whole samples (a trailing partial sample is ignored).</param>
    /// <param name="pcmEncoding">AudioFormat.ENCODING_PCM_* (16-bit when unknown).</param>
    /// <param name="destination">At least source.Length / BytesPerSample floats.</param>
    internal static int ToFloat(ReadOnlySpan<byte> source, int pcmEncoding, Span<float> destination)
    {
        int bytes = BytesPerSample(pcmEncoding);
        int count = source.Length / bytes;
        switch (pcmEncoding)
        {
            case MediaFormatKeys.PcmEncodingFloat:
                for (int i = 0; i < count; i++) destination[i] = BinaryPrimitives.ReadSingleLittleEndian(source.Slice(i * 4, 4));
                break;
            case MediaFormatKeys.PcmEncoding8Bit:
                for (int i = 0; i < count; i++) destination[i] = (source[i] - 128) / 128f;
                break;
            case MediaFormatKeys.PcmEncoding24BitPacked:
                for (int i = 0; i < count; i++)
                {
                    int o = i * 3;
                    int value = source[o] | (source[o + 1] << 8) | ((sbyte)source[o + 2] << 16);
                    destination[i] = value / 8388608f;
                }

                break;
            case MediaFormatKeys.PcmEncoding32Bit:
                for (int i = 0; i < count; i++) destination[i] = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(i * 4, 4)) / 2147483648f;
                break;
            default:
                for (int i = 0; i < count; i++) destination[i] = BinaryPrimitives.ReadInt16LittleEndian(source.Slice(i * 2, 2)) / 32768f;
                break;
        }

        return count;
    }

    /// <summary>Encodes floats to little-endian signed 16-bit PCM, clamping to -1..1 and rounding to nearest.</summary>
    /// <param name="source">The samples.</param>
    /// <param name="destination">At least 2 * source.Length bytes.</param>
    internal static void ToPcm16(ReadOnlySpan<float> source, Span<byte> destination)
    {
        for (int i = 0; i < source.Length; i++)
        {
            float value = source[i];
            if (float.IsNaN(value)) value = 0;
            double scaled = Math.Round(Math.Clamp(value, -1f, 1f) * 32767.0, MidpointRounding.AwayFromZero);
            BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(i * 2, 2), (short)scaled);
        }
    }
}
