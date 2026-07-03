using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Exceptions; //was previously: FFMpegCore.Exceptions;

public class FFProbeException : Exception
{
    public FFProbeException(string message, Exception inner = null) : base(message, inner)
    {
    }
}
