using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Constant Rate Factor (CRF) argument
/// </summary>
public class ConstantRateFactorArgument : IArgument
{
    public readonly int Crf;

    public ConstantRateFactorArgument(int crf)
    {
        if (crf < 0 || crf > 63)
        {
            throw new ArgumentException("Argument is outside range (0 - 63)", nameof(crf));
        }

        Crf = crf;
    }

    public string Text => $"-crf {Crf}";
}
