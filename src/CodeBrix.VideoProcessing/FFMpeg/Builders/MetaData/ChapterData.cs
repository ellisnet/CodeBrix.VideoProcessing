using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Builders.MetaData; //was previously: FFMpegCore.Builders.MetaData;

public class ChapterData
{
    public ChapterData(string title, TimeSpan start, TimeSpan end)
    {
        Title = title;
        Start = start;
        End = end;
    }

    public string Title { get; private set; }
    public TimeSpan Start { get; }
    public TimeSpan End { get; }

    public TimeSpan Duration => End - Start;
}
