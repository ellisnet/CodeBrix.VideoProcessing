using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

public class HardwareAccelerationArgument : IArgument
{
    public HardwareAccelerationArgument(HardwareAccelerationDevice hardwareAccelerationDevice)
    {
        HardwareAccelerationDevice = hardwareAccelerationDevice;
    }

    public HardwareAccelerationDevice HardwareAccelerationDevice { get; }

    public string Text => $"-hwaccel {HardwareAccelerationDevice.ToString().ToLower()}";
}
