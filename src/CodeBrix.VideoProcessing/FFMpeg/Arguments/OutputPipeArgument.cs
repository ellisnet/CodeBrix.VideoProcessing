using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Pipes;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

public class OutputPipeArgument : PipeArgument, IOutputArgument
{
    public readonly IPipeSink Reader;

    public OutputPipeArgument(IPipeSink reader) : base(PipeDirection.In)
    {
        Reader = reader;
    }

    public override string Text => $"\"{PipePath}\" -y";

    // CODEBRIX FIX (not in upstream FFMpegCore) - see OUTPUT PIPES DRAIN BEFORE THEY ARE DISPOSED in
    // MAINTAINER-README.txt. Upstream hands the sink the During token, which FFMpegArgumentProcessor cancels
    // the moment ffmpeg exits, so a sink still reading the pipe's buffered tail was cut off silently.
    // FFMpegArgumentProcessor now sets this to a token that is cancelled only by a user cancel or a drain
    // timeout. The During token still bounds the wait for ffmpeg to connect. Left at default (During called
    // directly), the sink reads under the During token, exactly as upstream.
    internal CancellationToken SinkCancellationToken { get; set; }

    protected override async Task ProcessDataAsync(CancellationToken token)
    {
        await Pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
        if (!Pipe.IsConnected)
        {
            throw new TaskCanceledException();
        }

        await Reader.ReadAsync(Pipe, SinkCancellationToken.CanBeCanceled ? SinkCancellationToken : token).ConfigureAwait(false);
    }
}
