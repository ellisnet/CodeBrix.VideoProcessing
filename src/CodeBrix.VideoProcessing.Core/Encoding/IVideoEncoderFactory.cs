using System;

namespace CodeBrix.VideoProcessing.Core.Encoding;

/// <summary>
/// Makes encoders for ONE codec. An encoder package registers one shared factory with
/// <see cref="VideoEncoders.Register" />; a consumer finds it with <see cref="VideoEncoders.Get" /> and calls
/// <see cref="Create" /> once per stream to encode.
/// </summary>
/// <remarks>A factory is safe to use from several threads at once; the encoders it creates are not.</remarks>
public interface IVideoEncoderFactory
{
    /// <summary>The codec this factory encodes, as a lower-case id - for example <see cref="VideoCodecIds.Av1" />.</summary>
    string CodecId { get; }

    /// <summary>A short human-readable name, for logs and diagnostics (for example the underlying library's name).</summary>
    string DisplayName { get; }

    /// <summary>The frame layouts and depths the encoders accept, and their optional features.</summary>
    VideoEncoderCapabilities Capabilities { get; }

    /// <summary>Creates an encoder for one stream.</summary>
    /// <param name="request">The stream's settings. The encoder keeps its own copy, so the caller may change or reuse the request afterwards.</param>
    /// <returns>The encoder; dispose it when done.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The request fails <see cref="VideoEncoderRequest.Validate" />, or carries a value or codec option the encoder
    /// rejects; the message names it.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// The encoder does not accept the request's layout, depth or lossless setting (see
    /// <see cref="VideoEncoderCapabilities.ThrowIfUnsupported" />).
    /// </exception>
    /// <exception cref="VideoEncodingException">The underlying encoder failed to start.</exception>
    IVideoEncoder Create(VideoEncoderRequest request);
}
