using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Enums; //was previously: FFMpegCore.Enums;

public enum Speed
{
    VerySlow,
    Slower,
    Slow,
    Medium,
    Fast,
    Faster,
    VeryFast,
    SuperFast,
    UltraFast
}
