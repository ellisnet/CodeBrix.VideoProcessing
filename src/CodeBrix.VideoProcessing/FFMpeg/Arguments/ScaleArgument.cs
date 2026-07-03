using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

/// <summary>
///     Represents scale parameter
/// </summary>
public class ScaleArgument : IVideoFilterArgument
{
    public readonly Size? Size;

    public ScaleArgument(Size? size)
    {
        Size = size;
    }

    public ScaleArgument(int width, int height) : this(new Size(width, height)) { }

    public ScaleArgument(VideoSize videosize)
    {
        Size = videosize == VideoSize.Original ? null : new Size(-1, (int)videosize);
    }

    public string Key { get; } = "scale";
    public string Value => Size == null ? string.Empty : $"{Size.Value.Width}:{Size.Value.Height}";
}
