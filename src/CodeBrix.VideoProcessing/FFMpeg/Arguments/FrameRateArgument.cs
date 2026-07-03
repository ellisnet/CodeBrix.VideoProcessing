using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents frame rate parameter
/// </summary>
public class FrameRateArgument : IArgument
{
    public readonly double Framerate;

    public FrameRateArgument(double framerate)
    {
        Framerate = framerate;
    }

    public string Text => $"-r {Framerate.ToString(CultureInfo.InvariantCulture)}";
}
