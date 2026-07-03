using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents threads parameter
///     Number of threads used for video encoding
/// </summary>
public class ThreadsArgument : IArgument
{
    public readonly int Threads;

    public ThreadsArgument(int threads)
    {
        Threads = threads;
    }

    public ThreadsArgument(bool isMultiThreaded) : this(isMultiThreaded ? Environment.ProcessorCount : 1) { }

    public string Text => $"-threads {Threads}";
}
