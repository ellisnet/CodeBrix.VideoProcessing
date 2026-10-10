using System;

namespace CodeBrix.VideoProcessing.Core;

/// <summary>The base type of every failure a video processor engine reports through the contract.</summary>
/// <remarks>
/// Engines wrap their own failures in this type (or a subclass) and keep the original as
/// <see cref="Exception.InnerException" />, so a consumer can catch one type whichever engine is running.
/// </remarks>
public class VideoProcessingException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public VideoProcessingException()
        : base("A video processing operation failed.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public VideoProcessingException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the failure that caused it.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The engine's own failure.</param>
    public VideoProcessingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
