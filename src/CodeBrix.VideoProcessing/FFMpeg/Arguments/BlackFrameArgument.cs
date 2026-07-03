using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

internal class BlackFrameArgument : IVideoFilterArgument
{
    public BlackFrameArgument(int amount = 98, int threshold = 32)
    {
        Value = $"amount={amount}:threshold={threshold}";
    }

    public string Key => "blackframe";

    public string Value { get; }
}
