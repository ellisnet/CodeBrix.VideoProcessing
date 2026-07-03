using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Enums; //was previously: FFMpegCore.Enums;

public enum FFMpegLogLevel
{
    Quiet = 0,
    Panic = 1,
    Fatal = 2,
    Error = 3,
    Warning = 4,
    Info = 5,
    Verbose = 6,
    Debug = 7,
    Trace = 8
}
