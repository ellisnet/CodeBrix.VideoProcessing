using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents loop parameter
/// </summary>
public class LoopArgument : IArgument
{
    public readonly int Times;

    public LoopArgument(int times)
    {
        Times = times;
    }

    public string Text => $"-loop {Times}";
}
