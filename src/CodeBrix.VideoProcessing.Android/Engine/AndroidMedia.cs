using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core;
using global::Android.Media;
using ByteBuffer = global::Java.Nio.ByteBuffer;
using JavaThrowable = global::Java.Lang.Throwable;

namespace CodeBrix.VideoProcessing.Android.Engine;

/// <summary>Small helpers over the Android media bindings: reading MediaFormat values, opening an extractor, copying between Java buffers and managed arrays, choosing codecs and wrapping platform failures.</summary>
internal static class AndroidMedia
{
    /// <summary>How long a codec loop may go without any progress before the engine gives up (a hung or crashed codec process).</summary>
    internal static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(15);

    /// <summary>The dequeue timeout used inside codec loops, in microseconds.</summary>
    internal const long DequeueTimeoutMicroseconds = 10_000;

    /// <summary>Opens a MediaExtractor on a file, mapping a missing file and an unreadable one to the contract's exceptions.</summary>
    internal static MediaExtractor OpenExtractor(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("The path is required.", nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException("The media file does not exist.", path);

        var extractor = new MediaExtractor();
        try
        {
            extractor.SetDataSource(path);
            return extractor;
        }
        catch (JavaThrowable exception)
        {
            ReleaseQuietly(extractor);
            throw new MediaFormatException($"Android's MediaExtractor cannot read '{path}': {exception.Message}", exception);
        }
    }

    /// <summary>Copies one track's MediaFormat into a <see cref="TrackDescription" />.</summary>
    internal static TrackDescription Describe(MediaFormat format, int index)
    {
        return new TrackDescription
        {
            Index = index,
            Mime = GetString(format, MediaFormatKeys.Mime),
            Width = GetInt(format, MediaFormatKeys.Width),
            Height = GetInt(format, MediaFormatKeys.Height),
            DurationMicroseconds = Math.Max(0, GetLong(format, MediaFormatKeys.Duration)),
            FrameRateValue = Math.Max(0, GetDouble(format, MediaFormatKeys.FrameRate)),
            Rotation = GetInt(format, MediaFormatKeys.Rotation),
            ColorRange = GetInt(format, MediaFormatKeys.ColorRange),
            ColorStandard = GetInt(format, MediaFormatKeys.ColorStandard),
            ColorTransfer = GetInt(format, MediaFormatKeys.ColorTransfer),
            Profile = GetInt(format, MediaFormatKeys.Profile),
            SampleRate = GetInt(format, MediaFormatKeys.SampleRate),
            ChannelCount = GetInt(format, MediaFormatKeys.ChannelCount),
            PcmEncoding = GetInt(format, MediaFormatKeys.PcmEncoding),
            Language = GetString(format, MediaFormatKeys.Language),
            Csd0 = GetBytes(format, MediaFormatKeys.Csd0),
            Csd1 = GetBytes(format, MediaFormatKeys.Csd1)
        };
    }

    /// <summary>
    /// Every track of an open extractor, with the managed MP4 reader's exact frame rate and frame count attached to
    /// the video tracks of an MP4 / MOV file (matched by order among video tracks, and only when the durations agree).
    /// </summary>
    internal static List<TrackDescription> ReadTracks(MediaExtractor extractor, string path)
    {
        var tracks = new List<TrackDescription>();
        for (int i = 0; i < extractor.TrackCount; i++) tracks.Add(Describe(extractor.GetTrackFormat(i), i));

        List<Mp4TrackTimingInfo> timing = Mp4TrackTiming.Read(path);
        var videoTiming = timing.FindAll(t => t.Handler == "vide");
        int ordinal = 0;
        foreach (TrackDescription track in tracks)
        {
            if (!track.IsVideo) continue;
            if (ordinal >= videoTiming.Count) break;
            Mp4TrackTimingInfo info = videoTiming[ordinal++];
            if (info.Timescale <= 0) continue;

            double containerSeconds = (double)info.Duration / info.Timescale;
            double trackSeconds = track.DurationMicroseconds / 1_000_000.0;
            bool agrees = trackSeconds <= 0 || containerSeconds <= 0 || Math.Abs(containerSeconds - trackSeconds) <= Math.Max(0.1, trackSeconds * 0.1);
            if (!agrees) continue;

            track.ContainerFrameRate = info.FrameRate;
            track.ContainerSampleCount = info.SampleCount;
        }

        return tracks;
    }

    /// <summary>An integer value (an int, long, float or numeric string in the format), or the fallback.</summary>
    internal static int GetInt(MediaFormat format, string key, int fallback = -1)
    {
        long value = GetLong(format, key, fallback);
        return value > int.MaxValue || value < int.MinValue ? fallback : (int)value;
    }

    /// <summary>A long value, or the fallback.</summary>
    internal static long GetLong(MediaFormat format, string key, long fallback = -1)
    {
        try
        {
            if (!format.ContainsKey(key)) return fallback;
            switch ((int)format.GetValueTypeForKey(key))
            {
                case 1: return format.GetInteger(key);
                case 2: return format.GetLong(key);
                case 3: return (long)Math.Round(format.GetFloat(key));
                case 4: return long.TryParse(format.GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : fallback;
                default: return fallback;
            }
        }
        catch (JavaThrowable)
        {
            return fallback;
        }
    }

    /// <summary>A floating-point value (int, long or float in the format), or the fallback.</summary>
    internal static double GetDouble(MediaFormat format, string key, double fallback = 0)
    {
        try
        {
            if (!format.ContainsKey(key)) return fallback;
            switch ((int)format.GetValueTypeForKey(key))
            {
                case 1: return format.GetInteger(key);
                case 2: return format.GetLong(key);
                case 3: return format.GetFloat(key);
                case 4: return double.TryParse(format.GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : fallback;
                default: return fallback;
            }
        }
        catch (JavaThrowable)
        {
            return fallback;
        }
    }

    /// <summary>A string value, or null.</summary>
    internal static string GetString(MediaFormat format, string key)
    {
        try
        {
            return format.ContainsKey(key) && (int)format.GetValueTypeForKey(key) == 4 ? format.GetString(key) : null;
        }
        catch (JavaThrowable)
        {
            return null;
        }
    }

    /// <summary>A byte-buffer value copied out, or null.</summary>
    internal static byte[] GetBytes(MediaFormat format, string key)
    {
        try
        {
            if (!format.ContainsKey(key) || (int)format.GetValueTypeForKey(key) != 5) return null;
            ByteBuffer buffer = format.GetByteBuffer(key);
            if (buffer == null) return null;
            ByteBuffer view = buffer.Duplicate();
            view.Rewind();
            byte[] bytes = new byte[view.Remaining()];
            view.Get(bytes);
            return bytes;
        }
        catch (JavaThrowable)
        {
            return null;
        }
    }

    /// <summary>Copies bytes out of a Java buffer at an ABSOLUTE index (position and limit are not used or changed for a direct buffer).</summary>
    internal static void CopyFrom(ByteBuffer buffer, int index, byte[] destination, int destinationOffset, int count)
    {
        if (count <= 0) return;
        IntPtr address = buffer.IsDirect ? buffer.GetDirectBufferAddress() : IntPtr.Zero;
        if (address != IntPtr.Zero)
        {
            Marshal.Copy(IntPtr.Add(address, index), destination, destinationOffset, count);
            return;
        }

        ByteBuffer view = buffer.Duplicate();
        view.Position(index);
        view.Get(destination, destinationOffset, count);
    }

    /// <summary>Copies bytes into a Java buffer at an ABSOLUTE index.</summary>
    internal static void CopyTo(byte[] source, int sourceOffset, int count, ByteBuffer buffer, int index)
    {
        if (count <= 0) return;
        IntPtr address = buffer.IsDirect ? buffer.GetDirectBufferAddress() : IntPtr.Zero;
        if (address != IntPtr.Zero)
        {
            Marshal.Copy(source, sourceOffset, IntPtr.Add(address, index), count);
            return;
        }

        ByteBuffer view = buffer.Duplicate();
        view.Position(index);
        view.Put(source, sourceOffset, count);
    }

    /// <summary>The name of a decoder for a track format, or null.</summary>
    internal static string FindDecoder(MediaFormat format)
    {
        try
        {
            return new MediaCodecList(MediaCodecListKind.RegularCodecs).FindDecoderForFormat(format);
        }
        catch (JavaThrowable)
        {
            return null;
        }
    }

    /// <summary>Creates, configures and starts a decoder for a track (video decoders are asked for flexible YUV 4:2:0 output).</summary>
    internal static MediaCodec CreateDecoder(MediaFormat format, string path, string mime)
    {
        string name = FindDecoder(format);
        if (name == null) throw new MediaFormatException($"This device has no decoder for the {mime} stream of '{path}'.");

        MediaCodec codec = null;
        try
        {
            codec = MediaCodec.CreateByCodecName(name);
            if (mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) format.SetInteger(MediaFormatKeys.ColorFormat, MediaFormatKeys.ColorFormatYuv420Flexible);
            codec.Configure(format, null, null, MediaCodecConfigFlags.None);
            codec.Start();
            return codec;
        }
        catch (JavaThrowable exception)
        {
            ReleaseQuietly(codec);
            throw new VideoProcessingException($"The {name} decoder could not be started for '{path}': {exception.Message}", exception);
        }
    }

    /// <summary>True when the device's codec list has an encoder for a MIME type.</summary>
    internal static bool HasEncoder(string mime)
    {
        try
        {
            foreach (MediaCodecInfo info in new MediaCodecList(MediaCodecListKind.RegularCodecs).GetCodecInfos())
            {
                if (!info.IsEncoder) continue;
                foreach (string type in info.GetSupportedTypes())
                {
                    if (string.Equals(type, mime, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
        }
        catch (JavaThrowable)
        {
        }

        return false;
    }

    /// <summary>The first encoder for a MIME type that accepts a format, else the first encoder for the MIME type; null when there is none.</summary>
    internal static MediaCodecInfo FindEncoder(MediaFormat format, string mime)
    {
        var list = new MediaCodecList(MediaCodecListKind.RegularCodecs);
        string preferred = null;
        try
        {
            preferred = list.FindEncoderForFormat(format);
        }
        catch (JavaThrowable)
        {
        }

        MediaCodecInfo fallback = null;
        foreach (MediaCodecInfo info in list.GetCodecInfos())
        {
            if (!info.IsEncoder) continue;
            bool supports = false;
            foreach (string type in info.GetSupportedTypes())
            {
                if (string.Equals(type, mime, StringComparison.OrdinalIgnoreCase)) supports = true;
            }

            if (!supports) continue;
            if (preferred != null && info.Name == preferred) return info;
            fallback ??= info;
        }

        return fallback;
    }

    /// <summary>The MediaMuxer output type for a path's extension: .webm, .3gp / .3g2 and .ogg / .opus by name, MP4 otherwise.</summary>
    internal static MuxerOutputType MuxerTypeFor(string path)
    {
        string extension = Path.GetExtension(path)?.ToLowerInvariant();
        return extension switch
        {
            ".webm" => MuxerOutputType.Webm,
            ".3gp" or ".3g2" => MuxerOutputType.M3gpp,
            ".ogg" or ".opus" => MuxerOutputType.Ogg,
            _ => MuxerOutputType.Mpeg4
        };
    }

    /// <summary>Runs the managed MP4 post-pass (fast start, title, creation time) on an MP4 / 3GP output.</summary>
    internal static void FinishMp4(string path, MuxerOutputType type, bool fastStart, string title, DateTimeOffset? creationTime)
    {
        if (type != MuxerOutputType.Mpeg4 && type != MuxerOutputType.M3gpp) return;
        try
        {
            Mp4Rewriter.Rewrite(path, new Mp4Rewriter.Options { MoveIndexToFront = fastStart, Title = title, CreationTime = creationTime });
        }
        catch (InvalidDataException exception)
        {
            throw new VideoProcessingException($"The MP4 post-pass could not rewrite '{path}': {exception.Message}", exception);
        }
    }

    /// <summary>Deletes a file, ignoring failures.</summary>
    internal static void DeleteQuietly(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Stops and releases a codec, ignoring failures.</summary>
    internal static void ReleaseQuietly(MediaCodec codec)
    {
        if (codec == null) return;
        try { codec.Stop(); } catch (JavaThrowable) { } catch (InvalidOperationException) { }
        try { codec.Release(); } catch (JavaThrowable) { }
    }

    /// <summary>Releases an extractor, ignoring failures.</summary>
    internal static void ReleaseQuietly(MediaExtractor extractor)
    {
        if (extractor == null) return;
        try { extractor.Release(); } catch (JavaThrowable) { }
    }

    /// <summary>Releases a muxer (stopping it first when asked), ignoring failures.</summary>
    internal static void ReleaseQuietly(MediaMuxer muxer, bool stop)
    {
        if (muxer == null) return;
        if (stop)
        {
            try { muxer.Stop(); } catch (JavaThrowable) { }
        }

        try { muxer.Release(); } catch (JavaThrowable) { }
    }

    /// <summary>A description of a platform failure for a message.</summary>
    internal static string Describe(JavaThrowable exception)
    {
        if (exception is MediaCodec.CodecException codec)
        {
            return $"MediaCodec error 0x{codec.ErrorCode:X} ({codec.DiagnosticInfo}; transient {codec.IsTransient}, recoverable {codec.IsRecoverable})";
        }

        return (exception.Class?.Name ?? exception.GetType().Name) + ": " + (exception.LocalizedMessage ?? exception.Message);
    }
}
