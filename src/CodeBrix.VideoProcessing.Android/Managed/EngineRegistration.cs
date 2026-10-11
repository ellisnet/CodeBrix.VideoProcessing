using System;
using CodeBrix.VideoProcessing.Core;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// The idempotent, locked "register ONE shared engine instance" logic behind
/// <c>CodeBrixVideoProcessingAndroid.Register()</c>, separated from the Android engine so the host tests can prove it
/// with a fake. The instance is created on first use (creating the Android engine queries the device's codec list,
/// so it must not happen in a static initializer).
/// </summary>
internal sealed class EngineRegistration
{
    private readonly object gate = new object();
    private readonly Func<IVideoProcessor> factory;

    private IVideoProcessor instance;
    private bool registered;

    /// <summary>Creates the registration over a factory for the one engine instance.</summary>
    internal EngineRegistration(Func<IVideoProcessor> factory)
    {
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>The single engine instance (created on first use).</summary>
    internal IVideoProcessor Processor
    {
        get
        {
            lock (gate) return instance ??= factory();
        }
    }

    /// <summary>True once <see cref="Register" /> has added the instance to the registry.</summary>
    internal bool IsRegistered
    {
        get
        {
            lock (gate) return registered;
        }
    }

    /// <summary>Adds the instance to <see cref="VideoProcessors" />; later calls do nothing.</summary>
    internal void Register()
    {
        lock (gate)
        {
            if (registered) return;
            instance ??= factory();
            VideoProcessors.Register(instance);
            registered = true;
        }
    }
}
