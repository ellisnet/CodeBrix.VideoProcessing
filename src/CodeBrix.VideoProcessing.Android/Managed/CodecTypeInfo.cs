using System;
using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// One (codec, MIME type) entry of the device's codec list, copied into plain values: the codec's name, whether it is
/// an encoder, the MIME type and the color formats its capabilities list for that type. The engine fills these from
/// MediaCodecList on the device; the decisions made from them (<see cref="AndroidEngineCapabilities" />) work on them
/// anywhere, which is what lets the host tests feed a fake codec list.
/// </summary>
internal sealed class CodecTypeInfo
{
    /// <summary>Creates an entry.</summary>
    /// <param name="name">The codec name ("c2.qti.hevc.decoder", ...).</param>
    /// <param name="isEncoder">True for an encoder.</param>
    /// <param name="mime">The MIME type ("video/hevc", ...).</param>
    /// <param name="colorFormats">MediaCodecInfo.CodecCapabilities.colorFormats for the type; null means none.</param>
    internal CodecTypeInfo(string name, bool isEncoder, string mime, IEnumerable<int> colorFormats)
    {
        Name = name ?? string.Empty;
        IsEncoder = isEncoder;
        Mime = mime ?? string.Empty;
        ColorFormats = colorFormats == null ? Array.Empty<int>() : new List<int>(colorFormats).ToArray();
    }

    /// <summary>The codec name.</summary>
    internal string Name { get; }

    /// <summary>True for an encoder, false for a decoder.</summary>
    internal bool IsEncoder { get; }

    /// <summary>The MIME type this entry describes.</summary>
    internal string Mime { get; }

    /// <summary>The color formats the codec lists for <see cref="Mime" />.</summary>
    internal IReadOnlyList<int> ColorFormats { get; }

    /// <summary>True for a VIDEO decoder that lists COLOR_FormatYUVP010 (10-bit output) for its type.</summary>
    internal bool IsP010VideoDecoder
    {
        get
        {
            if (IsEncoder || !Mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return false;
            foreach (int format in ColorFormats)
            {
                if (format == MediaFormatKeys.ColorFormatYuvP010) return true;
            }

            return false;
        }
    }

    /// <summary>"name (mime)", the form the engine reports a codec in.</summary>
    /// <returns>The description.</returns>
    public override string ToString() => $"{Name} ({Mime})";
}
