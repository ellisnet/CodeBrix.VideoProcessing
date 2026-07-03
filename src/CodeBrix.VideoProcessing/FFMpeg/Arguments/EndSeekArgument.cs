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
public class EndSeekArgument : IArgument
{
    public readonly TimeSpan? SeekTo;

    public EndSeekArgument(TimeSpan? seekTo)
    {
        SeekTo = seekTo;
    }

    public string Text => SeekTo.HasValue ? $"-to {SeekTo.Value.ToLongString()}" : string.Empty;
}
