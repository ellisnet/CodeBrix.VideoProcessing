using System;

namespace CodeBrix.VideoProcessing.Core;

/// <summary>Small helpers for asking an engine what it can do.</summary>
public static class VideoProcessorExtensions
{
    /// <summary>Reports whether an engine has EVERY one of the given capabilities.</summary>
    /// <param name="processor">The engine.</param>
    /// <param name="capabilities">One or more capability flags.</param>
    /// <returns>True when all of the flags are set on <see cref="IVideoProcessor.Capabilities" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="processor" /> is null.</exception>
    public static bool Supports(this IVideoProcessor processor, VideoProcessorCapabilities capabilities)
    {
        if (processor == null) throw new ArgumentNullException(nameof(processor));
        return (processor.Capabilities & capabilities) == capabilities;
    }

    /// <summary>
    /// Throws the contract's standard <see cref="NotSupportedException" /> unless an engine has every one of the
    /// given capabilities. Engine implementers call this at the top of each optional operation.
    /// </summary>
    /// <param name="processor">The engine.</param>
    /// <param name="capabilities">One or more capability flags.</param>
    /// <param name="operation">What was attempted, for the message (for example "TrimFile").</param>
    /// <exception cref="ArgumentNullException"><paramref name="processor" /> is null.</exception>
    /// <exception cref="NotSupportedException">A flag is missing.</exception>
    public static void RequireCapability(this IVideoProcessor processor, VideoProcessorCapabilities capabilities, string operation)
    {
        if (processor.Supports(capabilities)) return;

        VideoProcessorCapabilities missing = capabilities & ~processor.Capabilities;
        throw new NotSupportedException(
            $"The '{processor.Name}' video processor engine does not support {operation}: it lacks the {missing} capability. " +
            "Check IVideoProcessor.Capabilities (or processor.Supports(...)) before calling.");
    }
}
