using System;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Engine;

namespace CodeBrix.VideoProcessing;

/// <summary>
/// The one call an application makes to use this package as the video processor ENGINE for
/// CodeBrix.VideoProcessing.Core, on Windows, Linux and macOS.
/// </summary>
/// <remarks>
/// <para>
/// CodeBrix.VideoProcessing.Core is an engine-neutral contract; this package is its ffmpeg engine. An application
/// registers it once at start-up, and from then on any code - including libraries that reference only the Core
/// package - gets it from <see cref="VideoProcessors.Current" />:
/// </para>
/// <code>
/// CodeBrixVideoProcessing.Register();
/// IVideoProcessor engine = VideoProcessors.Current;
/// </code>
/// <para>
/// Registration is idempotent: a start-up path that runs twice registers once. There is deliberately no module
/// initializer - an explicit call is one line that the trimmer and the reader can both see.
/// </para>
/// <para>
/// NOT FOR ANDROID: there is no ffmpeg executable to launch there. On Android, <see cref="Register()" /> throws
/// <see cref="PlatformNotSupportedException" />; register the CodeBrix.VideoProcessing.Android engine instead.
/// </para>
/// </remarks>
public static class CodeBrixVideoProcessing
{
    private static readonly object Gate = new object();
    private static readonly FFmpegVideoProcessor Instance = new FFmpegVideoProcessor();

    private static bool registered;

    /// <summary>The single engine instance <see cref="Register()" /> registers. It uses <see cref="GlobalFFOptions.Current" />.</summary>
    public static FFmpegVideoProcessor Processor => Instance;

    /// <summary>True once <see cref="Register()" /> has added the engine to the process-wide registry.</summary>
    public static bool IsRegistered
    {
        get
        {
            lock (Gate) return registered;
        }
    }

    /// <summary>Makes the ffmpeg engine <see cref="VideoProcessors.Current" /> for this process.</summary>
    /// <remarks>
    /// Safe to call more than once and from any thread. It does not check for the ffmpeg executables - the first
    /// operation that needs them reports their absence with a <see cref="VideoProcessingException" />.
    /// </remarks>
    /// <exception cref="PlatformNotSupportedException">The process is running on Android.</exception>
    public static void Register()
    {
        if (OperatingSystem.IsAndroid())
        {
            throw new PlatformNotSupportedException(
                "CodeBrix.VideoProcessing drives the external ffmpeg executable, which does not exist on Android. " +
                "Reference the CodeBrix.VideoProcessing.Android.MitLicenseForever package and call CodeBrixVideoProcessingAndroid.Register() instead.");
        }

        lock (Gate)
        {
            if (registered) return;

            VideoProcessors.Register(Instance);
            registered = true;
        }
    }
}
