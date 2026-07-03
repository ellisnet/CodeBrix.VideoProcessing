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
///     Represents force format parameter
/// </summary>
public class ForceFormatArgument : IArgument
{
    private readonly string _format;

    public ForceFormatArgument(string format)
    {
        _format = format;
    }

    public ForceFormatArgument(ContainerFormat format)
    {
        _format = format.Name;
    }

    public string Text => $"-f {_format}";
}
