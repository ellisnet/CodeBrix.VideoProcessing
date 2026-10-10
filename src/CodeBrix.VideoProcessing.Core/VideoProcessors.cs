using System;
using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Core;

/// <summary>
/// The process-wide registry of video processor engines - the front door an engine package knocks on, and the
/// place a consumer gets its engine from.
/// </summary>
/// <remarks>
/// <para>
/// CodeBrix.VideoProcessing.Core is only the CONTRACT; it contains no engine. The application references an engine
/// package for the platform it runs on and registers it once at start-up:
/// </para>
/// <code>
/// CodeBrixVideoProcessing.Register();          // Windows, Linux, macOS - the ffmpeg engine
/// CodeBrixVideoProcessingAndroid.Register();   // Android - the platform-codec engine
/// </code>
/// <para>
/// A library that needs video processing then asks for <see cref="Current" /> and never names an engine:
/// </para>
/// <code>
/// IVideoProcessor processor = VideoProcessors.Current;
/// MediaInfo info = processor.Analyse(path);
/// </code>
/// <para>
/// Registration lasts for the process and is idempotent on the INSTANCE, so a start-up path that runs twice
/// registers once. When more than one engine is registered, <see cref="Current" /> is the one registered most
/// recently; registering an instance that is already present does not move it.
/// </para>
/// <para>Every member is safe to call from any thread.</para>
/// </remarks>
public static class VideoProcessors
{
    private static readonly object Gate = new object();
    private static readonly List<IVideoProcessor> Processors = new List<IVideoProcessor>();

    /// <summary>Adds an engine to the registry, or does nothing if that same instance is already registered.</summary>
    /// <param name="processor">The engine to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="processor" /> is null.</exception>
    public static void Register(IVideoProcessor processor)
    {
        if (processor == null) throw new ArgumentNullException(nameof(processor));

        lock (Gate)
        {
            foreach (IVideoProcessor registered in Processors)
            {
                if (ReferenceEquals(registered, processor)) return;
            }

            Processors.Add(processor);
        }
    }

    /// <summary>True when at least one engine is registered.</summary>
    public static bool IsRegistered
    {
        get
        {
            lock (Gate) return Processors.Count > 0;
        }
    }

    /// <summary>The engine to use: the one registered most recently.</summary>
    /// <exception cref="VideoProcessorNotRegisteredException">
    /// No engine is registered. The message names the two engine packages and their Register calls.
    /// </exception>
    public static IVideoProcessor Current
    {
        get
        {
            if (TryGetCurrent(out IVideoProcessor processor)) return processor;
            throw new VideoProcessorNotRegisteredException();
        }
    }

    /// <summary>Gets the engine to use without throwing when there is none.</summary>
    /// <param name="processor">The engine registered most recently, or null.</param>
    /// <returns>True when an engine is registered.</returns>
    public static bool TryGetCurrent(out IVideoProcessor processor)
    {
        lock (Gate)
        {
            processor = Processors.Count > 0 ? Processors[Processors.Count - 1] : null;
            return processor != null;
        }
    }

    /// <summary>Every registered engine, oldest registration first.</summary>
    public static IReadOnlyList<IVideoProcessor> RegisteredProcessors
    {
        get
        {
            lock (Gate) return Processors.ToArray();
        }
    }

    // For tests only: engines register for the life of the process, and nothing outside a test has a reason to
    // take one away again.
    internal static bool Unregister(IVideoProcessor processor)
    {
        if (processor == null) throw new ArgumentNullException(nameof(processor));

        lock (Gate)
        {
            for (int i = 0; i < Processors.Count; i++)
            {
                if (!ReferenceEquals(Processors[i], processor)) continue;
                Processors.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    internal static void Clear()
    {
        lock (Gate) Processors.Clear();
    }
}
