using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

public class SetMirroringArgument : IVideoFilterArgument
{
    public SetMirroringArgument(Mirroring mirroring)
    {
        Mirroring = mirroring;
    }

    public Mirroring Mirroring { get; set; }

    public string Key => string.Empty;

    public string Value =>
        Mirroring switch
        {
            Mirroring.Horizontal => "hflip",
            Mirroring.Vertical => "vflip",
            _ => throw new ArgumentOutOfRangeException(nameof(Mirroring))
        };
}
