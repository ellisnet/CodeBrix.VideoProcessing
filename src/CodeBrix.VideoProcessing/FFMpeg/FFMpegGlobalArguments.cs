using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Arguments;

namespace CodeBrix.VideoProcessing; //was previously: FFMpegCore;

public sealed class FFMpegGlobalArguments : FFMpegArgumentsBase
{
    internal FFMpegGlobalArguments() { }

    public FFMpegGlobalArguments WithVerbosityLevel(VerbosityLevel verbosityLevel = VerbosityLevel.Error)
    {
        return WithOption(new VerbosityLevelArgument(verbosityLevel));
    }

    private FFMpegGlobalArguments WithOption(IArgument argument)
    {
        Arguments.Add(argument);
        return this;
    }
}
