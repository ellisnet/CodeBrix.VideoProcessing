using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

public class ForcePixelFormat : IArgument
{
    public ForcePixelFormat(string format)
    {
        PixelFormat = format;
    }

    public ForcePixelFormat(PixelFormat format) : this(format.Name) { }
    public string PixelFormat { get; }
    public string Text => $"-pix_fmt {PixelFormat}";
}
