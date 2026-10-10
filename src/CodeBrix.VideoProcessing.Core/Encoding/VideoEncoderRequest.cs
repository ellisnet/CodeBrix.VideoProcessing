using System;
using System.Collections.Generic;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;

namespace CodeBrix.VideoProcessing.Core.Encoding;

/// <summary>
/// The settings of one video ELEMENTARY stream to encode: what frames will come in and how to compress them. Given to
/// <see cref="IVideoEncoderFactory.Create" />.
/// </summary>
/// <remarks>
/// <para>
/// The request describes the frames and the codec only. It knows nothing about containers, alpha streams, RGBA
/// conversion or file formats: an encoder turns frames into packets, and what the packets are stored in is the
/// caller's business. To encode an alpha channel as its own stream, the caller creates a second encoder (typically
/// <see cref="VideoPixelLayout.Gray" />, <see cref="VideoColorRange.Full" />) and feeds it the alpha planes.
/// </para>
/// <para>
/// The colour fields are TAGS written into the stream's headers; the encoder converts nothing.
/// <see cref="VideoColorPrimaries.Unspecified" /> (and the other Unspecified values) leave a field untagged.
/// </para>
/// </remarks>
public sealed class VideoEncoderRequest
{
    /// <summary>Width in pixels; greater than zero. Every frame passed to <see cref="IVideoEncoder.Encode" /> must have it.</summary>
    public int Width { get; set; }

    /// <summary>Height in pixels; greater than zero. Every frame passed to <see cref="IVideoEncoder.Encode" /> must have it.</summary>
    public int Height { get; set; }

    /// <summary>The layout of the frames the caller will pass. Default <see cref="VideoPixelLayout.I420" />.</summary>
    public VideoPixelLayout PixelLayout { get; set; } = VideoPixelLayout.I420;

    /// <summary>The bits per sample of the frames the caller will pass, and of the stream: 8 (the default), 10 or 12. RGBA is 8 only.</summary>
    public int BitDepth { get; set; } = 8;

    /// <summary>
    /// The CONSTANT frame rate; required. The encoder stamps the Nth frame it is given at
    /// <c>FrameRate.TimestampOf(N)</c> (see <see cref="EncodedVideoPacket.Timestamp" />).
    /// </summary>
    public FrameRate FrameRate { get; set; }

    /// <summary>The colour primaries tag. Default <see cref="VideoColorPrimaries.Bt709" />.</summary>
    public VideoColorPrimaries ColorPrimaries { get; set; } = VideoColorPrimaries.Bt709;

    /// <summary>The transfer characteristics tag. Default <see cref="VideoColorTransfer.Bt709" />.</summary>
    public VideoColorTransfer ColorTransfer { get; set; } = VideoColorTransfer.Bt709;

    /// <summary>The YUV matrix tag. Default <see cref="VideoColorMatrix.Bt709" />.</summary>
    public VideoColorMatrix ColorMatrix { get; set; } = VideoColorMatrix.Bt709;

    /// <summary>
    /// The sample range tag. Default <see cref="VideoColorRange.Limited" />. Use <see cref="VideoColorRange.Full" />
    /// for a stream that carries an alpha channel as luma.
    /// </summary>
    public VideoColorRange ColorRange { get; set; } = VideoColorRange.Limited;

    /// <summary>
    /// The quality / bitrate request (the same <see cref="VideoQuality" /> the standard writer uses). Default: the
    /// encoder's choice. The 0..51 constant-quality scale is mapped onto the codec's own scale by the encoder, as
    /// its documentation describes; a codec-specific <see cref="CodecOptions" /> entry may set the native value
    /// exactly. Must stay at the default when <see cref="Lossless" /> is set.
    /// </summary>
    public VideoQuality Quality { get; set; }

    /// <summary>
    /// Encode mathematically lossless. Needs <see cref="VideoEncoderCapabilities.SupportsLossless" />; replaces
    /// <see cref="Quality" />, which must then be left at the default. Default false.
    /// </summary>
    public bool Lossless { get; set; }

    /// <summary>The number of worker threads the encoder may use; 0 (the default) lets the encoder choose.</summary>
    public int Threads { get; set; }

    /// <summary>
    /// The maximum distance between keyframes, in frames; 0 (the default) lets the encoder choose. 1 makes every
    /// frame a keyframe.
    /// </summary>
    public int MaxKeyFrameInterval { get; set; }

    /// <summary>
    /// The CODEC-SPECIFIC speed / effort preset (for example libaom's cpu-used 0..9, where higher is faster); 0 (the
    /// default) lets the encoder choose. Its meaning and range are documented by each encoder package, which
    /// rejects values it does not accept.
    /// </summary>
    public int Speed { get; set; }

    /// <summary>
    /// Codec-specific settings by name, passed through to the encoder untouched (for example an AV1 encoder's
    /// tuning knobs), so this contract never has to enumerate any one codec's options. Never null; empty by
    /// default; keys are compared ordinally. An encoder rejects names and values it does not know with an
    /// <see cref="ArgumentException" />.
    /// </summary>
    public IDictionary<string, string> CodecOptions { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Checks the request.</summary>
    /// <exception cref="ArgumentException">A value is missing or out of range; the message names it.</exception>
    public void Validate()
    {
        if (Width <= 0) throw new ArgumentOutOfRangeException(nameof(Width), Width, "The width must be greater than zero.");
        if (Height <= 0) throw new ArgumentOutOfRangeException(nameof(Height), Height, "The height must be greater than zero.");
        if (!Enum.IsDefined(PixelLayout)) throw new ArgumentOutOfRangeException(nameof(PixelLayout), PixelLayout, "Unknown pixel layout.");
        if (BitDepth != 8 && BitDepth != 10 && BitDepth != 12)
            throw new ArgumentOutOfRangeException(nameof(BitDepth), BitDepth, "The bit depth must be 8, 10 or 12.");
        if (BitDepth > 8 && PixelLayout == VideoPixelLayout.Rgba)
            throw new ArgumentOutOfRangeException(nameof(BitDepth), BitDepth, "RGBA frames are always 8 bits.");
        if (!FrameRate.IsSpecified) throw new ArgumentException("The frame rate is required.", nameof(FrameRate));

        if (!Enum.IsDefined(ColorPrimaries)) throw new ArgumentOutOfRangeException(nameof(ColorPrimaries), ColorPrimaries, "Unknown colour primaries.");
        if (!Enum.IsDefined(ColorTransfer)) throw new ArgumentOutOfRangeException(nameof(ColorTransfer), ColorTransfer, "Unknown colour transfer.");
        if (!Enum.IsDefined(ColorMatrix)) throw new ArgumentOutOfRangeException(nameof(ColorMatrix), ColorMatrix, "Unknown colour matrix.");
        if (!Enum.IsDefined(ColorRange)) throw new ArgumentOutOfRangeException(nameof(ColorRange), ColorRange, "Unknown colour range.");

        if (!Enum.IsDefined(Quality.Mode)) throw new ArgumentOutOfRangeException(nameof(Quality), Quality, "Unknown quality mode.");
        if (Quality.Mode == VideoQualityMode.ConstantQuality &&
            (Quality.QualityLevel < VideoQuality.BestQualityLevel || Quality.QualityLevel > VideoQuality.WorstQualityLevel))
            throw new ArgumentOutOfRangeException(nameof(Quality), Quality, "The quality level must be between 0 and 51.");
        if (Lossless && Quality.Mode != VideoQualityMode.EngineDefault)
            throw new ArgumentException("A lossless request takes no quality or bitrate; leave Quality at VideoQuality.EngineDefault.", nameof(Quality));

        if (Threads < 0) throw new ArgumentOutOfRangeException(nameof(Threads), Threads, "The thread count cannot be negative (0 = encoder default).");
        if (MaxKeyFrameInterval < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxKeyFrameInterval), MaxKeyFrameInterval, "The keyframe interval cannot be negative (0 = encoder default).");
        if (Speed < 0) throw new ArgumentOutOfRangeException(nameof(Speed), Speed, "The speed cannot be negative (0 = encoder default).");

        if (CodecOptions == null) throw new ArgumentException("The codec options cannot be null; leave the dictionary empty for none.", nameof(CodecOptions));
        foreach (KeyValuePair<string, string> option in CodecOptions)
        {
            if (string.IsNullOrWhiteSpace(option.Key)) throw new ArgumentException("A codec option name cannot be empty.", nameof(CodecOptions));
            if (option.Value == null) throw new ArgumentException($"The codec option '{option.Key}' has no value.", nameof(CodecOptions));
        }
    }

    /// <summary>Makes a copy; <see cref="CodecOptions" /> is copied into a new dictionary, so the copy is independent.</summary>
    /// <returns>The copy.</returns>
    public VideoEncoderRequest Clone()
    {
        var copy = (VideoEncoderRequest)MemberwiseClone();
        copy.CodecOptions = CodecOptions == null ? null : new Dictionary<string, string>(CodecOptions, StringComparer.Ordinal);
        return copy;
    }
}
