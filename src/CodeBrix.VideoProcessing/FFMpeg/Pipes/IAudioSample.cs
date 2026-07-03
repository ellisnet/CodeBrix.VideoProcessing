using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Pipes; //was previously: FFMpegCore.Pipes;

/// <summary>
///     Interface for Audio sample
/// </summary>
public interface IAudioSample
{
    void Serialize(Stream stream);

    Task SerializeAsync(Stream stream, CancellationToken token);
}
