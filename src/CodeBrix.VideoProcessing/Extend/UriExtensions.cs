using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Extend; //was previously: FFMpegCore.Extend;

public static class UriExtensions
{
    public static bool SaveStream(this Uri uri, string output)
    {
        return FFMpeg.SaveM3U8Stream(uri, output);
    }
}
