using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Enums; //was previously: FFMpegCore.Enums;

public enum AudioQuality
{
    Ultra = 384,
    VeryHigh = 256,
    Good = 192,
    Normal = 128,
    BelowNormal = 96,
    Low = 64
}
