using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents parameter of bitstream filter
/// </summary>
public class BitStreamFilterArgument : IArgument
{
    public readonly Channel Channel;
    public readonly Filter Filter;

    public BitStreamFilterArgument(Channel channel, Filter filter)
    {
        Channel = channel;
        Filter = filter;
    }

    public string Text => Channel switch
    {
        Channel.Audio => $"-bsf:a {Filter.ToString().ToLowerInvariant()}",
        Channel.Video => $"-bsf:v {Filter.ToString().ToLowerInvariant()}",
        _ => string.Empty
    };
}
