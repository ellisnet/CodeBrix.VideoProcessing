using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Enums; //was previously: FFMpegCore.Enums;

public enum VideoSize
{
    FullHd = 1080,
    Hd = 720,
    Ed = 480,
    Ld = 360,
    Original = -1
}
