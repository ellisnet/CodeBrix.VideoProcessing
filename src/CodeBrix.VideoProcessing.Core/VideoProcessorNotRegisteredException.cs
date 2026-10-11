using System;

namespace CodeBrix.VideoProcessing.Core;

/// <summary>
/// <see cref="VideoProcessors.Current" /> was asked for an engine before the application registered one.
/// </summary>
public class VideoProcessorNotRegisteredException : VideoProcessingException
{
    /// <summary>The message used when no message is given: it names both engine packages and their Register calls.</summary>
    public const string DefaultMessage =
        "No video processor engine is registered. CodeBrix.VideoProcessing.Core is only the contract; the application " +
        "must reference an engine package and register it once at start-up: on Windows, Linux and macOS reference " +
        "CodeBrix.VideoProcessing.MitLicenseForever (package CodeBrix.VideoProcessing, the ffmpeg engine) and call " +
        "CodeBrixVideoProcessing.Register(); on Android reference the CodeBrix.VideoProcessing.Android.MitLicenseForever package and call " +
        "CodeBrixVideoProcessingAndroid.Register().";

    /// <summary>Creates the exception with <see cref="DefaultMessage" />.</summary>
    public VideoProcessorNotRegisteredException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public VideoProcessorNotRegisteredException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner exception.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The cause.</param>
    public VideoProcessorNotRegisteredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
