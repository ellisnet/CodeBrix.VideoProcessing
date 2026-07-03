using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Exceptions;
using CodeBrix.VideoProcessing.Instances;

namespace CodeBrix.VideoProcessing.Helpers; //was previously: FFMpegCore.Helpers;

public static class FFProbeHelper
{
    private static bool _ffprobeVerified;

    public static void RootExceptionCheck()
    {
        if (GlobalFFOptions.Current.BinaryFolder == null)
        {
            throw new FFOptionsException("FFProbe root is not configured in app config. Missing key 'BinaryFolder'.");
        }
    }

    public static void VerifyFFProbeExists(FFOptions ffMpegOptions)
    {
        if (_ffprobeVerified)
        {
            return;
        }

        var result = Instance.Finish(GlobalFFOptions.GetFFProbeBinaryPath(ffMpegOptions), "-version");
        _ffprobeVerified = result.ExitCode == 0;
        if (!_ffprobeVerified)
        {
            throw new FFProbeException("ffprobe was not found on your system");
        }
    }
}
