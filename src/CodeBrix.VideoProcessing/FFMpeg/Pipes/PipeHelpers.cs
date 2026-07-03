using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Pipes; //was previously: FFMpegCore.Pipes;

internal static class PipeHelpers
{
    public static string GetUniquePipeName()
    {
        return $"FFMpegCore_{Guid.NewGuid().ToString("N").Substring(0, 16)}";
    }

    public static string GetPipePath(string pipeName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $@"\\.\pipe\{pipeName}";
        }

        return $"unix:{Path.Combine(Path.GetTempPath(), $"CoreFxPipe_{pipeName}")}";
    }
}
