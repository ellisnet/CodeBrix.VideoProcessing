using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Builders.MetaData; //was previously: FFMpegCore.Builders.MetaData;

public interface IReadOnlyMetaData
{
    IReadOnlyList<ChapterData> Chapters { get; }
    IReadOnlyDictionary<string, string> Entries { get; }
}
