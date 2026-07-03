using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents duration parameter
/// </summary>
public class DurationArgument : IArgument
{
    public readonly TimeSpan? Duration;

    public DurationArgument(TimeSpan? duration)
    {
        Duration = duration;
    }

    public string Text => !Duration.HasValue ? string.Empty : $"-t {Duration.Value}";
}
