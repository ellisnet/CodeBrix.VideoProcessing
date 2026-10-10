using System;

namespace CodeBrix.VideoProcessing.Core.Encoding;

/// <summary>
/// <see cref="VideoEncoders.Get" /> was asked for a codec no registered encoder package provides.
/// </summary>
public class VideoEncoderNotRegisteredException : VideoProcessingException
{
    /// <summary>The message used when no message is given.</summary>
    public const string DefaultMessage =
        "No video encoder is registered for the requested codec. CodeBrix.VideoProcessing.Core is only the contract; " +
        "the application must reference an encoder package and register it once at start-up (for AV1: reference " +
        "CodeBrix.VideoEncoding.Av1 and call CodeBrixVideoEncodingAv1.Register()).";

    /// <summary>Creates the exception with <see cref="DefaultMessage" />.</summary>
    public VideoEncoderNotRegisteredException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public VideoEncoderNotRegisteredException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner exception.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public VideoEncoderNotRegisteredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private VideoEncoderNotRegisteredException(string codecId, string message)
        : base(message)
    {
        CodecId = codecId;
    }

    /// <summary>The codec id that was asked for; null when the exception was not created by <see cref="ForCodec" />.</summary>
    public string CodecId { get; }

    /// <summary>
    /// Creates the exception for a codec id, with a message that names the id and - for a codec with a known
    /// encoder package - the package and its Register call.
    /// </summary>
    /// <param name="codecId">The codec id that was asked for.</param>
    /// <returns>The exception.</returns>
    public static VideoEncoderNotRegisteredException ForCodec(string codecId) => new VideoEncoderNotRegisteredException(codecId, MessageFor(codecId));

    /// <summary>The message <see cref="ForCodec" /> uses for a codec id.</summary>
    /// <param name="codecId">The codec id that was asked for.</param>
    /// <returns>The message.</returns>
    public static string MessageFor(string codecId)
    {
        string message =
            $"No video encoder is registered for codec '{codecId}'. CodeBrix.VideoProcessing.Core is only the contract; " +
            "the application must reference an encoder package for that codec and register it once at start-up";

        if (string.Equals(codecId, VideoCodecIds.Av1, StringComparison.OrdinalIgnoreCase))
            return message + ": for AV1 reference CodeBrix.VideoEncoding.Av1 and call CodeBrixVideoEncodingAv1.Register().";

        return message + " (each encoder package documents its own Register call).";
    }
}
