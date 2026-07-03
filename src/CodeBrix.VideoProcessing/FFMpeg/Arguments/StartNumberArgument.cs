using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents start number parameter
/// </summary>
public class StartNumberArgument : IArgument
{
    public readonly int StartNumber;

    public StartNumberArgument(int startNumber)
    {
        StartNumber = startNumber;
    }

    public string Text => $"-start_number {StartNumber}";
}
