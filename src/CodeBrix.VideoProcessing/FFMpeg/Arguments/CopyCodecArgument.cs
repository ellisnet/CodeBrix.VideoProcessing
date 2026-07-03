using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents a copy codec parameter
/// </summary>
public class CopyCodecArgument : IArgument
{
    public string Text => "-codec copy";
}
