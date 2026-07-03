using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Instances;

namespace CodeBrix.VideoProcessing; //was previously: FFMpegCore;

public static class ProcessArgumentsExtensions
{
    public static IProcessResult StartAndWaitForExit(this ProcessArguments processArguments)
    {
        using var instance = processArguments.Start();
        return instance.WaitForExit();
    }

    public static async Task<IProcessResult> StartAndWaitForExitAsync(this ProcessArguments processArguments, CancellationToken cancellationToken = default)
    {
        using var instance = processArguments.Start();
        return await instance.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }
}
