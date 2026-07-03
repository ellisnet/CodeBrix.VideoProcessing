using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents outputting to url using supported protocols
///     See http://ffmpeg.org/ffmpeg-protocols.html
/// </summary>
public class OutputUrlArgument : IOutputArgument
{
    public readonly string Url;

    public OutputUrlArgument(string url)
    {
        Url = url;
    }

    public void Post() { }

    public Task During(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Pre() { }

    public string Text => Url;
}
