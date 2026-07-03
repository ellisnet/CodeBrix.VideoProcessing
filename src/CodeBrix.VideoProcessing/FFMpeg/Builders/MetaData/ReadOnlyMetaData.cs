using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Builders.MetaData; //was previously: FFMpegCore.Builders.MetaData;

public class ReadOnlyMetaData : IReadOnlyMetaData
{
    public ReadOnlyMetaData(MetaData metaData)
    {
        Entries = new Dictionary<string, string>(metaData.Entries);
        Chapters = metaData.Chapters
            .Select(x => new ChapterData
            (
                start: x.Start,
                end: x.End,
                title: x.Title
            ))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyDictionary<string, string> Entries { get; }
    public IReadOnlyList<ChapterData> Chapters { get; }
}
