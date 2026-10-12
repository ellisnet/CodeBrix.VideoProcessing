using System;
using System.Collections.Generic;
using CodeBrix.VideoProcessing.Core;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>The Android engine's capability flags, as a pure function of what the device's codec list offers.</summary>
/// <remarks>
/// Always set: RgbaInput, Nv12Input (managed conversions both ways), GrayFrameSource (the Y plane of the decoded 4:2:0
/// picture), Snapshot, DirectTranscode, DirectTrim, DirectConcatenate. HevcOutput only when the device has a
/// "video/hevc" ENCODER. HighBitDepthFrameSource only when the device has at least one VIDEO DECODER whose
/// capabilities list COLOR_FormatYUVP010 (10-bit output; see <see cref="FindP010Decoders" />). Never set:
/// AlphaFrameSource (Android's decoders expose no alpha) and I444FrameSource (the decoders deliver 4:2:0).
/// </remarks>
internal static class AndroidEngineCapabilities
{
    /// <summary>The flags every Android device gets.</summary>
    internal const VideoProcessorCapabilities Always =
        VideoProcessorCapabilities.RgbaInput |
        VideoProcessorCapabilities.Nv12Input |
        VideoProcessorCapabilities.GrayFrameSource |
        VideoProcessorCapabilities.Snapshot |
        VideoProcessorCapabilities.DirectTranscode |
        VideoProcessorCapabilities.DirectTrim |
        VideoProcessorCapabilities.DirectConcatenate;

    /// <summary>The flags for a device.</summary>
    /// <param name="hasHevcEncoder">True when MediaCodecList has a video/hevc encoder.</param>
    /// <param name="hasP010Decoder">True when MediaCodecList has a video decoder that lists P010 output.</param>
    internal static VideoProcessorCapabilities Compute(bool hasHevcEncoder, bool hasP010Decoder)
    {
        VideoProcessorCapabilities flags = Always;
        if (hasHevcEncoder) flags |= VideoProcessorCapabilities.HevcOutput;
        if (hasP010Decoder) flags |= VideoProcessorCapabilities.HighBitDepthFrameSource;
        return flags;
    }

    /// <summary>The flags for a device, from its whole codec list.</summary>
    /// <param name="codecs">Every (codec, MIME type) entry of MediaCodecList.</param>
    internal static VideoProcessorCapabilities Compute(IEnumerable<CodecTypeInfo> codecs)
    {
        bool hevcEncoder = false;
        foreach (CodecTypeInfo codec in codecs ?? Array.Empty<CodecTypeInfo>())
        {
            if (codec.IsEncoder && string.Equals(codec.Mime, MediaFormatKeys.MimeHevc, StringComparison.OrdinalIgnoreCase)) hevcEncoder = true;
        }

        return Compute(hevcEncoder, FindP010Decoders(codecs).Count > 0);
    }

    /// <summary>
    /// The video decoders that list COLOR_FormatYUVP010 for one of their types, as "name (mime)" in codec-list order
    /// (one entry per codec and type). Encoders, audio codecs and decoders without P010 are left out.
    /// </summary>
    /// <param name="codecs">Every (codec, MIME type) entry of MediaCodecList; null means none.</param>
    internal static IReadOnlyList<string> FindP010Decoders(IEnumerable<CodecTypeInfo> codecs)
    {
        var found = new List<string>();
        if (codecs == null) return found;
        foreach (CodecTypeInfo codec in codecs)
        {
            if (codec != null && codec.IsP010VideoDecoder) found.Add(codec.ToString());
        }

        return found;
    }

    /// <summary>
    /// Picks the decoder to ask for P010 output for a stream: the platform's own choice when it lists P010 for the
    /// stream's MIME type, else the first OTHER decoder of that type that does (the candidates should already be
    /// limited to decoders that accept the stream's format); null when there is none.
    /// </summary>
    /// <param name="candidates">The device's (codec, MIME type) entries that can decode the stream.</param>
    /// <param name="mime">The stream's MIME type.</param>
    /// <param name="preferred">The platform's choice (MediaCodecList.findDecoderForFormat); may be null.</param>
    internal static string ChooseP010Decoder(IEnumerable<CodecTypeInfo> candidates, string mime, string preferred)
    {
        if (candidates == null) return null;
        string first = null;
        foreach (CodecTypeInfo codec in candidates)
        {
            if (codec == null || !codec.IsP010VideoDecoder || !string.Equals(codec.Mime, mime, StringComparison.OrdinalIgnoreCase)) continue;
            if (preferred != null && codec.Name == preferred) return codec.Name;
            first ??= codec.Name;
        }

        return first;
    }
}
