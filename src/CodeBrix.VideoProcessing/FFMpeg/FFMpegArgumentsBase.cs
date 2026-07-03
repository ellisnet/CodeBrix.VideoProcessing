using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Arguments;

namespace CodeBrix.VideoProcessing; //was previously: FFMpegCore;

public abstract class FFMpegArgumentsBase
{
    internal readonly List<IArgument> Arguments = new();
}
