using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;

namespace CodeBrix.VideoProcessing; //was previously: FFMpegCore;

public class FFOptions : ICloneable
{
    /// <summary>
    ///     Working directory for the ffmpeg/ffprobe instance
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    ///     Folder container ffmpeg and ffprobe binaries. Leave empty if ffmpeg and ffprobe are present in PATH
    /// </summary>
    public string BinaryFolder { get; set; } = string.Empty;

    /// <summary>
    ///     Folder used for temporary files necessary for static methods on FFMpeg class
    /// </summary>
    public string TemporaryFilesFolder { get; set; } = Path.GetTempPath();

    /// <summary>
    ///     Encoding web name used to persist encoding <see cref="Encoding" />
    /// </summary>
    public string EncodingWebName { get; set; } = Encoding.Default.WebName;

    /// <summary>
    ///     Encoding used for parsing stdout/stderr on ffmpeg and ffprobe processes
    /// </summary>
    [JsonIgnore]
    public Encoding Encoding
    {
        get => Encoding.GetEncoding(EncodingWebName);
        set => EncodingWebName = value?.WebName ?? Encoding.Default.WebName;
    }

    /// <summary>
    ///     The log level to use when calling of the ffmpeg executable.
    ///     <para>
    ///         This option can be overridden before an execution of a Process command
    ///         to set the log level for that command.
    ///     </para>
    /// </summary>
    public FFMpegLogLevel? LogLevel { get; set; }

    /// <summary>
    /// </summary>
    public Dictionary<string, string> ExtensionOverrides { get; set; } = new() { { "mpegts", ".ts" } };

    /// <summary>
    ///     Whether to cache calls to get ffmpeg codec, pixel- and container-formats
    /// </summary>
    public bool UseCache { get; set; } = true;

    /// <summary>
    ///     Whether an output that asks for the SVT-AV1 encoder (<c>libsvtav1</c>) may be encoded with libaom
    ///     (<c>libaom-av1</c>) instead, when the ffmpeg being run does not have SVT-AV1. Off by default: without it,
    ///     a missing <c>libsvtav1</c> fails the run with an <see cref="Exceptions.FFMpegEncoderNotFoundException" />
    ///     that explains the problem and suggests turning this on.
    ///     <para>
    ///         When it is on, ffmpeg's encoder list is read before any run that selects <c>libsvtav1</c> (once per
    ///         ffmpeg binary while <see cref="UseCache" /> is on). If SVT-AV1 is missing and libaom is present, the
    ///         arguments of each output that selects <c>libsvtav1</c> are rewritten before ffmpeg starts: the codec
    ///         becomes <c>libaom-av1</c>, a numeric <c>-preset N</c> becomes <c>-cpu-used N</c> capped at 8,
    ///         <c>-b:v 0</c> follows <c>-crf</c> unless a video bit rate is set, and <c>-svtav1-params</c> is dropped.
    ///         <see cref="FFMpegArgumentProcessor.Av1EncoderFallbackNotes" /> says what was changed, and
    ///         <see cref="FFMpegArgumentProcessor.Arguments" /> shows the command that actually ran.
    ///     </para>
    ///     <para>
    ///         libaom is much slower than SVT-AV1 and its rate factor does not give identical quality, so this keeps
    ///         AV1 work going on a machine without SVT-AV1; it is not a like-for-like substitute.
    ///     </para>
    /// </summary>
    public bool AllowAv1EncoderFallback { get; set; }

    /// <summary>
    ///     How long, after ffmpeg exits, an output pipe's sink (<see cref="Pipes.IPipeSink" />) may take to read the
    ///     rest of the pipe. Default 30 seconds.
    ///     <para>
    ///         When ffmpeg exits, the run waits for each output pipe's sink to read the pipe to its end, so a sink slower
    ///         than ffmpeg still receives every byte. This bounds that wait, counted from ffmpeg's exit: a sink that has
    ///         not finished by then is cancelled, its pipe is disposed, and the run throws
    ///         <see cref="Exceptions.FFMpegException" /> (Type = Process, "An output pipe sink did not drain ...") rather
    ///         than returning short output.
    ///     </para>
    ///     <para>
    ///         It does NOT delay a cancel: cancelling the run (<see cref="FFMpegArgumentProcessor.CancellableThrough(CancellationToken, int)" />)
    ///         tears the sink down at once - its token is cancelled and its pipe disposed - and the run ends with an
    ///         <see cref="OperationCanceledException" /> carrying the caller's token, even when the sink ignores its
    ///         token and fails on the disposed pipe instead.
    ///     </para>
    ///     <para>
    ///         Must be <see cref="TimeSpan.Zero" /> or more (at most <see cref="int.MaxValue" /> milliseconds), or
    ///         <see cref="Timeout.InfiniteTimeSpan" /> to wait as long as the sink takes; any other value fails the run
    ///         with <see cref="ArgumentOutOfRangeException" /> before ffmpeg starts.
    ///     </para>
    /// </summary>
    public TimeSpan OutputPipeDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    object ICloneable.Clone()
    {
        return Clone();
    }

    /// <summary>
    ///     Creates a new object that is a copy of the current instance.
    /// </summary>
    public FFOptions Clone()
    {
        return (FFOptions)MemberwiseClone();
    }
}
