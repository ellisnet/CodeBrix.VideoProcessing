using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Pipes; //was previously: FFMpegCore.Pipes;

public interface IPipeSink
{
    Task ReadAsync(Stream inputStream, CancellationToken cancellationToken);
    string GetFormat();
}
