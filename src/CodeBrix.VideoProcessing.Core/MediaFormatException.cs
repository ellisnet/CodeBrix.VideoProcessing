using System;

namespace CodeBrix.VideoProcessing.Core;

/// <summary>
/// An input could not be read as media, or lacks what the operation needs (for example a video stream).
/// </summary>
public class MediaFormatException : VideoProcessingException
{
    /// <summary>Creates the exception with a default message.</summary>
    public MediaFormatException()
        : base("The input is not media the video processor engine can read.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public MediaFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the failure that caused it.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The engine's own failure.</param>
    public MediaFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
