using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Exceptions;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents video codec parameter
/// </summary>
public class VideoCodecArgument : IArgument
{
    public readonly string Codec;

    public VideoCodecArgument(string codec)
    {
        Codec = codec;
    }

    public VideoCodecArgument(Codec value)
    {
        if (value.Type != CodecType.Video)
        {
            throw new FFMpegException(FFMpegExceptionType.Operation, $"Codec \"{value.Name}\" is not a video codec");
        }

        Codec = value.Name;
    }

    public string Text => $"-c:v {Codec}";
}
