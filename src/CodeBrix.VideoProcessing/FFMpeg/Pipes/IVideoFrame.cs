using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Pipes; //was previously: FFMpegCore.Pipes;

/// <summary>
///     Interface for Video frame
/// </summary>
public interface IVideoFrame
{
    int Width { get; }
    int Height { get; }
    string Format { get; }

    void Serialize(Stream pipe);
    Task SerializeAsync(Stream pipe, CancellationToken token);
}
