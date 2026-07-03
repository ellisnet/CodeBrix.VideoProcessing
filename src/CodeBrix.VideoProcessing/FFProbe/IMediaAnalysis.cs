using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Builders.MetaData;

namespace CodeBrix.VideoProcessing; //was previously: FFMpegCore;

public interface IMediaAnalysis
{
    TimeSpan Duration { get; }
    MediaFormat Format { get; }
    List<ChapterData> Chapters { get; }
    AudioStream PrimaryAudioStream { get; }
    VideoStream PrimaryVideoStream { get; }
    SubtitleStream PrimarySubtitleStream { get; }
    List<VideoStream> VideoStreams { get; }
    List<AudioStream> AudioStreams { get; }
    List<SubtitleStream> SubtitleStreams { get; }
    IReadOnlyList<string> ErrorData { get; }
}
