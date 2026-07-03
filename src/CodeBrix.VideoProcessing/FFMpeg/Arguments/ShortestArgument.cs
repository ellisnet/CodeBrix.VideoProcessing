using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents shortest parameter
/// </summary>
public class ShortestArgument : IArgument
{
    public readonly bool Shortest;

    public ShortestArgument(bool shortest)
    {
        Shortest = shortest;
    }

    public string Text => Shortest ? "-shortest" : string.Empty;
}
