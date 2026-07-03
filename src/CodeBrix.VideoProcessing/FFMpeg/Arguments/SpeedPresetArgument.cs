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
///     Represents speed parameter
/// </summary>
public class SpeedPresetArgument : IArgument
{
    public readonly Speed Speed;

    public SpeedPresetArgument(Speed speed)
    {
        Speed = speed;
    }

    public string Text => $"-preset {Speed.ToString().ToLowerInvariant()}";
}
