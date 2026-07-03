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
///     Represents cpu speed parameter
/// </summary>
public class DisableChannelArgument : IArgument
{
    public readonly Channel Channel;

    public DisableChannelArgument(Channel channel)
    {
        if (channel == Channel.Both)
        {
            throw new FFMpegException(FFMpegExceptionType.Conversion, "Cannot disable both channels");
        }

        Channel = channel;
    }

    public string Text => Channel switch
    {
        Channel.Video => "-vn",
        Channel.Audio => "-an",
        _ => string.Empty
    };
}
