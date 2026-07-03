using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents overwrite parameter
///     If output file should be overwritten if exists
/// </summary>
public class OverwriteArgument : IArgument
{
    public string Text => "-y";
}
