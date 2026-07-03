using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Extend;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents seek parameter
/// </summary>
public class SeekArgument : IArgument
{
    public readonly TimeSpan? SeekTo;

    public SeekArgument(TimeSpan? seekTo)
    {
        SeekTo = seekTo;
    }

    public string Text => SeekTo.HasValue ? $"-ss {SeekTo.Value.ToLongString()}" : string.Empty;
}
