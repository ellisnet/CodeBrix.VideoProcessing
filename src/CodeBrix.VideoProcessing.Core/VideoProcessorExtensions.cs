using System;
using CodeBrix.VideoProcessing.Core.Tools;

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

    /// <summary>
    /// Gets an engine's external tools: true only when the engine reports
    /// <see cref="VideoProcessorCapabilities.ExternalTools" /> AND implements <see cref="IExternalToolHost" /> with
    /// non-null <see cref="IExternalToolHost.Tools" />.
    /// </summary>
    /// <param name="processor">The engine.</param>
    /// <param name="tools">The engine's tools, or null when it has none.</param>
    /// <returns>True when the engine offers external tools.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="processor" /> is null.</exception>
    public static bool TryGetExternalTools(this IVideoProcessor processor, out IExternalTools tools)
    {
        if (processor == null) throw new ArgumentNullException(nameof(processor));

        tools = null;
        if (!processor.Supports(VideoProcessorCapabilities.ExternalTools)) return false;
        if (processor is not IExternalToolHost host) return false;

        tools = host.Tools;
        return tools != null;
    }

    /// <summary>
    /// Gets an engine's external tools, or throws a <see cref="VideoProcessingException" /> naming the engine and the
    /// operation when it has none (see <see cref="TryGetExternalTools" />).
    /// </summary>
    /// <param name="processor">The engine.</param>
    /// <param name="operation">What needs the tools, for the message (for example "the ffmpeg command-line flavours").</param>
    /// <returns>The engine's tools.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="processor" /> is null.</exception>
    /// <exception cref="VideoProcessingException">The engine offers no external tools.</exception>
    public static IExternalTools RequireExternalTools(this IVideoProcessor processor, string operation)
    {
        if (processor.TryGetExternalTools(out IExternalTools tools)) return tools;

        throw new VideoProcessingException(
            $"The '{processor.Name}' video processor engine cannot run external tools, which {operation} needs: it lacks the " +
            $"{VideoProcessorCapabilities.ExternalTools} capability. Check processor.TryGetExternalTools(...) before calling, " +
            "or register an engine that offers them.");
    }
}
