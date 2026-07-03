using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

public class BlackDetectArgument : IVideoFilterArgument
{
    public BlackDetectArgument(double minimumDuration = 2.0, double pictureBlackRatioThreshold = 0.98, double pixelBlackThreshold = 0.1)
    {
        Value = $"d={minimumDuration}:pic_th={pictureBlackRatioThreshold}:pix_th={pixelBlackThreshold}";
    }

    public string Key => "blackdetect";

    public string Value { get; }
}
