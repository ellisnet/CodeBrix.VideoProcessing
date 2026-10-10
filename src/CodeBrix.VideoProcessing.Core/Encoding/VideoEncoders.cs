using System;
using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Core.Encoding;

/// <summary>
/// The process-wide registry of video ENCODERS, keyed by codec id - the front door an encoder package knocks on, and
/// the place a consumer gets an encoder from.
/// </summary>
/// <remarks>
/// <para>
/// CodeBrix.VideoProcessing.Core contains no encoder. The application references an encoder package and registers it
/// once at start-up:
/// </para>
/// <code>
/// CodeBrixVideoEncodingAv1.Register();   // from CodeBrix.VideoEncoding.Av1 - registers the "av1" factory
/// </code>
/// <para>
/// A library that needs to encode then asks by codec id and never names a package:
/// </para>
/// <code>
/// IVideoEncoderFactory av1 = VideoEncoders.Get(VideoCodecIds.Av1);
/// using IVideoEncoder encoder = av1.Create(request);
/// </code>
/// <para>
/// Registration lasts for the process and is idempotent on the INSTANCE (registering an instance that is already
/// present does nothing, and does not move it). When several factories for the same codec id are registered, the
/// one registered most recently wins. Codec ids are compared ignoring case.
/// </para>
/// <para>
/// This registry is independent of <see cref="VideoProcessors" />: an encoder package needs no video processor
/// engine, and a video processor engine registers no encoders here.
/// </para>
/// <para>Every member is safe to call from any thread.</para>
/// </remarks>
public static class VideoEncoders
{
    private static readonly object Gate = new object();
    private static readonly List<Registration> Factories = new List<Registration>();

    /// <summary>
    /// Adds an encoder factory under its <see cref="IVideoEncoderFactory.CodecId" />, or does nothing if that same
    /// instance is already registered.
    /// </summary>
    /// <param name="factory">The factory to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory" /> is null.</exception>
    /// <exception cref="ArgumentException">The factory's codec id is null or blank.</exception>
    public static void Register(IVideoEncoderFactory factory)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        string codecId = factory.CodecId;
        if (string.IsNullOrWhiteSpace(codecId)) throw new ArgumentException("The encoder factory has no codec id.", nameof(factory));

        lock (Gate)
        {
            foreach (Registration registered in Factories)
            {
                if (ReferenceEquals(registered.Factory, factory)) return;
            }

            Factories.Add(new Registration(codecId, factory));
        }
    }

    /// <summary>True when an encoder for the codec is registered.</summary>
    /// <param name="codecId">The codec id, for example <see cref="VideoCodecIds.Av1" />.</param>
    /// <returns>True when <see cref="TryGet" /> would succeed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="codecId" /> is null.</exception>
    public static bool IsRegistered(string codecId) => TryGet(codecId, out IVideoEncoderFactory _);

    /// <summary>Gets the encoder factory for a codec without throwing when there is none.</summary>
    /// <param name="codecId">The codec id, for example <see cref="VideoCodecIds.Av1" />.</param>
    /// <param name="factory">The factory registered most recently for that codec, or null.</param>
    /// <returns>True when one is registered.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="codecId" /> is null.</exception>
    public static bool TryGet(string codecId, out IVideoEncoderFactory factory)
    {
        if (codecId == null) throw new ArgumentNullException(nameof(codecId));

        lock (Gate)
        {
            for (int i = Factories.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(Factories[i].CodecId, codecId, StringComparison.OrdinalIgnoreCase)) continue;
                factory = Factories[i].Factory;
                return true;
            }
        }

        factory = null;
        return false;
    }

    /// <summary>Gets the encoder factory for a codec.</summary>
    /// <param name="codecId">The codec id, for example <see cref="VideoCodecIds.Av1" />.</param>
    /// <returns>The factory registered most recently for that codec.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="codecId" /> is null.</exception>
    /// <exception cref="VideoEncoderNotRegisteredException">
    /// None is registered. The message names the codec id and, for AV1, the package and its Register call.
    /// </exception>
    public static IVideoEncoderFactory Get(string codecId)
    {
        if (TryGet(codecId, out IVideoEncoderFactory factory)) return factory;
        throw VideoEncoderNotRegisteredException.ForCodec(codecId);
    }

    /// <summary>Every registered factory, oldest registration first.</summary>
    public static IReadOnlyList<IVideoEncoderFactory> RegisteredFactories
    {
        get
        {
            lock (Gate)
            {
                var factories = new IVideoEncoderFactory[Factories.Count];
                for (int i = 0; i < factories.Length; i++) factories[i] = Factories[i].Factory;
                return factories;
            }
        }
    }

    // For tests only: encoders register for the life of the process, and nothing outside a test has a reason to
    // take one away again.
    internal static bool Unregister(IVideoEncoderFactory factory)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        lock (Gate)
        {
            for (int i = 0; i < Factories.Count; i++)
            {
                if (!ReferenceEquals(Factories[i].Factory, factory)) continue;
                Factories.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    internal static void Clear()
    {
        lock (Gate) Factories.Clear();
    }

    // The codec id is read once, at registration, so a factory whose CodecId changed later cannot confuse lookups.
    private readonly struct Registration
    {
        public Registration(string codecId, IVideoEncoderFactory factory)
        {
            CodecId = codecId;
            Factory = factory;
        }

        public string CodecId { get; }

        public IVideoEncoderFactory Factory { get; }
    }
}
