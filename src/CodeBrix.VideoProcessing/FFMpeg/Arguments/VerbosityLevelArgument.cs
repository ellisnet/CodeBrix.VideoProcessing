using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

public class VerbosityLevelArgument : IArgument
{
    private readonly VerbosityLevel _verbosityLevel;

    public VerbosityLevelArgument(VerbosityLevel verbosityLevel)
    {
        _verbosityLevel = verbosityLevel;
    }

    public string Text => $"{((int)_verbosityLevel < 32 ? "-hide_banner " : "")}-loglevel {_verbosityLevel.ToString().ToLowerInvariant()}";
}

public enum VerbosityLevel
{
    Quiet = -8,
    Fatal = 8,
    Error = 16,
    Warning = 24,
    Info = 32,
    Verbose = 40,
    Debug = 48,
    Trace = 56
}
