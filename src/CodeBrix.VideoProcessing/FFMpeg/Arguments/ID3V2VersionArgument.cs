using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments; //was previously: FFMpegCore.Arguments;

public class ID3V2VersionArgument : IArgument
{
    private readonly int _version;

    public ID3V2VersionArgument(int version)
    {
        _version = version;
    }

    public string Text => $"-id3v2_version {_version}";
}
