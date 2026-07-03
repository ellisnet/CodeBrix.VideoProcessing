using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Exceptions; //was previously: FFMpegCore.Exceptions;

public class FFProbeProcessException : FFProbeException
{
    public FFProbeProcessException(string message, IReadOnlyCollection<string> processErrors, Exception inner = null) : base(message, inner)
    {
        ProcessErrors = processErrors;
    }

    public IReadOnlyCollection<string> ProcessErrors { get; }
}
