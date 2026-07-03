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
///     Represents parameter of audio codec and it's quality
/// </summary>
public class AudioBitrateArgument : IArgument
{
    public readonly int Bitrate;
    public AudioBitrateArgument(AudioQuality value) : this((int)value) { }

    public AudioBitrateArgument(int bitrate)
    {
        Bitrate = bitrate;
    }

    public string Text => $"-b:a {Bitrate}k";
}
