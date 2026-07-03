using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

public interface IArgument
{
    /// <summary>
    ///     The textual representation of the argument
    /// </summary>
    string Text { get; }
}
