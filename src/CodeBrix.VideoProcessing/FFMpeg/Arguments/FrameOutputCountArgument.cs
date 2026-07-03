using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents frame output count parameter
/// </summary>
public class FrameOutputCountArgument : IArgument
{
    public readonly int Frames;

    public FrameOutputCountArgument(int frames)
    {
        Frames = frames;
    }

    public string Text => $"-vframes {Frames}";
}
