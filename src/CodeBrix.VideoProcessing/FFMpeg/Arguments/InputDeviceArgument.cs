using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents an input device parameter
/// </summary>
public class InputDeviceArgument : IInputArgument
{
    private readonly string Device;

    public InputDeviceArgument(string device)
    {
        Device = device;
    }

    public Task During(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Pre() { }

    public void Post() { }

    public string Text => $"-i {Device}";
}
