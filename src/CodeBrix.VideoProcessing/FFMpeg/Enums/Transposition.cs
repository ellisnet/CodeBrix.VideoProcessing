using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Enums; //was previously: FFMpegCore.Enums;

public enum Transposition
{
    CounterClockwise90VerticalFlip = 0,
    Clockwise90 = 1,
    CounterClockwise90 = 2,
    Clockwise90VerticalFlip = 3
}
