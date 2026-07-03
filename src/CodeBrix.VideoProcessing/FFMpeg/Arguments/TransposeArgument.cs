using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Transpose argument.
///     0 = 90CounterCLockwise and Vertical Flip (default)
///     1 = 90Clockwise
///     2 = 90CounterClockwise
///     3 = 90Clockwise and Vertical Flip
/// </summary>
public class TransposeArgument : IVideoFilterArgument
{
    public readonly Transposition Transposition;

    public TransposeArgument(Transposition transposition)
    {
        Transposition = transposition;
    }

    public string Key { get; } = "transpose";
    public string Value => ((int)Transposition).ToString();
}
