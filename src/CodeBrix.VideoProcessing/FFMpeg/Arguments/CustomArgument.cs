using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

public class CustomArgument : IArgument
{
    public readonly string Argument;

    public CustomArgument(string argument)
    {
        Argument = argument;
    }

    public string Text => Argument ?? string.Empty;
}
