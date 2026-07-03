using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

public class GifPaletteArgument : IArgument
{
    private readonly double _fps;
    private readonly Size? _size;
    private readonly int _streamIndex;

    public GifPaletteArgument(int streamIndex, double fps, Size? size)
    {
        _streamIndex = streamIndex;
        _fps = fps;
        _size = size;
    }

    private string ScaleText => _size.HasValue ? $"scale=w={_size.Value.Width}:h={_size.Value.Height}," : string.Empty;

    public string Text =>
        $"-filter_complex \"[{_streamIndex}:v] fps={_fps},{ScaleText}split [a][b];[a] palettegen=max_colors=32 [p];[b][p] paletteuse=dither=bayer\"";
}
