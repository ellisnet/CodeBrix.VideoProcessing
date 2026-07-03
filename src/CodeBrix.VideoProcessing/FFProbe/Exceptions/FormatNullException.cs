using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Exceptions; //was previously: FFMpegCore.Exceptions;

public class FormatNullException : FFProbeException
{
    public FormatNullException() : base("Format not specified")
    {
    }
}
