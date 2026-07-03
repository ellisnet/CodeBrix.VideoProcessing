using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Audio sampling rate argument. Defaults to 48000 (Hz)
/// </summary>
public class AudioSamplingRateArgument : IArgument
{
    public readonly int SamplingRate;

    public AudioSamplingRateArgument(int samplingRate = 48000)
    {
        SamplingRate = samplingRate;
    }

    public string Text => $"-ar {SamplingRate}";
}
