using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents video bitrate parameter
/// </summary>
public class VideoBitrateArgument : IArgument
{
    public readonly int Bitrate;

    public VideoBitrateArgument(int bitrate)
    {
        Bitrate = bitrate;
    }

    public string Text => $"-b:v {Bitrate}k";
}
