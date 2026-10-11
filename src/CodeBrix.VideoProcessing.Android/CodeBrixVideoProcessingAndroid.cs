using CodeBrix.VideoProcessing.Android.Engine;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core;

namespace CodeBrix.VideoProcessing.Android;

/// <summary>
/// The one call an Android application makes to use this package as the video processor ENGINE for
/// CodeBrix.VideoProcessing.Core.
/// </summary>
/// <remarks>
/// <para>
/// CodeBrix.VideoProcessing.Core is an engine-neutral contract; this package is its Android engine. An application
/// registers it once at start-up, and from then on any code - including libraries that reference only the Core
/// package - gets it from <see cref="VideoProcessors.Current" />:
/// </para>
/// <code>
/// CodeBrixVideoProcessingAndroid.Register();
/// IVideoProcessor engine = VideoProcessors.Current;
/// </code>
/// <para>
/// Registration is idempotent and thread-safe: a start-up path that runs twice registers once. There is deliberately
/// no module initializer - an explicit call is one line that the trimmer and the reader can both see.
/// </para>
/// </remarks>
public static class CodeBrixVideoProcessingAndroid
{
    private static readonly EngineRegistration Registration = new EngineRegistration(() => new AndroidVideoProcessor());

    /// <summary>The single engine instance <see cref="Register()" /> registers (created on first use).</summary>
    public static AndroidVideoProcessor Processor => (AndroidVideoProcessor)Registration.Processor;

    /// <summary>True once <see cref="Register()" /> has added the engine to the process-wide registry.</summary>
    public static bool IsRegistered => Registration.IsRegistered;

    /// <summary>Makes the Android engine <see cref="VideoProcessors.Current" /> for this process.</summary>
    /// <remarks>Safe to call more than once and from any thread. The first call reads the device's codec list once.</remarks>
    public static void Register() => Registration.Register();
}
