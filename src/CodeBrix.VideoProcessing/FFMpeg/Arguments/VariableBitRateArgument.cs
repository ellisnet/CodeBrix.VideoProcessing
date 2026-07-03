using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Variable Bitrate Argument (VBR) argument
/// </summary>
public class VariableBitRateArgument : IArgument
{
    public readonly int Vbr;

    public VariableBitRateArgument(int vbr)
    {
        if (vbr < 0 || vbr > 5)
        {
            throw new ArgumentException("Argument is outside range (0 - 5)", nameof(vbr));
        }

        Vbr = vbr;
    }

    public string Text => $"-vbr {Vbr}";
}
