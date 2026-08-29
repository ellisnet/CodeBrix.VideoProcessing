using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Exceptions;

namespace CodeBrix.VideoProcessing.Arguments;

/// <summary>
///     Represents the subtitle-codec parameter, <c>-c:s</c>.
/// </summary>
public class SubtitleCodecArgument : IArgument
{
    /// <summary>The codec name this argument emits.</summary>
    public readonly string SubtitleCodec;

    /// <summary>
    ///     Sets the subtitle codec from a codec reported by the installed FFmpeg.
    /// </summary>
    /// <param name="subtitleCodec">The codec; it must be a subtitle codec.</param>
    /// <exception cref="FFMpegException">The codec is not a subtitle codec.</exception>
    public SubtitleCodecArgument(Codec subtitleCodec)
    {
        if (subtitleCodec.Type != CodecType.Subtitle)
        {
            throw new FFMpegException(FFMpegExceptionType.Operation, $"Codec \"{subtitleCodec.Name}\" is not a subtitle codec");
        }

        SubtitleCodec = subtitleCodec.Name;
    }

    /// <summary>
    ///     Sets the subtitle codec by name, for example <c>webvtt</c>, <c>mov_text</c> or <c>copy</c>.
    /// </summary>
    /// <param name="subtitleCodec">The codec name.</param>
    public SubtitleCodecArgument(string subtitleCodec)
    {
        SubtitleCodec = subtitleCodec;
    }

    /// <summary>The emitted text, for example <c>-c:s webvtt</c>.</summary>
    public string Text => $"-c:s {SubtitleCodec.ToLowerInvariant()}";
}
