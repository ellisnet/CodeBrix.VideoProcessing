using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Remove metadata argument
/// </summary>
public class RemoveMetadataArgument : IArgument
{
    public string Text => "-map_metadata -1";
}
