using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Faststart argument - for moving moov atom to the start of file
/// </summary>
public class FaststartArgument : IArgument
{
    public string Text => "-movflags faststart";
}
