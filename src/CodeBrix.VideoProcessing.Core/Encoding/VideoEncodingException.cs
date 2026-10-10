using System;

namespace CodeBrix.VideoProcessing.Core.Encoding;

/// <summary>An encoder (<see cref="IVideoEncoder" /> or <see cref="IVideoEncoderFactory" />) failed.</summary>
/// <remarks>
/// Encoder packages wrap their own failures in this type and keep the original as
/// <see cref="Exception.InnerException" />. It derives from <see cref="VideoProcessingException" />, so catching that
/// catches encoder failures too.
/// </remarks>
public class VideoEncodingException : VideoProcessingException
{
    /// <summary>Creates the exception with a default message.</summary>
    public VideoEncodingException()
        : base("A video encoding operation failed.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public VideoEncodingException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the failure that caused it.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The encoder's own failure.</param>
    public VideoEncodingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
