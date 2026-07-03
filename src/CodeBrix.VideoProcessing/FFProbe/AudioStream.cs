using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing; //was previously: FFMpegCore;

public class AudioStream : MediaStream
{
    public int Channels { get; set; }
    public string ChannelLayout { get; set; } = null;
    public int SampleRateHz { get; set; }
    public string Profile { get; set; } = null;
}
