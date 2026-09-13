================================================================================
AGENT-README: CodeBrix.VideoProcessing
A Guide for AI Coding Agents - CONSUMING the
CodeBrix.VideoProcessing.MitLicenseForever NuGet package
================================================================================


OVERVIEW
========
CodeBrix.VideoProcessing is a fully managed, cross-platform FFmpeg / FFprobe
wrapper. It lets you analyse media (durations, streams, codecs, resolutions,
bitrates, frames, packets, chapters, tags), convert / transcode / mux video and
audio, extract snapshots and animated GIFs, pipe raw frames or byte streams into
and out of FFmpeg, build FFmpeg metadata, and bridge video frames to and from
in-memory images.

Target framework: .NET 10 or later. The assembly is 100% managed C#.

IMPORTANT - THIS IS A WRAPPER, NOT A CODEC. At runtime it launches the external
`ffmpeg` and `ffprobe` executables as child processes and parses their output.
Those binaries MUST be installed and discoverable at runtime:
  * on the system PATH (the fallback), or
  * in a folder configured through FFOptions.BinaryFolder (see
    "LOCATING THE FFMPEG BINARIES" below).
The package does NOT bundle the FFmpeg binaries and does NOT P/Invoke into
libav*. If ffmpeg is missing you get an FFMpegException ("ffmpeg was not found
on your system"); if ffprobe is missing you get an FFProbeException.

Provenance: this library is a port of the FFMpegCore library (MIT, by Malte
Rosenbjerg, Vlad Jerca, Max Bagryantsev), with the Instances process wrapper
(MIT) vendored directly in. Namespaces were renamed
FFMpegCore.* -> CodeBrix.VideoProcessing.* and Instances[.Exceptions] ->
CodeBrix.VideoProcessing.Instances[.Exceptions]. DO NOT write the upstream
namespaces in code that consumes this package, and do not add a package
reference to FFMpegCore or Instances - everything is in this one assembly.
The FFMpegCore.Extensions.* companion projects (SkiaSharp,
System.Drawing.Common, Downloader) were NOT ported; their image helpers are
replaced by the CodeBrix.Imaging bridge described below.


INSTALLATION
============
PackageId:  CodeBrix.VideoProcessing.MitLicenseForever

    dotnet add package CodeBrix.VideoProcessing.MitLicenseForever

The package id carries the ".MitLicenseForever" license suffix; the namespace
you write in code is just "CodeBrix.VideoProcessing".

License: MIT (the package incorporates MIT-licensed FFMpegCore and Instances
source; see THIRD-PARTY-NOTICES.txt shipped in the package).

NuGet dependencies (one):
  * CodeBrix.Imaging.ApacheLicenseForever (Apache-2.0) - a fully managed,
    cross-platform, dependency-free CodeBrix imaging library. It is used only by
    the CodeBrix.VideoProcessing.Imaging bridge (FFMpegImage, ImageExtensions,
    ImageVideoFrameWrapper). Everything else works without touching it.

System.Text.Json comes from the .NET shared framework, so it is not a separate
package reference.

RUNTIME REQUIREMENT: the `ffmpeg` and `ffprobe` executables. On Debian-based
Linux, `sudo apt install ffmpeg` installs both. On Windows and macOS, install
them yourself and either put them on PATH or point FFOptions.BinaryFolder at the
folder that holds them. There is no auto-downloader in this package.


KEY NAMESPACES / USINGS
=======================
    using CodeBrix.VideoProcessing;
        // FFMpeg, FFProbe, FFMpegArguments, FFMpegArgumentOptions,
        // FFMpegArgumentProcessor, FFMpegGlobalArguments,
        // FFMpegMultiOutputOptions, FFMpegArgumentsBase, FFOptions,
        // GlobalFFOptions, FFMetadataBuilder, SnapshotArgumentBuilder,
        // IMediaAnalysis, MediaFormat, MediaStream, VideoStream, AudioStream,
        // SubtitleStream, FFProbeAnalysis, FFProbeStream, Format, Chapter,
        // ITagsContainer, IDispositionContainer, TagExtensions,
        // DispositionExtensions, FFProbeFrames, FFProbeFrameAnalysis,
        // FFProbePackets, FFProbePacketAnalysis, MediaAnalysisUtils,
        // ProcessArgumentsExtensions

    using CodeBrix.VideoProcessing.Arguments;
        // IArgument, IPreEscapedFilterArgument and every concrete ffmpeg
        // argument type, plus VideoFilterOptions / AudioFilterOptions and the
        // filter option builders (DrawTextOptions, PadOptions,
        // SubtitleHardBurnOptions, StyleOptions, ZScaleOptions) and the
        // VerbosityLevel enum

    using CodeBrix.VideoProcessing.Enums;
        // VideoCodec, AudioCodec, VideoType, Codec, CodecType, ContainerFormat,
        // PixelFormat, VideoSize, Speed, AudioQuality, FileExtension,
        // FFMpegLogLevel, Mirroring, Transposition, Channel, Filter,
        // HardwareAccelerationDevice, FeatureStatus, Disposition,
        // Lut3DInterpolation, TonemapAlgorithm, and the zscale constant
        // classes ZScaleTransfer, ZScalePrimaries, ZScaleMatrix, ZScaleRange,
        // ZScaleDither

    using CodeBrix.VideoProcessing.Builders.MetaData;
        // MetaDataBuilder, MetaData, ReadOnlyMetaData, IReadOnlyMetaData,
        // ChapterData, MetaDataSerializer

    using CodeBrix.VideoProcessing.Pipes;
        // IPipeSource, IPipeSink, IVideoFrame, IAudioSample,
        // RawVideoPipeSource, RawAudioPipeSource, StreamPipeSource,
        // StreamPipeSink

    using CodeBrix.VideoProcessing.Extend;
        // PcmAudioSampleWrapper, TimeSpanExtensions, UriExtensions

    using CodeBrix.VideoProcessing.Exceptions;
        // FFMpegException, FFMpegExceptionType, FFMpegStreamFormatException,
        // FFMpegEncoderNotFoundException,
        // FFMpegArgumentException, FFOptionsException, FFProbeException,
        // FFProbeProcessException, FormatNullException

    using CodeBrix.VideoProcessing.Helpers;
        // FFMpegHelper, FFProbeHelper

    using CodeBrix.VideoProcessing.Imaging;      // needs CodeBrix.Imaging
        // FFMpegImage, ImageExtensions, ImageVideoFrameWrapper

    using CodeBrix.VideoProcessing.Instances;    // vendored process wrapper
    using CodeBrix.VideoProcessing.Instances.Exceptions;
        // Instance, ProcessArguments, ProcessArgumentsExtensions,
        // ProcessInstance, ProcessResult, IProcessInstance, IProcessResult,
        // InstanceException, InstanceFileNotFoundException,
        // InstanceProcessAlreadyExitedException

Note: `Size` in this API is System.Drawing.Size (the struct from the shared
framework), not a CodeBrix type. Add `using System.Drawing;` wherever a Size
appears in a signature.


QUICK START
===========
Analyse a media file:

    using CodeBrix.VideoProcessing;

    IMediaAnalysis info = FFProbe.Analyse("input.mp4");
    Console.WriteLine(info.Duration);
    Console.WriteLine(info.PrimaryVideoStream?.CodecName);
    Console.WriteLine($"{info.PrimaryVideoStream?.Width}x{info.PrimaryVideoStream?.Height}");

Transcode a video (fluent builder):

    using CodeBrix.VideoProcessing;
    using CodeBrix.VideoProcessing.Enums;

    FFMpegArguments
        .FromFileInput("input.webm")
        .OutputToFile("output.mp4", overwrite: true, opt => opt
            .WithVideoCodec(VideoCodec.LibX264)
            .WithConstantRateFactor(21)
            .WithAudioCodec(AudioCodec.Aac)
            .WithVideoFilters(f => f.Scale(VideoSize.Hd)))
        .ProcessSynchronously();

Snapshot to a file, and to an in-memory image:

    using System.Drawing;
    using CodeBrix.VideoProcessing;
    using CodeBrix.VideoProcessing.Imaging;   // the image bridge

    FFMpeg.Snapshot("input.mp4", "thumb.png",
        new Size(1920, 1080), TimeSpan.FromSeconds(10));

    using CodeBrix.Imaging.Image image =
        FFMpegImage.Snapshot("input.mp4", captureTime: TimeSpan.FromSeconds(10));

Render the command line WITHOUT running ffmpeg (great for unit tests):

    string cmd = FFMpegArguments
        .FromFileInput("input.mp4")
        .OutputToFile("output.mp4", false, opt => opt.WithSpeedPreset(Speed.Fast))
        .Arguments;
    // -i "input.mp4" -preset fast "output.mp4"


HOW IT WORKS (EXECUTION MODEL)
==============================
A conversion is built as a data structure, rendered to an ffmpeg command line,
executed as a child process, and its stdout/stderr are parsed. End to end:

  (a) BUILD INPUTS. FFMpegArguments is the root builder. A static factory
      (FromFileInput / FromPipeInput / FromUrlInput / FromDeviceInput /
      FromConcatInput / FromDemuxConcatInput) creates it with one input;
      instance AddXxxInput methods append more inputs; WithGlobalOptions adds
      pre-input global flags. Internally it holds an ordered List<IArgument>;
      each IArgument knows how to render its own ffmpeg text.

  (b) BUILD OUTPUT + OPTIONS. OutputToFile / OutputToPipe / OutputToUrl /
      OutputToTee / MultiOutput take an Action<FFMpegArgumentOptions> callback.
      FFMpegArgumentOptions is the OUTPUT-side builder: every WithXxx / Using /
      Select / Resize / Seek / ForceFormat call appends an IArgument. The
      terminal call returns an FFMpegArgumentProcessor - the object that runs
      ffmpeg.

  (c) RENDER. Argument text is produced by concatenating global arguments first,
      then the ordered input/output arguments, separated by single spaces. An
      argument that also implements IDynamicArgument (MapMetadataArgument,
      MetaDataArgument) is asked for its text with the whole argument list as
      context, so it can compute the correct `-map_metadata <index>`.
      FFMpegArguments.Text and FFMpegArgumentProcessor.Arguments both expose the
      fully rendered command line - use it to unit-test without running ffmpeg.

  (d) EXECUTE. ProcessSynchronously() / ProcessAsynchronously() resolve the
      ffmpeg binary path (GlobalFFOptions.GetFFMpegBinaryPath), verify it runs
      (`ffmpeg -version`), build a ProcessArguments (the vendored Instances
      type) and Start() it. The process layer redirects stdin/stdout/stderr,
      streams output line-by-line, raises Exited / OutputDataReceived /
      ErrorDataReceived, and supports WaitForExit / WaitForExitAsync / Kill /
      SendInput. Before the process starts, every IInputOutputArgument gets
      Pre() (temp metadata/concat files are written, pipes are created); while
      it runs, their During(token) tasks pump the pipes; after it exits, Post()
      cleans up. A non-zero exit code throws FFMpegException with
      Type = FFMpegExceptionType.Process and the captured stderr in
      FFMpegErrorOutput, unless you pass throwOnError: false.
      When that stderr says ffmpeg does not have an encoder the command asked
      for ("Unknown encoder '...'"), the exception is the FFMpegException
      subclass FFMpegEncoderNotFoundException. For libsvtav1 its message starts
      with an explanation and the FFOptions.AllowAv1EncoderFallback suggestion,
      followed by the usual text in full - see AV1 ENCODING WITHOUT SVT-AV1.
      If ffmpeg exiting early also broke a pipe that was still being written
      into, the resulting IOException ("Pipe is broken") is attached as the
      InnerException of that FFMpegException rather than replacing it, so the
      message you see is always ffmpeg's own explanation of what went wrong.

  (e) PROGRESS + LOGGING. NotifyOnProgress / NotifyOnOutput / NotifyOnError
      register callbacks. Progress is parsed out of ffmpeg's stderr with the
      regex `time=(\d\d:\d\d:\d\d.\d\d?)`; the percentage form needs the total
      duration you pass in. WithLogLevel(FFMpegLogLevel) appends `-v <level>` to
      the rendered arguments for that one run; FFOptions.LogLevel is the global
      default; WithGlobalOptions(o => o.WithVerbosityLevel(...)) emits a
      pre-input `-loglevel` (and `-hide_banner` for levels below Info).

  (f) CANCELLATION. See the next section.


CANCELLATION MODEL
==================
FFMpegArgumentProcessor.CancellableThrough(...) wires cancellation:

    FFMpegArgumentProcessor CancellableThrough(out Action cancel, int timeout = 0)
    FFMpegArgumentProcessor CancellableThrough(CancellationToken token, int timeout = 0)

When triggered (the returned Action is invoked, or the token is cancelled), the
processor runs, on the cancelling thread:

    1. instance.SendInput("q")     // ask ffmpeg to quit GRACEFULLY
    2. wait up to `timeout` milliseconds for the process to exit on its own
    3. if it has not exited: cancel the internal token source and
       instance.Kill()

With the default timeout = 0, step 2 returns immediately and the process is
killed. A non-zero timeout gives ffmpeg a grace period to flush a valid output
file before being force-killed. Exceptions thrown by the process layer because
the process already exited (InstanceProcessAlreadyExitedException) or was
disposed are swallowed.

If cancellation happened BEFORE the process started, ProcessSynchronously /
ProcessAsynchronously throw OperationCanceledException("cancelled before
starting processing"). If it happened during the run, they throw
OperationCanceledException("ffmpeg processing was cancelled") - but only when
throwOnError is true (the default); with throwOnError: false they return false.

The token overload also calls token.ThrowIfCancellationRequested() immediately,
so passing an already-cancelled token throws at wiring time.

KNOWN ENVIRONMENTAL ISSUE: on some hosts / ffmpeg builds the graceful
SendInput("q") step does not return, so runtime cancellation of a running ffmpeg
can hang before reaching the Kill(). The cancellation code is a faithful port of
upstream and the vendored process Kill/SendInput primitives pass their own unit
tests, so this is an ffmpeg-interaction issue. If you rely on runtime
cancellation, pass a non-zero `timeout` and verify behaviour on your target
ffmpeg build.


PIPE MODEL
==========
Pipes let you feed data into ffmpeg or read its output in-process, with no
temporary files. Under the hood the library creates an OS named pipe (Windows:
\\.\pipe\<name>; elsewhere a unix: socket path under the temp folder), passes
its path to ffmpeg as the input/output "file", and copies bytes on a background
task while ffmpeg runs.

  INPUT sources (IPipeSource - GetStreamArguments(), WriteAsync(Stream, token)):
    * RawVideoPipeSource(IEnumerable<IVideoFrame> frames)
        Properties: string StreamFormat { get; }  int Width { get; }
                    int Height { get; }  double FrameRate { get; set; } = 25
        It reads the FIRST frame to fix the stream format, then emits
        `-f rawvideo -r {FrameRate} -pix_fmt {StreamFormat} -s {Width}x{Height}`
        and validates that EVERY subsequent frame has the same Width, Height and
        Format - a mismatch throws FFMpegStreamFormatException BEFORE the
        offending frame is serialized. An empty enumerable throws
        InvalidOperationException("Enumerator is empty, unable to get frame").
    * RawAudioPipeSource(IEnumerable<IAudioSample>) and
      RawAudioPipeSource(IEnumerator<IAudioSample>)
        Properties: string Format { get; set; } = "s16le"
                    uint SampleRate { get; set; } = 8000
                    uint Channels { get; set; } = 1
        Emits `-f {Format} -ar {SampleRate} -ac {Channels}`.
    * StreamPipeSource(Stream source)
        Properties: Stream Source { get; }  int BlockSize { get; } = 4096
                    string StreamFormat { get; } = ""
        Copies an arbitrary byte Stream in. Contributes no format flags of its
        own, so set the input format with the input-options callback if ffmpeg
        cannot sniff it.

  OUTPUT sinks (IPipeSink - ReadAsync(Stream, token), GetFormat()):
    * StreamPipeSink(Stream destination)
      StreamPipeSink(Func<Stream, CancellationToken, Task> writer)
        Properties: Func<Stream, CancellationToken, Task> Writer { get; }
                    int BlockSize { get; set; } = 4096
                    string Format { get; set; } = ""
        Captures ffmpeg output into a Stream (or hands you the stream to read
        yourself). Set the OUTPUT container format with ForceFormat(...) in the
        output options - ffmpeg cannot infer a format from a pipe path.

  Frame / sample contracts:
    public interface IVideoFrame
    {
        int Width { get; }
        int Height { get; }
        string Format { get; }              // an ffmpeg pix_fmt name
        void Serialize(Stream pipe);
        Task SerializeAsync(Stream pipe, CancellationToken token);
    }

    public interface IAudioSample
    {
        void Serialize(Stream stream);
        Task SerializeAsync(Stream stream, CancellationToken token);
    }

  Shipped implementations: ImageVideoFrameWrapper (IVideoFrame, in the Imaging
  namespace) and PcmAudioSampleWrapper (IAudioSample, in the Extend namespace).
  Write your own for any other pixel or sample layout.

Use FromPipeInput(IPipeSource) / AddPipeInput(IPipeSource) and
OutputToPipe(IPipeSink, Action<FFMpegArgumentOptions>).


CORE API REFERENCE
==================

FFProbe (static) - media analysis
---------------------------------
    IMediaAnalysis Analyse(string filePath, FFOptions ffOptions = null,
                           string customArguments = null)
    IMediaAnalysis Analyse(Uri uri, FFOptions ffOptions = null,
                           string customArguments = null)
    IMediaAnalysis Analyse(Stream stream, FFOptions ffOptions = null,
                           string customArguments = null)
    Task<IMediaAnalysis> AnalyseAsync(string filePath, FFOptions ffOptions = null,
                           CancellationToken cancellationToken = default,
                           string customArguments = null)
    Task<IMediaAnalysis> AnalyseAsync(Uri uri, ... same trailing parameters)
    Task<IMediaAnalysis> AnalyseAsync(Stream stream, ... same trailing parameters)

    FFProbeFrames GetFrames(string filePath, FFOptions ffOptions = null,
                            string customArguments = null)
    FFProbeFrames GetFrames(Uri uri, FFOptions ffOptions = null,
                            string customArguments = null)
    Task<FFProbeFrames> GetFramesAsync(string filePath, FFOptions ffOptions = null,
                            CancellationToken cancellationToken = default,
                            string customArguments = null)
    Task<FFProbeFrames> GetFramesAsync(Uri uri, ... same trailing parameters)

    FFProbePackets GetPackets(string filePath, FFOptions ffOptions = null,
                            string customArguments = null)
    Task<FFProbePackets> GetPacketsAsync(string filePath, FFOptions ffOptions = null,
                            CancellationToken cancellationToken = default,
                            string customArguments = null)

  NOTE the parameter ORDER on the async overloads: cancellationToken comes
  THIRD, before customArguments. Call it by name -
  `FFProbe.AnalyseAsync(path, cancellationToken: token)`.

  `customArguments` is appended to the ffprobe command line, so you can add
  probe flags (e.g. "-probesize 50M -analyzeduration 100M") without leaving the
  API.

  ERRORS from FFProbe - note that most of them are FFMpegException, not
  FFProbeException:
    * a filePath that does not exist ->
      FFMpegException(FFMpegExceptionType.File, "No file found at '...'")
    * ffprobe not installed / not runnable ->
      FFProbeException("ffprobe was not found on your system")
    * a non-zero ffprobe exit code ->
      FFMpegException(FFMpegExceptionType.Process, ...) with the stderr in
      FFMpegErrorOutput
    * a response with no "format" object -> FormatNullException
      (which derives from FFProbeException)
  The Uri and Stream overloads do no up-front existence check, so a bad URI
  surfaces as the non-zero-exit-code case.

FFProbe RESULT MODEL
--------------------
IMediaAnalysis is the friendly, typed view returned by Analyse/AnalyseAsync
(the implementing class is internal - always hold the interface):

    public interface IMediaAnalysis
    {
        TimeSpan Duration { get; }             // max of format/video/audio
        MediaFormat Format { get; }
        List<ChapterData> Chapters { get; }
        AudioStream PrimaryAudioStream { get; }      // lowest Index, or null
        VideoStream PrimaryVideoStream { get; }
        SubtitleStream PrimarySubtitleStream { get; }
        List<VideoStream> VideoStreams { get; }
        List<AudioStream> AudioStreams { get; }
        List<SubtitleStream> SubtitleStreams { get; }
        IReadOnlyList<string> ErrorData { get; }     // ffprobe stderr lines
    }

    public class MediaFormat : ITagsContainer
    {
        TimeSpan Duration; TimeSpan StartTime;
        string FormatName; string FormatLongName;
        int StreamCount; double ProbeScore; double BitRate;
        Dictionary<string, string> Tags;
    }

    public abstract class MediaStream : ITagsContainer
    {
        int Index; string CodecName; string CodecLongName;
        string CodecTagString; string CodecTag;
        long BitRate; TimeSpan StartTime; TimeSpan Duration;
        string Language; Dictionary<string, bool> Disposition;
        int? BitDepth; Dictionary<string, string> Tags;
        Codec GetCodecInfo();      // == FFMpeg.GetCodec(CodecName)
    }

    public class VideoStream : MediaStream
    {
        double AvgFrameRate; int BitsPerRawSample;
        (int Width, int Height) DisplayAspectRatio;
        (int Width, int Height) SampleAspectRatio;
        string Profile; int Width; int Height;
        double FrameRate; string PixelFormat; int Level; int Rotation;
        double AverageFrameRate;
        string ColorRange; string ColorSpace;
        string ColorTransfer; string ColorPrimaries;
        PixelFormat GetPixelFormatInfo();   // == FFMpeg.GetPixelFormat(PixelFormat)
    }

    public class AudioStream : MediaStream
    {
        int Channels; string ChannelLayout;
        int SampleRateHz;                    // note: NOT "SampleRate"
        string Profile;
    }

    public class SubtitleStream : MediaStream { }   // no extra members

    public class ChapterData          // CodeBrix.VideoProcessing.Builders.MetaData
    {
        ChapterData(string title, TimeSpan start, TimeSpan end);
        string Title { get; }  TimeSpan Start { get; }  TimeSpan End { get; }
        TimeSpan Duration { get; }        // End - Start
    }

  On VideoStream, note that BOTH `FrameRate` (from ffprobe's r_frame_rate) and
  `AvgFrameRate`/`AverageFrameRate` (from avg_frame_rate) are populated;
  `Rotation` is derived from the rotate tag / display-matrix side data, and the
  snapshot builder swaps width and height when |Rotation| is 90 or 180.

RAW ffprobe JSON MODEL (what the wire format deserializes into)
---------------------------------------------------------------
These types are public so you can reach fields the friendly model does not
surface. Analyse() maps them into IMediaAnalysis internally.

    public class FFProbeAnalysis
    {
        List<FFProbeStream> Streams;   // "streams"
        Format Format;                 // "format"
        List<Chapter> Chapters;        // "chapters"
        IReadOnlyList<string> ErrorData;   // [JsonIgnore]
    }

    public class FFProbeStream : ITagsContainer, IDispositionContainer
        int Index; string AvgFrameRate; string BitsPerRawSample;
        int BitsPerSample; string BitRate; int? Channels; string ChannelLayout;
        string CodecType; string CodecName; string CodecLongName;
        string CodecTag; string CodecTagString;
        string DisplayAspectRatio; string SampleAspectRatio;
        string StartTime; string Duration; string Profile;
        int? Width; int? Height; string FrameRate;   // "r_frame_rate"
        string PixelFormat; int Level; string SampleRate;
        List<Dictionary<string, JsonValue>> SideData;
        string ColorRange; string ColorSpace; string ColorTransfer;
        string ColorPrimaries;
        Dictionary<string, int> Disposition;
        Dictionary<string, string> Tags;

    public class Format : ITagsContainer
        string Filename; int NbStreams; int NbPrograms;
        string FormatName; string FormatLongName; string StartTime;
        string Duration; string Size; string BitRate; int ProbeScore;
        Dictionary<string, string> Tags;

    public class Chapter : ITagsContainer
        long Id; string TimeBase; long Start; string StartTime;
        long End; string EndTime; Dictionary<string, string> Tags;

    public interface ITagsContainer      { Dictionary<string, string> Tags { get; set; } }
    public interface IDispositionContainer { Dictionary<string, int> Disposition { get; set; } }

    public static class TagExtensions          // extension methods on ITagsContainer
        string GetLanguage(this ITagsContainer)        // Tags["language"]
        string GetCreationTime(this ITagsContainer)    // Tags["creation_time"]
        string GetRotate(this ITagsContainer)          // Tags["rotate"]
        string GetDuration(this ITagsContainer)        // Tags["duration"]

    public static class DispositionExtensions   // on IDispositionContainer
        int? GetDefault(this IDispositionContainer)    // Disposition["default"]
        int? GetForced(this IDispositionContainer)     // Disposition["forced"]

  All six return null when the key is absent, so they are safe on a stream with
  no tags. MediaFormat and MediaStream implement ITagsContainer too, so
  `analysis.Format.GetCreationTime()` and
  `analysis.PrimarySubtitleStream.GetLanguage()` both work. Note that
  IDispositionContainer is implemented by FFProbeStream (int values), while the
  friendly MediaStream exposes Disposition as Dictionary<string, bool> - so
  GetDefault()/GetForced() apply to FFProbeStream, not to MediaStream.

FRAMES AND PACKETS
------------------
    public class FFProbeFrames  { List<FFProbeFrameAnalysis> Frames; }
    public class FFProbePackets { List<FFProbePacketAnalysis> Packets; }

    public class FFProbeFrameAnalysis
        string MediaType; int StreamIndex; int KeyFrame;
        long PacketPts; string PacketPtsTime;
        long PacketDts; string PacketDtsTime;
        long BestEffortTimestamp; string BestEffortTimestampTime;
        int PacketDuration; string PacketDurationTime;
        long PacketPos; int PacketSize;
        long Width; long Height; string PixelFormat; string PictureType;
        long CodedPictureNumber; long DisplayPictureNumber;
        int InterlacedFrame; int TopFieldFirst; int RepeatPicture;
        string ChromaLocation;

    public class FFProbePacketAnalysis
        string CodecType; int StreamIndex;
        long Pts; string PtsTime; long Dts; string DtsTime;
        int Duration; string DurationTime;
        int Size; long Pos; string Flags;

  Both are deserialized with NumberHandling = AllowReadingFromString, because
  ffprobe emits several of these numbers as JSON strings. The *Time properties
  stay strings (ffprobe emits fractional seconds); parse them yourself or use
  MediaAnalysisUtils.ParseDuration.

    public static class MediaAnalysisUtils      // parsing helpers, all public
        double DivideRatio((double, double) ratio)
        (int, int) ParseRatioInt(string input, char separator)
        (double, double) ParseRatioDouble(string input, char separator)
        double ParseDoubleInvariant(string line)
        int ParseIntInvariant(string line)
        long ParseLongInvariant(string line)
        TimeSpan ParseDuration(string duration)
        int ParseRotation(FFProbeStream fFProbeStream)
        Dictionary<string, bool> FormatDisposition(Dictionary<string, int> disposition)

FFMpeg (static) - one-shot conveniences
---------------------------------------
Each of these builds and runs a complete conversion for you. They all call
FFProbe first where they need the source's size or duration.

    bool Snapshot(string input, string output, Size? size = null,
                  TimeSpan? captureTime = null, int? streamIndex = null,
                  int inputFileIndex = 0)
    Task<bool> SnapshotAsync(string input, string output, Size? size = null,
                  TimeSpan? captureTime = null, int? streamIndex = null,
                  int inputFileIndex = 0, CancellationToken cancellationToken = default)
        Output extension must be one of .png .jpg .bmp .webp
        (FileExtension.Image.All) or an FFMpegException is thrown.
        captureTime defaults to one third of the source duration.

    bool GifSnapshot(string input, string output, Size? size = null,
                  TimeSpan? captureTime = null, TimeSpan? duration = null,
                  int? streamIndex = null)
    Task<bool> GifSnapshotAsync(string input, string output, Size? size = null,
                  TimeSpan? captureTime = null, TimeSpan? duration = null,
                  int? streamIndex = null, CancellationToken cancellationToken = default)
        Output extension must be .gif. Default output width is 480 (height
        auto), default palette fps is 12.

    bool SubVideo(string input, string output, TimeSpan startTime, TimeSpan endTime)
    Task<bool> SubVideoAsync(string input, string output, TimeSpan startTime,
                  TimeSpan endTime, CancellationToken cancellationToken = default)

    bool Convert(string input, string output, ContainerFormat format,
                 Speed speed = Speed.SuperFast,
                 VideoSize size = VideoSize.Original,
                 AudioQuality audioQuality = AudioQuality.Normal,
                 bool multithreaded = false)
        The output extension must match format.Extension. Both source
        dimensions must be even (yuv420p requirement) or ArgumentException is
        thrown by the internal size check.

    bool Join(string output, params string[] videos)
    bool JoinImageSequence(string output, double frameRate = 30, params string[] images)
        JoinImageSequence requires every image to share one extension.
    bool PosterWithAudio(string image, string audio, string output)
    bool Mute(string input, string output)
    bool ExtractAudio(string input, string output)
    bool ReplaceAudio(string input, string inputAudio, string output,
                      bool stopAtShortest = false)
    bool SaveM3U8Stream(Uri uri, string output)

  ffmpeg capability catalogue (parsed from `ffmpeg -codecs`, `-encoders`,
  `-decoders`, `-pix_fmts` and `-formats`; cached process-wide when
  FFOptions.UseCache is true, which is the default):

    IReadOnlyList<Codec> GetCodecs()
    IReadOnlyList<Codec> GetCodecs(CodecType type)
    IReadOnlyList<Codec> GetVideoCodecs() / GetAudioCodecs()
                       / GetSubtitleCodecs() / GetDataCodecs()
    bool TryGetCodec(string name, out Codec codec)
    Codec GetCodec(string name)                  // throws if unsupported

    IReadOnlyList<PixelFormat> GetPixelFormats()
    bool TryGetPixelFormat(string name, out PixelFormat format)
    PixelFormat GetPixelFormat(string name)      // throws if unsupported

    IReadOnlyList<ContainerFormat> GetContainerFormats()
    bool TryGetContainerFormat(string name, out ContainerFormat fmt)
    ContainerFormat GetContainerFormat(string name)   // throws if unsupported

FFMpegArguments - the INPUT side
--------------------------------
    public sealed class FFMpegArguments : FFMpegArgumentsBase

  Static factories (start a chain; `addArguments` configures INPUT-side options
  such as Seek, and every one of them defaults to null):

    static FFMpegArguments FromFileInput(string filePath, bool verifyExists = true,
                              Action<FFMpegArgumentOptions> addArguments = null)
    static FFMpegArguments FromFileInput(IEnumerable<string> filePath,
                              bool verifyExists = true,
                              Action<FFMpegArgumentOptions> addArguments = null)
    static FFMpegArguments FromFileInput(FileInfo fileInfo,
                              Action<FFMpegArgumentOptions> addArguments = null)
    static FFMpegArguments FromUrlInput(Uri uri,
                              Action<FFMpegArgumentOptions> addArguments = null)
    static FFMpegArguments FromDeviceInput(string device,
                              Action<FFMpegArgumentOptions> addArguments = null)
    static FFMpegArguments FromPipeInput(IPipeSource sourcePipe,
                              Action<FFMpegArgumentOptions> addArguments = null)
    static FFMpegArguments FromConcatInput(IEnumerable<string> filePaths,
                              Action<FFMpegArgumentOptions> addArguments = null)
    static FFMpegArguments FromDemuxConcatInput(IEnumerable<string> filePaths,
                              Action<FFMpegArgumentOptions> addArguments = null)

  Instance methods (append more inputs; same parameter shapes):

    AddFileInput(string, bool verifyExists = true, Action<...> = null)
    AddFileInput(IEnumerable<string>, bool verifyExists = true, Action<...> = null)
    AddFileInput(FileInfo, Action<...> = null)
    AddUrlInput(Uri, Action<...> = null)
    AddDeviceInput(string device, Action<...> = null)
    AddPipeInput(IPipeSource, Action<...> = null)
    AddConcatInput(IEnumerable<string>, Action<...> = null)
    AddDemuxConcatInput(IEnumerable<string>, Action<...> = null)
    AddMetaData(string content, Action<...> = null)
    AddMetaData(FFMetadataBuilder metaDataBuilder, Action<...> = null)
    AddMetaData(IReadOnlyMetaData metaData, Action<...> = null)
    MapMetaData(int? inputIndex = null, Action<...> = null)
    WithGlobalOptions(Action<FFMpegGlobalArguments> configureOptions)

    string Text { get; }        // the fully rendered command line

  Terminals (each returns an FFMpegArgumentProcessor):

    FFMpegArgumentProcessor OutputToFile(string file, bool overwrite = true,
                              Action<FFMpegArgumentOptions> addArguments = null)
    FFMpegArgumentProcessor OutputToUrl(string uri, Action<...> = null)
    FFMpegArgumentProcessor OutputToUrl(Uri uri, Action<...> = null)
    FFMpegArgumentProcessor OutputToPipe(IPipeSink reader, Action<...> = null)
    FFMpegArgumentProcessor OutputToTee(Action<FFMpegMultiOutputOptions> addOutputs,
                              Action<FFMpegArgumentOptions> addArguments = null)
    FFMpegArgumentProcessor MultiOutput(Action<FFMpegMultiOutputOptions> addOutputs)

  FromFileInput/AddFileInput with verifyExists: true check the file only when
  the conversion STARTS (the argument's Pre() step), and the failure is a
  System.IO.FileNotFoundException - not an FFMpegException. Pass false when you
  are only rendering a command line for a test. The FileInfo and Uri overloads
  always use verifyExists: false, so they never check.

FFMpegGlobalArguments - pre-input global flags
----------------------------------------------
    public sealed class FFMpegGlobalArguments : FFMpegArgumentsBase
        FFMpegGlobalArguments WithVerbosityLevel(
            VerbosityLevel verbosityLevel = VerbosityLevel.Error)

  That is the whole public surface - it has exactly one method. Global arguments
  are rendered BEFORE every input argument. VerbosityLevel values are
  Quiet = -8, Fatal = 8, Error = 16, Warning = 24, Info = 32, Verbose = 40,
  Debug = 48, Trace = 56; any level below Info (32) also emits `-hide_banner`.
  VerbosityLevel lives in CodeBrix.VideoProcessing.Arguments and is a DIFFERENT
  type from FFMpegLogLevel (CodeBrix.VideoProcessing.Enums), which is what
  WithLogLevel and FFOptions.LogLevel take.

FFMpegMultiOutputOptions - several outputs from one decode
----------------------------------------------------------
    public class FFMpegMultiOutputOptions
        IEnumerable<IArgument> Arguments { get; }
        FFMpegMultiOutputOptions OutputToFile(string file, bool overwrite = true,
                                  Action<FFMpegArgumentOptions> addArguments = null)
        FFMpegMultiOutputOptions OutputToUrl(string uri, Action<...> = null)
        FFMpegMultiOutputOptions OutputToUrl(Uri uri, Action<...> = null)
        FFMpegMultiOutputOptions OutputToPipe(IPipeSink reader, Action<...> = null)
        FFMpegMultiOutputOptions AddOutput(IOutputArgument argument,
                                  Action<FFMpegArgumentOptions> addArguments)

  Two ways to use it, and they are NOT the same thing:
    * FFMpegArguments.MultiOutput(...) renders each output's options followed by
      its destination, one after another - ffmpeg's native multiple-output form.
      Each output re-encodes independently.
    * FFMpegArguments.OutputToTee(...) renders `-f tee "[opts]dest1|[opts]dest2"`
      - one encode, duplicated to several destinations by the tee muxer. Options
      set on a tee output become bracketed tee parameters, so only options the
      tee muxer accepts belong there.


FFMpegArgumentOptions - the OUTPUT side (all 63 methods)
--------------------------------------------------------
    public class FFMpegArgumentOptions : FFMpegArgumentsBase

Every method returns FFMpegArgumentOptions, so they chain. The same class is
also used for INPUT-side options (the `addArguments` callback on the input
factories) - which method makes sense on which side is an ffmpeg question, not a
C# one; Seek/EndSeek/WithDuration, AutoRotate and the Audible keys are the usual
input-side ones. The rendered flag each method produces is shown after the arrow.

AutoRotate deserves a warning of its own: ffmpeg only honours -autorotate /
-noautorotate when it appears BEFORE the -i it belongs to, so it must go in the
addArguments callback of FromFileInput / AddFileInput. The builder emits an
input's options ahead of its own -i, so that position is correct by
construction. On an output it renders, and ffmpeg ignores it.

  Codecs and bitrate
    WithVideoCodec(Codec videoCodec)                     -> -c:v {name}
    WithVideoCodec(string videoCodec)                    -> -c:v {name}
    WithAudioCodec(Codec audioCodec)                     -> -c:a {name}
    WithAudioCodec(string audioCodec)                    -> -c:a {name}
    WithCopyCodec()                                      -> -codec copy
    CopyChannel(Channel channel = Channel.Both)          -> -c:a copy / -c:v copy
    WithVideoBitrate(int bitrate)                        -> -b:v {n}k
    WithAudioBitrate(int bitrate)                        -> -b:a {n}k
    WithAudioBitrate(AudioQuality audioQuality)          -> -b:a {n}k
    WithConstantRateFactor(int crf)                      -> -crf {n}
    WithVariableBitrate(int vbr)                         -> -vbr {n}
    WithAudioSamplingRate(int samplingRate = 48000)      -> -ar {n}
    WithSpeedPreset(Speed speed)                         -> -preset {name}
    WithSpeedPreset(int preset)                          -> -preset {n}
    WithSubtitleCodec(Codec subtitleCodec)               -> -c:s {name}
    WithSubtitleCodec(string subtitleCodec)              -> -c:s {name}
    WithEncoderParams(string optionName,
        params (string Key, string Value)[] parameters)  -> -{name} k=v:k=v
    WithFramerate(double framerate)                      -> -r {n}

  Geometry
    Resize(int width, int height)                        -> -s {w}x{h}
    Resize(Size? size)                                   -> -s {w}x{h}
    Crop(Size? size, int left, int top)                  -> -vf crop=w:h:left:top
    Crop(int width, int height, int left, int top)       -> -vf crop=...
    Crop(Size? size)                                     -> -vf crop=w:h:0:0
    Crop(int width, int height)                          -> -vf crop=w:h:0:0

  Filters (see "FILTER OPTIONS" below for what goes inside)
    WithVideoFilters(Action<VideoFilterOptions> videoFilterOptions)  -> -vf "..."
    WithAudioFilters(Action<AudioFilterOptions> audioFilterOptions)  -> -af "..."

  Timing and stream selection
    Seek(TimeSpan? seekTo)                               -> -ss hh:mm:ss.fff
    EndSeek(TimeSpan? seekTo)                            -> -to hh:mm:ss.fff
    WithDuration(TimeSpan? duration)                     -> -t {duration}
    Loop(int times)                                      -> -loop {n}
    WithFrameOutputCount(int frames)                     -> -vframes {n}
    WithStartNumber(int startNumber)                     -> -start_number {n}
    SelectStream(int streamIndex, int inputFileIndex = 0,
                 Channel channel = Channel.All)          -> -map {i}{:type}:{n}
    SelectStreams(IEnumerable<int> streamIndices, int inputFileIndex = 0,
                 Channel channel = Channel.All)          -> one -map each
    DeselectStream(int streamIndex, int inputFileIndex = 0,
                 Channel channel = Channel.All)          -> -map -{i}...
    DeselectStreams(IEnumerable<int> streamIndices, int inputFileIndex = 0,
                 Channel channel = Channel.All)          -> one -map - each
    DisableChannel(Channel channel)                      -> -vn / -an

  Container, pixels, hardware
    ForceFormat(ContainerFormat format)                  -> -f {name}
    ForceFormat(string format)                           -> -f {format}
    ForcePixelFormat(PixelFormat pixelFormat)            -> -pix_fmt {name}
    ForcePixelFormat(string pixelFormat)                 -> -pix_fmt {name}
    WithFastStart()                                      -> -movflags faststart
    WithTagVersion(int id3v2Version = 3)                 -> -id3v2_version {n}
    WithHardwareAcceleration(HardwareAccelerationDevice
        hardwareAccelerationDevice = HardwareAccelerationDevice.Auto)
                                                         -> -hwaccel {name}
    WithBitStreamFilter(Channel channel, Filter filter)  -> -bsf:v / -bsf:a {name}
    WithGifPaletteArgument(int streamIndex, Size? size, double fps = 12)
                                                         -> -filter_complex "..."

  Threads, metadata and everything else
    UsingShortest(bool shortest = true)                  -> -shortest
    UsingMultithreading(bool multithread)                -> -threads {n or 1}
    UsingThreads(int threads)                            -> -threads {n}
    OverwriteExisting()                                  -> -y
    WithoutMetadata()                                    -> -map_metadata -1
    WithAudibleActivationBytes(string activationBytes)   -> -activation_bytes {v}
    WithAudibleEncryptionKeys(string key, string iv)     -> -audible_key .. -audible_iv ..
    WithMetadata(string key, string value)               -> -metadata k="v"
    WithStreamMetadata(int streamIndex,
        string key, string value)                        -> -metadata:s:N k="v"
    WithStreamMetadata(Channel channel, int streamIndex,
        string key, string value)                        -> -metadata:s:{t}:N k="v"
    WithDisposition(int streamIndex,
        params Disposition[] flags)                      -> -disposition:N a+b
    WithDisposition(Channel channel, int streamIndex,
        params Disposition[] flags)                      -> -disposition:{t}:N a+b
    CuesToFront(bool enabled = true)                     -> -cues_to_front 1|0
    ReserveIndexSpace(int bytes)                         -> -reserve_index_space {n}
    AutoRotate(bool enabled = true)                      -> -autorotate / -noautorotate
    WithCustomArgument(string argument)                  -> the string, verbatim
    WithArgument(IArgument argument)                     -> argument.Text

  UsingMultithreading(true) renders `-threads {Environment.ProcessorCount}`;
  UsingMultithreading(false) renders `-threads 1`.
  WithEncoderParams trims a leading '-' from optionName, so "svtav1-params" and
  "-svtav1-params" both render `-svtav1-params`. The pairs are joined k=v:k=v
  and the WHOLE parameter string is wrapped in double quotes when any part of it
  contains whitespace. The argument object it builds exposes OptionName and
  Parameters, so a test can read back exactly what was assembled.
  WithCustomArgument is the escape hatch for any flag this API does not model -
  it is inserted verbatim at that position in the command line, so you can pass
  several flags in one string ("-ac 2 -write_xing 0"). WithArgument is the same
  escape hatch at the object level: pass any IArgument, including one of the
  concrete argument classes below or your own implementation.

FFMpegArgumentProcessor - run and control
------------------------------------------
    public class FFMpegArgumentProcessor
        string Arguments { get; }         // the rendered command line
        bool Av1EncoderFallbackApplied { get; }
        IReadOnlyList<string> Av1EncoderFallbackNotes { get; }
                                          // see AV1 ENCODING WITHOUT SVT-AV1

        bool ProcessSynchronously(bool throwOnError = true,
                                  FFOptions ffMpegOptions = null)
        Task<bool> ProcessAsynchronously(bool throwOnError = true,
                                  FFOptions ffMpegOptions = null)

        FFMpegArgumentProcessor Configure(Action<FFOptions> configureOptions)
        FFMpegArgumentProcessor CancellableThrough(out Action cancel, int timeout = 0)
        FFMpegArgumentProcessor CancellableThrough(CancellationToken token,
                                                   int timeout = 0)
        FFMpegArgumentProcessor NotifyOnProgress(Action<double> onPercentageProgress,
                                                 TimeSpan totalTimeSpan)
        FFMpegArgumentProcessor NotifyOnProgress(Action<TimeSpan> onTimeProgress)
        FFMpegArgumentProcessor NotifyOnOutput(Action<string> onOutput)
        FFMpegArgumentProcessor NotifyOnError(Action<string> onError)
        FFMpegArgumentProcessor WithLogLevel(FFMpegLogLevel logLevel)

  The percentage overload REQUIRES the total duration as a second, non-optional
  argument (get it from FFProbe first); the TimeSpan overload takes only the
  callback. You may register both on the same processor. When the run finishes
  successfully the percentage callback is invoked one final time with 100.0, and
  the time callback with the total duration you supplied.

  Configure(Action<FFOptions>) applies to THIS run only: when no explicit
  FFOptions is passed to Process*, the processor clones GlobalFFOptions.Current
  and applies the configure actions to the clone, so globals are never mutated.
  Passing ffMpegOptions to Process* uses that instance as the base instead
  (and the Configure actions still run on top of it).

  Progress and error callbacks are wired to the process only if you registered
  them, so registering nothing costs nothing.

THE ARGUMENTS NAMESPACE - the IArgument catalogue
--------------------------------------------------
Everything the fluent API emits is one of these objects. You need them directly
only for WithArgument(IArgument), for writing your own argument type, or for
reading a rendered value in a test.

    public interface IArgument                { string Text { get; } }
    public interface IInputOutputArgument : IArgument
    {
        void Pre();
        Task During(CancellationToken cancellationToken = default);
        void Post();
    }
    public interface IInputArgument  : IInputOutputArgument { }
    public interface IOutputArgument : IInputOutputArgument { }
    public interface IDynamicArgument { string GetText(IEnumerable<IArgument> context); }
    public interface IPreEscapedFilterArgument { }          // empty marker

  IInputArgument / IOutputArgument mark an argument that owns a resource around
  the run: Pre() before the process starts, During(token) while it runs, Post()
  afterwards. IDynamicArgument lets an argument see the whole argument list when
  it renders (used to compute metadata stream indices).
  IPreEscapedFilterArgument is an EMPTY marker carried by a filter argument that
  has already applied the full filtergraph escaping to its own Value: the -vf /
  -af chain builder emits such a Value verbatim, while an argument without the
  marker gets the historical comma-only escape. Every filter argument listed
  below as "pre-escaped" carries it.

  The newer filter and option arguments VALIDATE EAGERLY in their constructors -
  an empty name or key, a negative index, a non-finite number and a null options
  object all throw a System.Argument* exception (ArgumentException,
  ArgumentOutOfRangeException, ArgumentNullException) as you build the chain, not
  when the command line renders. The individual cases are listed with each type.

  INPUT arguments (IInputArgument)
    InputArgument(bool verifyExists, string filePaths)
    InputArgument(string path, bool verifyExists)
        readonly string FilePath;  readonly bool VerifyExists;
        -> -i "path"      (when VerifyExists, Pre() throws
                           System.IO.FileNotFoundException for a missing file -
                           and Pre() runs when the process starts, not when you
                           build the chain)
    MultiInputArgument(bool verifyExists, IEnumerable<string> filePaths)
    MultiInputArgument(IEnumerable<string> filePaths, bool verifyExists)
        readonly IEnumerable<string> FilePaths;  readonly bool VerifyExists;
        -> -i "a" -i "b" -i "c"
           When VerifyExists, Pre() throws one
           System.IO.FileNotFoundException listing every missing path.
    InputDeviceArgument(string device)                -> -i {device}
    InputPipeArgument(IPipeSource writer) : PipeArgument
        readonly IPipeSource Writer;
        -> {Writer.GetStreamArguments()} -i "{PipePath}"
    ConcatArgument(IEnumerable<string> values)
        readonly IEnumerable<string> Values;
        -> -i "concat:a|b|c"        (the protocol-level concat)
    DemuxConcatArgument(IEnumerable<string> values)
        readonly IEnumerable<string> Values;
        -> -f concat -safe 0 -i "{temp list file}"
           Pre() writes the temp list file, Post() deletes it.
    MetaDataArgument(string metaDataContent) : IDynamicArgument
        -> -i "{temp FFMETADATA file}" -map_metadata {index of this input}
    MapMetadataArgument(int? inputIndex = null) : IDynamicArgument
        -> -map_metadata {index}     (null = the input that precedes it)

  OUTPUT arguments (IOutputArgument)
    OutputArgument(string path, bool overwrite = true)
    OutputArgument(FileInfo value)   OutputArgument(Uri value)
        readonly string Path;  readonly bool Overwrite;
        -> "path" -y   (the -y is emitted only when Overwrite is true; when it
                        is false, Pre() throws FFMpegException(File, "Output
                        file already exists and overwrite is disabled") if the
                        file is already there)
    OutputUrlArgument(string url)
        readonly string Url;    -> {url}
    OutputPipeArgument(IPipeSink reader) : PipeArgument
        readonly IPipeSink Reader;   -> "{PipePath}" -y

    public abstract class PipeArgument     // base of both pipe arguments
        string PipePath { get; }           // the named-pipe path handed to ffmpeg
        abstract string Text { get; }
        void Pre();  void Post();  Task During(CancellationToken = default);

  GLOBAL argument
    VerbosityLevelArgument(VerbosityLevel verbosityLevel)
        -> [-hide_banner ]-loglevel {level}

  VIDEO FILTER arguments (IVideoFilterArgument: string Key, string Value; they
  are combined by VideoFiltersArgument into one -vf "k=v, k=v" list)
    ScaleArgument(Size? size)
    ScaleArgument(int width, int height)
    ScaleArgument(VideoSize videosize)
        readonly Size? Size;   Key = "scale"   Value = "{w}:{h}"
        The VideoSize constructor sets width to -1 (auto) and height to the
        enum's numeric value; VideoSize.Original leaves Size null, which renders
        an empty value and is dropped from the filter list.
    TransposeArgument(Transposition transposition)
        readonly Transposition Transposition;  Key = "transpose"
    SetMirroringArgument(Mirroring mirroring)
        Mirroring Mirroring { get; set; }      Key = "" (bare hflip/vflip value)
    PadArgument(PadOptions options)                    Key = "pad"
    DrawTextArgument(DrawTextOptions options)          Key = "drawtext"
        readonly DrawTextOptions Options;
    SubtitleHardBurnArgument(SubtitleHardBurnOptions o) Key = "subtitles"
    BlackDetectArgument(double minimumDuration = 2.0,
        double pictureBlackRatioThreshold = 0.98,
        double pixelBlackThreshold = 0.1)              Key = "blackdetect"
    (a blackframe filter also exists, reachable only through
     VideoFilterOptions.BlackFrame)

    The filter arguments below are the newer ones. Every one of them carries the
    empty marker IPreEscapedFilterArgument, so the chain builder emits its Value
    verbatim instead of applying the historical comma-only escape - each type is
    responsible for whatever escaping its own value needs, and the entry says
    what it does.

    CustomFilterArgument(string rawFilterText)
    CustomFilterArgument(string filterName, string options)
    CustomFilterArgument(string filterName,
        params (string Key, string Value)[] options)
        Also IAudioFilterArgument, so the SAME object works in an -af chain.
        rawFilterText          Key = ""    Value = the text, nothing escaped
        (filterName, options)  Key = name  Value = options with only , ; [ ]
                                           escaped (: and = pass through)
        (filterName, pairs)    Key = name  Value = "k=v:k=v" with each VALUE
                                           escaped for both filtergraph passes
        An empty rawFilterText or filterName -> ArgumentException; a pair with an
        empty Key -> ArgumentException. An empty options string or an empty pair
        array renders the bare filter name.
        Built by VideoFilterOptions.Custom(...).
    FpsArgument(double fps)
    FpsArgument(string fps)                            Key = "fps"
        Value = the number in InvariantCulture, or the escaped expression.
        A NaN, infinite or non-positive double -> ArgumentOutOfRangeException;
        an empty string -> ArgumentException.
        Built by VideoFilterOptions.Fps(...).
    FormatFilterArgument(string pixelFormat)
    FormatFilterArgument(PixelFormat pixelFormat)      Key = "format"
        Value = the escaped pixel-format name.
        An empty string -> ArgumentException; a null PixelFormat ->
        ArgumentNullException.
        Built by VideoFilterOptions.Format(...).
    Lut3DArgument(string cubePath,
        Lut3DInterpolation interpolation = Lut3DInterpolation.Tetrahedral)
        string CubePath { get; }  Lut3DInterpolation Interpolation { get; }
        Key = "lut3d"   Value = "file={escaped path}:interp={name}"
        An empty cubePath -> ArgumentException.
        Built by VideoFilterOptions.Lut3D(...).
    RotateArgument(double radians)
    RotateArgument(string expression)                  Key = "rotate"
        Value = the angle in InvariantCulture, or the escaped expression.
        A NaN or infinite angle -> ArgumentOutOfRangeException; an empty
        expression -> ArgumentException.
        Built by VideoFilterOptions.Rotate(...).
    TonemapArgument(TonemapAlgorithm algorithm, double? param = null,
        double? desat = null, double? peak = null)
        TonemapAlgorithm Algorithm { get; }            Key = "tonemap"
        Value = "{algorithm}" plus ":param=", ":desat=", ":peak=" for whichever
        of the three you passed, always in that order.
        A non-finite param / desat / peak -> ArgumentOutOfRangeException.
        Built by VideoFilterOptions.Tonemap(...).
    ZScaleArgument(ZScaleOptions options)              Key = "zscale"
        ZScaleOptions Options { get; }
        Value = the options rendered in the fixed order described under "FILTER
        OPTIONS" below. A null options object -> ArgumentNullException.
        Built by VideoFilterOptions.ZScale(...).
    public class ZScaleOptions        // a settable value object, not an IArgument
        int? Width; int? Height; Size? Size; string Transfer; string Primaries;
        string Matrix; string Range; double? Npl; string DitherMode
        Rendering it throws FFMpegArgumentException when nothing is set, and
        when Npl is not a finite number. Its string values are escaped for both
        filtergraph passes. Full description under "FILTER OPTIONS" below.

    public class VideoFiltersArgument : IArgument
        VideoFiltersArgument(VideoFilterOptions options)
        readonly VideoFilterOptions Options;
        Text throws FFMpegArgumentException("No video-filter arguments
        provided") when the option object is empty. Commas inside a filter value
        are escaped as "\," before the list is joined - unless the argument
        carries IPreEscapedFilterArgument, whose Value is emitted verbatim
        because it has already escaped itself.

  AUDIO FILTER arguments (IAudioFilterArgument: string Key, string Value;
  combined by AudioFiltersArgument into one -af "..." list)
    PanArgument(string channelLayout, params string[] outputDefinitions)
    PanArgument(int channels, params string[] outputDefinitions)
        readonly string ChannelLayout;  Key = "pan"
    DynamicNormalizerArgument(int frameLength = 500, int filterWindow = 31,
        double targetPeak = 0.95, double gainFactor = 10.0, double targetRms = 0.0,
        bool channelCoupling = true, bool enableDcBiasCorrection = false,
        bool enableAlternativeBoundary = false, double compressorFactor = 0.0)
    HighPassFilterArgument(...) / LowPassFilterArgument(...)
        (same parameter list as AudioFilterOptions.HighPass / LowPass below)
    AudioGateArgument(double levelIn = 1, string mode = "downward",
        double range = 0.06125, double threshold = 0.125, int ratio = 2,
        double attack = 20, double release = 250, int makeup = 1,
        double knee = 2.828427125, string detection = "rms",
        string link = "average")
    SilenceDetectArgument(string noise_type = "db", double noise = 60,
        double duration = 2, bool mono = false)        Key = "silencedetect"

    CustomFilterArgument(...)   the video-filter type above is ALSO an
        IAudioFilterArgument, so it is the way to put an arbitrary filter into
        an -af chain. AudioFilterOptions has no Custom(...) method: construct
        the argument yourself and add it to the options object's Arguments list,
        or build an AudioFiltersArgument around it and pass that to
        WithArgument. See "REACHING CustomFilterArgument FROM AN -af CHAIN"
        below.

    public class AudioFiltersArgument : IArgument
        AudioFiltersArgument(AudioFilterOptions options)
        readonly AudioFilterOptions Options;
        Text throws FFMpegArgumentException("No audio-filter arguments
        provided") when the option object is empty. It applies exactly the same
        escaping rule as VideoFiltersArgument: a value from an argument carrying
        IPreEscapedFilterArgument is emitted verbatim, everything else has its
        commas escaped as "\,".

  PLAIN option arguments (IArgument only - these are what the FFMpegArgumentOptions
  methods above construct; every one is public and constructible)
    AudioBitrateArgument(int bitrate) / (AudioQuality value)   readonly int Bitrate
    AudioCodecArgument(Codec) / (string)                       readonly string AudioCodec
    AudioSamplingRateArgument(int samplingRate = 48000)        readonly int SamplingRate
    VariableBitRateArgument(int vbr)                           readonly int Vbr
    VideoBitrateArgument(int bitrate)                          readonly int Bitrate
    VideoCodecArgument(Codec) / (string codec)                 readonly string Codec
    ConstantRateFactorArgument(int crf)                        readonly int Crf
    SpeedPresetArgument(Speed speed)                           readonly Speed Speed
    CopyArgument(Channel channel = Channel.Both)               readonly Channel Channel
    CopyCodecArgument()
    DisableChannelArgument(Channel channel)                    readonly Channel Channel
    BitStreamFilterArgument(Channel channel, Filter filter)    readonly Channel/Filter
    DurationArgument(TimeSpan? duration)                       readonly TimeSpan? Duration
    SeekArgument(TimeSpan? seekTo)                             readonly TimeSpan? SeekTo
    EndSeekArgument(TimeSpan? seekTo)                          readonly TimeSpan? SeekTo
    LoopArgument(int times)                                    readonly int Times
    FrameOutputCountArgument(int frames)                       readonly int Frames
    FrameRateArgument(double framerate)                        readonly double Framerate
    StartNumberArgument(int startNumber)                       readonly int StartNumber
    SizeArgument(Size? size) / (int width, int height)         readonly Size? Size
    CropArgument(Size? size, int top, int left)
    CropArgument(int width, int height, int top, int left)
        readonly Size? Size; readonly int Top; readonly int Left
        NOTE the constructor order is (top, left) while
        FFMpegArgumentOptions.Crop takes (left, top).
    ForceFormatArgument(string format) / (ContainerFormat format)
    ForcePixelFormat(string format) / (PixelFormat format)     string PixelFormat { get; }
    FaststartArgument()
    GifPaletteArgument(int streamIndex, double fps, Size? size)
    HardwareAccelerationArgument(HardwareAccelerationDevice d)
        HardwareAccelerationDevice HardwareAccelerationDevice { get; }
    ID3V2VersionArgument(int version)
    MapStreamArgument(int streamIndex, int inputFileIndex,
        Channel channel = Channel.All, bool negativeMap = false)
    OverwriteArgument()                                        -> -y
    RemoveMetadataArgument()                                   -> -map_metadata -1
    ShortestArgument(bool shortest)                            readonly bool Shortest
    ThreadsArgument(int threads) / (bool isMultiThreaded)      readonly int Threads
    AudibleEncryptionKeyArgument(string activationBytes)
    AudibleEncryptionKeyArgument(string key, string iv)
    CustomArgument(string argument)                            readonly string Argument
    AutoRotateArgument(bool enabled)                           bool Enabled
        -> -autorotate / -noautorotate                (AutoRotate(bool);
           INPUT-side only - see the warning above)
    CuesToFrontArgument(bool enabled)                          bool Enabled
        -> -cues_to_front 1 / -cues_to_front 0        (CuesToFront(bool))
    ReserveIndexSpaceArgument(int bytes)                       int Bytes
        -> -reserve_index_space {n}                   (ReserveIndexSpace(int))
        A negative byte count -> ArgumentOutOfRangeException.
    NumericSpeedPresetArgument(int preset)                     int Preset
        -> -preset {n}                                (WithSpeedPreset(int))
        The word-based presets stay on SpeedPresetArgument; the AV1 encoders
        take the number.
    SubtitleCodecArgument(Codec subtitleCodec)
    SubtitleCodecArgument(string subtitleCodec)         readonly string SubtitleCodec
        -> -c:s {name}, lower-cased               (WithSubtitleCodec(...))
        The Codec overload throws FFMpegException(Operation) when the codec's
        Type is not CodecType.Subtitle.
    EncoderParamsArgument(string optionName,
        params (string Key, string Value)[] parameters)
        string OptionName { get; }   string Parameters { get; }
        -> -{name} k=v:k=v, with the parameter string wrapped in double quotes
           when it contains whitespace; a leading '-' on optionName is trimmed
                                                      (WithEncoderParams(...))
        An empty optionName, a null/empty parameter array, or a parameter with
        an empty Key -> ArgumentException.
    SetMetadataArgument(string key, string value)
    SetMetadataArgument(int streamIndex, string key, string value)
    SetMetadataArgument(Channel channel, int streamIndex,
        string key, string value)
        -> -metadata k="v"  /  -metadata:s{:type}:{n} k="v"
                                        (WithMetadata / WithStreamMetadata)
        A '"' inside the value is escaped as '\"'; a null value renders as an
        empty string. An empty key -> ArgumentException; a negative streamIndex
        -> ArgumentOutOfRangeException.
    DispositionArgument(int streamIndex, params Disposition[] flags)
    DispositionArgument(Channel channel, int streamIndex,
        params Disposition[] flags)
        -> -disposition{:type}:{n} a+b                (WithDisposition(...))
        No flags at all, or any flag equal to Disposition.None, renders the
        single value "0" (clear every flag). A negative streamIndex ->
        ArgumentOutOfRangeException.

  Writing your own: implement IArgument (a single `Text` property) and pass it
  to WithArgument. Implement IVideoFilterArgument / IAudioFilterArgument (Key +
  Value) if you want it merged into an existing -vf / -af chain, and add it to
  the options object's public Arguments list inside the WithVideoFilters /
  WithAudioFilters callback - or, for a filter you want to model properly,
  construct VideoFiltersArgument yourself and pass it to WithArgument. (For a
  one-off filter you do not need a type for, VideoFilterOptions.Custom(...)
  takes the raw text or the name-and-options pairs directly.)

  If your own filter argument escapes its own value (see "FILTER-GRAPH ESCAPING"
  below), also implement the empty marker interface IPreEscapedFilterArgument so
  the chain builder emits your Value verbatim instead of escaping its commas a
  second time.

FILTER OPTIONS - what goes inside WithVideoFilters / WithAudioFilters
----------------------------------------------------------------------
    public class VideoFilterOptions
        List<IVideoFilterArgument> Arguments { get; }
        VideoFilterOptions Scale(VideoSize videoSize)
        VideoFilterOptions Scale(int width, int height)
        VideoFilterOptions Scale(Size size)
        VideoFilterOptions Transpose(Transposition transposition)
        VideoFilterOptions Mirror(Mirroring mirroring)
        VideoFilterOptions DrawText(DrawTextOptions drawTextOptions)
        VideoFilterOptions HardBurnSubtitle(SubtitleHardBurnOptions options)
        VideoFilterOptions BlackDetect(double minimumDuration = 2.0,
            double pictureBlackRatioThreshold = 0.98,
            double pixelBlackThreshold = 0.1)
        VideoFilterOptions BlackFrame(int amount = 98, int threshold = 32)
        VideoFilterOptions Pad(PadOptions padOptions)
        VideoFilterOptions Custom(string rawFilterText)
        VideoFilterOptions Custom(string filterName, string options)
        VideoFilterOptions Custom(string filterName,
            params (string Key, string Value)[] options)
        VideoFilterOptions Fps(double fps)
        VideoFilterOptions Fps(string fps)
        VideoFilterOptions Lut3D(string cubePath,
            Lut3DInterpolation interpolation = Lut3DInterpolation.Tetrahedral)
        VideoFilterOptions Format(PixelFormat pixelFormat)
        VideoFilterOptions Format(string pixelFormat)
        VideoFilterOptions ZScale(ZScaleOptions options)
        VideoFilterOptions ZScale(Action<ZScaleOptions> configure)
        VideoFilterOptions Tonemap(TonemapAlgorithm algorithm, double? param = null,
            double? desat = null, double? peak = null)
        VideoFilterOptions Rotate(double radians)
        VideoFilterOptions Rotate(string expression)

  What the newer video filters render:
        Custom("negate")                      -> negate            (verbatim)
        Custom("drawbox", "x=10:y=10")        -> drawbox=x=10:y=10
        Custom("lut3d", ("file", p))          -> lut3d=file={escaped p}
        Fps(30)          / Fps("ntsc")        -> fps=30 / fps=ntsc
        Lut3D(p)                              -> lut3d=file={escaped p}:interp=tetrahedral
        Format("yuv420p")                     -> format=yuv420p
        ZScale(z => { z.Transfer = "linear";
                      z.Npl = 100; })         -> zscale=t=linear:npl=100
        Tonemap(TonemapAlgorithm.Hable,
                desat: 0)                     -> tonemap=hable:desat=0
        Rotate(1.5707963) / Rotate("PI/2")    -> rotate=1.5707963 / rotate=PI/2

  Every number is formatted with CultureInfo.InvariantCulture, so a whole number
  prints without a decimal point and a decimal separator is never a comma.

  Format(...) is the IN-CHAIN pixel-format conversion (the `format` filter);
  ForcePixelFormat(...) on FFMpegArgumentOptions is the encoder-level -pix_fmt.
  Rotate(...) is an arbitrary-angle resample; for the four right-angle cases
  Transpose(...) is cheaper and lossless.

  ZScaleOptions is a plain settable object; only the options you set are
  emitted, always in this order - w, h, s, t, p, m, r, npl, d:
        int? Width       -> w        (ignored when Size is set)
        int? Height      -> h        (ignored when Size is set)
        Size? Size       -> s=WxH
        string Transfer  -> t        ZScaleTransfer.*   constants
        string Primaries -> p        ZScalePrimaries.*  constants
        string Matrix    -> m        ZScaleMatrix.*     constants
        string Range     -> r        ZScaleRange.*      constants
        double? Npl      -> npl      nominal peak luminance, cd/m2
        string DitherMode-> d        ZScaleDither.*     constants
  Rendering a ZScaleOptions with nothing set throws FFMpegArgumentException
  ("At least one zscale option must be set"), and so does rendering one whose
  Npl is not a finite number ("The zscale nominal peak luminance must be a
  finite number"). Both throw when the command line renders, not when you set
  the property.
  zscale needs libzimg in the ffmpeg build, and it needs to know the colour
  space it is converting FROM: on a file whose colour metadata is unspecified,
  or after a filter that hands it RGB, set Matrix (and/or reach for
  Custom("zscale", ...) with zscale's input-side min/tin/pin options) or ffmpeg
  reports "code 3074: no path between colorspaces".

    public class AudioFilterOptions
        List<IAudioFilterArgument> Arguments { get; }
        AudioFilterOptions Pan(string channelLayout, params string[] outputDefinitions)
        AudioFilterOptions Pan(int channels, params string[] outputDefinitions)
        AudioFilterOptions DynamicNormalizer(int frameLength = 500,
            int filterWindow = 31, double targetPeak = 0.95,
            double gainFactor = 10.0, double targetRms = 0.0,
            bool channelCoupling = true, bool enableDcBiasCorrection = false,
            bool enableAlternativeBoundary = false, double compressorFactor = 0.0)
        AudioFilterOptions HighPass(double frequency = 3000, int poles = 2,
            string width_type = "q", double width = 0.707, double mix = 1,
            string channels = "", bool normalize = false, string transform = "",
            string precision = "auto", int? blocksize = null)
        AudioFilterOptions LowPass(...)          // identical parameter list
        AudioFilterOptions AudioGate(double level_in = 1, string mode = "downward",
            double range = 0.06125, double threshold = 0.125, int ratio = 2,
            double attack = 20, double release = 250, int makeup = 1,
            double knee = 2.828427125, string detection = "rms",
            string link = "average")
        AudioFilterOptions SilenceDetect(string noise_type = "db",
            double noise = 60, double duration = 2, bool mono = false)

  REACHING CustomFilterArgument FROM AN -af CHAIN. AudioFilterOptions has no
  Custom(...) method, but CustomFilterArgument implements IAudioFilterArgument,
  so add it to the options object's public Arguments list yourself:

    using CodeBrix.VideoProcessing;
    using CodeBrix.VideoProcessing.Arguments;

    FFMpegArguments
        .FromFileInput("in.mp4")
        .OutputToFile("out.mp4", true, o => o
            .WithAudioFilters(f =>
            {
                f.Pan(2, "c0=c0", "c1=c1");
                f.Arguments.Add(new CustomFilterArgument("loudnorm",
                    ("I", "-16"), ("TP", "-1.5")));
            }))
        .ProcessSynchronously();

  The chain builder treats it like any other audio filter, and because
  CustomFilterArgument carries IPreEscapedFilterArgument its value is emitted
  exactly as the constructor escaped it.

  The three filter option builders (all created through a static Create):

    public class DrawTextOptions
        readonly string Text;  readonly string Font;
        readonly List<(string key, string value)> Parameters;
        static DrawTextOptions Create(string text, string font)
        static DrawTextOptions Create(string text, string font,
                                      params (string key, string value)[] parameters)
        DrawTextOptions WithParameter(string key, string value)
        `text` and `font` become the drawtext `text=` and `fontfile=` parameters;
        `font` is a path to a font FILE. Any value containing a space is wrapped
        in single quotes.

    public class PadOptions
        readonly Dictionary<string, string> Parameters;
        static PadOptions Create(string width, string height)   // pad=width=..:height=..
        static PadOptions Create(string aspectRatio)            // pad=aspect=..
        PadOptions WithParameter(string key, string value)
        Create(width, height) throws if BOTH are null; either one may be null.
        Values are ffmpeg pad expressions, e.g. Create("max(iw,ih)", "ow").

    public class SubtitleHardBurnOptions
        readonly Dictionary<string, string> Parameters;
        static SubtitleHardBurnOptions Create(string subtitlePath)
        SubtitleHardBurnOptions SetOriginalSize(int width, int height)
        SubtitleHardBurnOptions SetOriginalSize(Size size)
        SubtitleHardBurnOptions SetSubtitleIndex(int index)
        SubtitleHardBurnOptions SetCharacterEncoding(string encode)
        SubtitleHardBurnOptions WithStyle(StyleOptions styleOptions)
        SubtitleHardBurnOptions WithParameter(string key, string value)
        Supports .srt and .ass files, and subrip/ssa subtitle streams inside a
        container. SetSubtitleIndex is the index among the SUBTITLES, not the
        stream index. The subtitle path is quoted and libavfilter-escaped for
        you (backslash, colon, bracket and apostrophe are all handled).

    public class StyleOptions
        readonly Dictionary<string, string> Parameters;
        static StyleOptions Create()
        StyleOptions WithParameter(string key, string value)
        Emitted as the subtitles filter's `force_style` value, in ASS style
        format (e.g. "FontName", "PrimaryColour").

FILTER-GRAPH ESCAPING, AND THE ONE -vf PER OUTPUT RULE
-------------------------------------------------------
ONE CHAIN PER OUTPUT. ffmpeg accepts a single -vf per output and silently keeps
only the LAST one it is given, so a second WithVideoFilters call on the same
output now throws FFMpegArgumentException instead of producing a command line
that quietly drops half your filters. Build the whole chain in one call:

    // WRONG - throws
    .OutputToFile(out, true, o => o
        .WithVideoFilters(f => f.Scale(1280, 720))
        .WithVideoFilters(f => f.Fps(30)))

    // RIGHT
    .OutputToFile(out, true, o => o
        .WithVideoFilters(f => f.Scale(1280, 720).Fps(30)))

WithAudioFilters / -af behaves exactly the same way. Separate outputs (and an
input's own callback) each have their own chain, so MultiOutput is unaffected.

ESCAPING. ffmpeg unescapes a filter option value TWICE while it parses a
filtergraph - once when the "name=options" filter description is tokenised, and
once when that options string is split into key=value pairs. A free-text value
therefore has to be escaped twice, which is what the newer filter arguments do
for the values they own. Which characters get escaped depends on the shape you
choose:

    Custom(name, params (key, value)[])  each VALUE is escaped for BOTH passes:
    Lut3D(path), Fps(string), Format,    \  '  :  ,  ;  [  ]  =
    Rotate(string), ZScale values        a Windows path (C:\luts\warm.cube), a
                                         path with a comma or a space, an
                                         apostrophe - all survive.

    Custom(name, "already:formed=opts")  only the characters that would END the
                                         filter are escaped:  ,  ;  [  ]
                                         : and = pass through so they keep
                                         separating the options. The escaping is
                                         not quote-aware, so a comma inside a
                                         '...' section is escaped too; when that
                                         matters, use the raw overload.

    Custom("raw filter text")            NOTHING is escaped. You own every
                                         backslash.

A double-quote character in a value is not escaped by any of them: like every
other path this library hands to ffmpeg, a path containing " is not supported.

The older typed filters (Scale, Transpose, Mirror, Crop, Pad, DrawText,
HardBurnSubtitle, BlackDetect, BlackFrame, and the audio ones) are NOT routed
through this helper - they keep the escaping they always had, so scale=200:300
and transpose=2 render byte for byte as before. What separates the two groups is
the empty marker interface IPreEscapedFilterArgument: an argument that carries it
has escaped its own value and the chain builder emits it verbatim; an argument
without it gets the historical comma-only escape.


LOCATING THE FFMPEG BINARIES - FFOptions / GlobalFFOptions
-----------------------------------------------------------
    public class FFOptions : ICloneable
        string WorkingDirectory { get; set; } = ""
        string BinaryFolder { get; set; } = ""       // "" == look on PATH
        string TemporaryFilesFolder { get; set; } = Path.GetTempPath()
        string EncodingWebName { get; set; } = Encoding.Default.WebName
        Encoding Encoding { get; set; }              // [JsonIgnore] view of the above
        FFMpegLogLevel? LogLevel { get; set; }       // null == do not pass -v
        Dictionary<string, string> ExtensionOverrides { get; set; }
                                        // defaults to { "mpegts" -> ".ts" }
        bool UseCache { get; set; } = true           // cache the codec catalogue
        bool AllowAv1EncoderFallback { get; set; } = false
                                        // libsvtav1 missing -> use libaom-av1
        FFOptions Clone()

    public static class GlobalFFOptions
        static FFOptions Current { get; }
        static void Configure(FFOptions ffOptions)          // replaces Current
        static void Configure(Action<FFOptions> optionsAction)  // mutates Current
        static string GetFFMpegBinaryPath(FFOptions ffOptions = null)
        static string GetFFProbeBinaryPath(FFOptions ffOptions = null)

  RESOLUTION ORDER for the binary path, given BinaryFolder:
      1. {BinaryFolder}/{x64|x86}/ffmpeg[.exe]   (the sub-folder matching the
         bitness of the current process)
      2. {BinaryFolder}/ffmpeg[.exe]
      3. the bare name "ffmpeg"/"ffmpeg.exe", i.e. resolved through PATH
  ".exe" is appended on Windows only. The same rules apply to ffprobe.

  On first access, GlobalFFOptions.Current is loaded from a file named
  `ffmpeg.config.json` in the process's current directory if that file exists
  (deserialized straight into FFOptions - so its keys are "BinaryFolder",
  "TemporaryFilesFolder", "WorkingDirectory", "EncodingWebName", "LogLevel",
  "ExtensionOverrides", "UseCache", "AllowAv1EncoderFallback"); otherwise a
  default FFOptions is used.

  Configure it in code at startup instead if you prefer:

      GlobalFFOptions.Configure(new FFOptions
      {
          BinaryFolder = "/opt/ffmpeg/bin",
          TemporaryFilesFolder = "/var/tmp",
      });
      // or mutate the existing instance:
      GlobalFFOptions.Configure(o => o.LogLevel = FFMpegLogLevel.Error);

  ExtensionOverrides feeds ContainerFormat.Extension: the extension for a
  container is "." + its name unless the name has an entry in the dictionary
  (which is why VideoType.MpegTs.Extension is ".ts", not ".mpegts").

AV1 ENCODING WITHOUT SVT-AV1 - FFOptions.AllowAv1EncoderFallback
-----------------------------------------------------------------
  Many ffmpeg builds have no SVT-AV1 encoder (libsvtav1) - the gyan.dev
  "essentials" Windows build is a common example - while still having libaom
  (libaom-av1). An output that asks for libsvtav1 then fails.

  BY DEFAULT (AllowAv1EncoderFallback == false) the run throws
  FFMpegEncoderNotFoundException (EncoderName == "libsvtav1"). Its Message is:

      The SVT-AV1 encoder library (libsvtav1) is not present on this machine,
      or the ffmpeg being used could not find it. To encode AV1 without
      installing anything, opt in to the libaom-av1 fallback by setting
      FFOptions.AllowAv1EncoderFallback = true (...), or install an ffmpeg
      build that includes libsvtav1. ffmpeg exited with non-zero exit-code
      (<code> - <ffmpeg's stderr>)

  - i.e. the explanation, then everything the message has always said.
  FFMpegErrorOutput is ffmpeg's stderr, as for any other failure.

  OPTING IN - globally, per run, or in ffmpeg.config.json:

      GlobalFFOptions.Configure(o => o.AllowAv1EncoderFallback = true);

      FFMpegArguments.FromFileInput(input)
          .OutputToFile(output, true, o => o
              .WithVideoCodec("libsvtav1").WithSpeedPreset(8).WithConstantRateFactor(30))
          .Configure(o => o.AllowAv1EncoderFallback = true)
          .ProcessSynchronously();

  WHAT IT DOES. Before ffmpeg starts, and only when an output selects
  libsvtav1, the library reads `ffmpeg -encoders` (once per ffmpeg binary while
  UseCache is on). If libsvtav1 is there, nothing changes. If it is missing and
  libaom-av1 is there, each output that selects libsvtav1 - and only those -
  is rewritten:
      -c:v libsvtav1           -> -c:v libaom-av1
      -preset N  (numeric)     -> -cpu-used N, capped at 8 (libaom's fastest)
      -crf N                   -> kept, followed by -b:v 0 unless the output
                                  sets a video bit rate
      -svtav1-params ...       -> dropped (libaom-av1 does not accept them)
  Arguments are recognised when they were added with WithVideoCodec,
  WithSpeedPreset(int), WithConstantRateFactor, WithVideoBitrate and
  WithEncoderParams, or with a WithCustomArgument whose whole text is that one
  option (for example "-c:v libsvtav1" or "-preset 8"). Options buried inside a
  longer custom string are not rewritten.

  Afterwards, processor.Av1EncoderFallbackApplied is true,
  processor.Av1EncoderFallbackNotes lists each change in a sentence, and
  processor.Arguments shows the command that actually ran.

  If the fallback is on but cannot help - the ffmpeg has no libaom-av1 either,
  or libsvtav1 was selected in a form it does not recognise - the run still
  throws FFMpegEncoderNotFoundException, and the explanation says the fallback
  was enabled and why it could not be used.

  libaom is far slower than SVT-AV1 and the same -crf does not give identical
  quality, so treat the fallback as a way to keep working on a machine without
  SVT-AV1, not as a like-for-like substitute.

ENUMS AND CATALOGUE TYPES (with values)
----------------------------------------
    public enum VideoSize      { FullHd = 1080, Hd = 720, Ed = 480, Ld = 360,
                                 Original = -1 }
    public enum AudioQuality   { Ultra = 384, VeryHigh = 256, Good = 192,
                                 Normal = 128, BelowNormal = 96, Low = 64 }
                                 // the value IS the kbit/s bitrate
    public enum Speed          { VerySlow, Slower, Slow, Medium, Fast, Faster,
                                 VeryFast, SuperFast, UltraFast }
                                 // rendered lower-cased as the x264 -preset
    public enum FFMpegLogLevel { Quiet = 0, Panic = 1, Fatal = 2, Error = 3,
                                 Warning = 4, Info = 5, Verbose = 6, Debug = 7,
                                 Trace = 8 }
    public enum VerbosityLevel { Quiet = -8, Fatal = 8, Error = 16, Warning = 24,
                                 Info = 32, Verbose = 40, Debug = 48, Trace = 56 }
                                 // in CodeBrix.VideoProcessing.Arguments
    public enum Mirroring      { Vertical, Horizontal }   // vflip / hflip
    public enum Transposition  { CounterClockwise90VerticalFlip = 0,
                                 Clockwise90 = 1, CounterClockwise90 = 2,
                                 Clockwise90VerticalFlip = 3 }
    public enum HardwareAccelerationDevice
                               { Auto, D3D11VA, DXVA2, QSV, CUVID, CUDA, VDPAU,
                                 VAAPI, LibMFX }   // rendered lower-cased
    public enum CodecType      { Unknown = 0, Video = 1 << 1, Audio = 1 << 2,
                                 Subtitle = 1 << 3, Data = 1 << 4 }
    public enum Channel        { Audio, Video, Both, VideoNoAttachedPic,
                                 Subtitle, Data, Attachments, All }
                                 // stream specifiers :a :v :V :s :d :t
    public enum Filter         { H264_Mp4ToAnnexB, Aac_AdtstoAsc }  // bitstream
    public enum FeatureStatus  { Unknown, NotSupported, Supported }
    public enum Lut3DInterpolation
                               { Nearest, Trilinear, Tetrahedral, Pyramid,
                                 Prism }   // the lut3d filter's interp option
    public enum TonemapAlgorithm
                               { None, Clip, Linear, Gamma, Reinhard, Hable,
                                 Mobius }  // rendered lower-cased
    public enum Disposition    { None, Default, Dub, Original, Comment, Lyrics,
                                 Karaoke, Forced, HearingImpaired,
                                 VisualImpaired, CleanEffects, AttachedPic,
                                 Captions, Descriptions, Dependent, Metadata }
                                 // rendered in ffmpeg's snake_case
                                 // (hearing_impaired, attached_pic, ...);
                                 // None renders as "0" and clears every flag

  A disposition REPLACES whatever the stream had, so list every flag that should
  stay. WithDisposition with no flags at all, or with Disposition.None among
  them, renders "0".

  Channel.Both means "audio and video" for CopyArgument/DisableChannelArgument;
  in MapStreamArgument it is silently treated as Channel.All. Channel.All and
  Channel.Both both render an EMPTY stream-type specifier.

  Codec, ContainerFormat and PixelFormat are NOT enums - they are catalogue
  objects parsed out of ffmpeg's own capability listings. Their constructors are
  internal, so you obtain them from FFMpeg.GetCodec / GetContainerFormat /
  GetPixelFormat or from the named helpers below.

    public class Codec
        string Name { get; }              CodecType Type { get; }
        bool DecodingSupported { get; }   bool EncodingSupported { get; }
        bool IsIntraFrameOnly { get; }    bool IsLossy { get; }
        bool IsLossless { get; }          string Description { get; }
        FeatureLevel EncoderFeatureLevel { get; }
        FeatureLevel DecoderFeatureLevel { get; }

        public class FeatureLevel        // Codec.FeatureLevel, nested
            bool IsExperimental { get; }
            FeatureStatus SupportsFrameLevelMultithreading { get; }
            FeatureStatus SupportsSliceLevelMultithreading { get; }
            FeatureStatus SupportsDrawHorizBand { get; }
            FeatureStatus SupportsDirectRendering { get; }

    public class ContainerFormat
        string Name { get; }   bool DemuxingSupported { get; }
        bool MuxingSupported { get; }   string Description { get; }
        string Extension { get; }        // ExtensionOverrides, else "." + Name

    public class PixelFormat
        string Name { get; }   int Components { get; }   int BitsPerPixel { get; }
        bool InputConversionSupported { get; }
        bool OutputConversionSupported { get; }
        bool HardwareAccelerationSupported { get; }
        bool IsPaletted { get; }   bool IsBitstream { get; }
        bool CanConvertTo(PixelFormat other)

  Named shortcuts (each is a PROPERTY that calls FFMpeg.GetCodec /
  GetContainerFormat, so the first use launches ffmpeg to read its catalogue):

    public static class VideoCodec
        Codec LibX264, LibX265, LibVpx, LibTheora, MpegTs, LibaomAv1,
              LibSvtAv1, LibRav1e
        public static class Image
            Codec Png, Jpg (mjpeg), Bmp, Webp
            Codec GetByExtension(string path)   // throws NotSupportedException
                                                // for anything else
    public static class AudioCodec
        Codec Aac, LibVorbis, LibFdk_Aac, Ac3, Eac3, LibMp3Lame, LibOpus
        Codec Copy      // the ONE that does not consult ffmpeg
    public static class VideoType
        ContainerFormat MpegTs, Ts, Mp4, Mov, Avi, Ogv, WebM, Ivf, Ogg, Matroska

        (VideoType is where every named ContainerFormat lives - ContainerFormat
        itself is the catalogue object, not a constants class.)

    The zscale colour-name constants (all plain strings, all in
    CodeBrix.VideoProcessing.Enums):
        ZScaleTransfer   Input, Linear, Bt709, Smpte170M, Bt2020Ten,
                         Bt2020Twelve, Smpte2084, AribStdB67
        ZScalePrimaries  Input, Bt709, Bt470Bg, Smpte170M, Bt2020, Smpte431,
                         Smpte432
        ZScaleMatrix     Input, Gbr, Bt709, Bt470Bg, Smpte170M, Bt2020Ncl,
                         Bt2020Cl
        ZScaleRange      Input, Limited, Full, Tv, Pc
        ZScaleDither     None, Ordered, Random, ErrorDiffusion
    Any other name the installed ffmpeg accepts can be passed as a plain string.
    public static class FileExtension
        static readonly string Mp4, Ts, Ogv, WebM     // via VideoType
        static readonly string Mp3 = ".mp3", Gif = ".gif"
        static string Extension(this Codec type)      // extension method
        public static class Image
            const string Png = ".png", Jpg = ".jpg", Bmp = ".bmp", Webp = ".webp"
            static readonly List<string> All

FEATURE DETECTION AND VERIFICATION HELPERS
-------------------------------------------
    public static class FFMpegHelper       // CodeBrix.VideoProcessing.Helpers
        void VerifyFFMpegExists(FFOptions ffMpegOptions)
            Runs `ffmpeg -version`; throws
            FFMpegException(FFMpegExceptionType.Operation, "ffmpeg was not found
            on your system") if the exit code is not 0. A success is remembered
            for the life of the process, so later calls are free.
        void RootExceptionCheck()
            Throws FFOptionsException if GlobalFFOptions.Current.BinaryFolder is
            null (it defaults to "", which is legal and means "use PATH").
        void ConversionSizeExceptionCheck(IMediaAnalysis info)
        void ConversionSizeExceptionCheck(int width, int height)
            Throws ArgumentException unless both dimensions are even.
        void ExtensionExceptionCheck(string filename, string extension)
            Throws FFMpegException(FFMpegExceptionType.File) on a mismatch.

    public static class FFProbeHelper
        void VerifyFFProbeExists(FFOptions ffMpegOptions)
            Runs `ffprobe -version`; throws FFProbeException("ffprobe was not
            found on your system") on failure. Also cached per process.
        void RootExceptionCheck()

  Use VerifyFFMpegExists/VerifyFFProbeExists at startup to fail fast with a
  clear message instead of discovering a missing binary mid-conversion:

      try
      {
          FFMpegHelper.VerifyFFMpegExists(GlobalFFOptions.Current);
          FFProbeHelper.VerifyFFProbeExists(GlobalFFOptions.Current);
      }
      catch (Exception ex)   // FFMpegException / FFProbeException
      {
          Console.Error.WriteLine($"FFmpeg tooling unavailable: {ex.Message}");
      }

  For per-codec capability detection, read the Codec object:

      if (FFMpeg.TryGetCodec("libx265", out Codec hevc) && hevc.EncodingSupported)
      {
          bool experimental = hevc.EncoderFeatureLevel.IsExperimental;
      }

METADATA
--------
Two independent builders ship in this package; use either one.

    public class MetaDataBuilder    // CodeBrix.VideoProcessing.Builders.MetaData
        MetaDataBuilder WithEntry(string key, string entry)
        MetaDataBuilder WithEntry(string key, params string[] values)
        MetaDataBuilder WithEntry(string key, IEnumerable<string> values)
        MetaDataBuilder WithTitle(string value)
        MetaDataBuilder WithAlbum(string value)
        MetaDataBuilder WithArtists(params string[] value)
        MetaDataBuilder WithArtists(IEnumerable<string> value)
        MetaDataBuilder WithAlbumArtists(params string[] value)
        MetaDataBuilder WithAlbumArtists(IEnumerable<string> value)
        MetaDataBuilder WithComposers(params string[] value)
        MetaDataBuilder WithComposers(IEnumerable<string> value)
        MetaDataBuilder WithGenres(params string[] value)
        MetaDataBuilder WithGenres(IEnumerable<string> value)
        MetaDataBuilder WithComments(params string[] value)
        MetaDataBuilder WithComments(IEnumerable<string> value)
        MetaDataBuilder WithCopyright(string value)
        MetaDataBuilder WithDate(string value)
        MetaDataBuilder WithEncoder(string value)
        MetaDataBuilder WithMajorBrand(string value)
        MetaDataBuilder WithMinorVersion(string value)
        MetaDataBuilder WithCompatibleBrands(string value)
        MetaDataBuilder AddChapter(ChapterData chapterData)
        MetaDataBuilder AddChapter(TimeSpan duration, string title = null)
        MetaDataBuilder AddChapters<T>(IEnumerable<T> values,
                            Func<T, (TimeSpan duration, string title)> chapterGetter)
        ReadOnlyMetaData Build()

      Multiple values for one key are joined with "; ", and calling WithEntry
      twice for the same key APPENDS ("existing; new") rather than replacing.
      AddChapter(duration, title) makes each chapter start where the previous
      one ended; an omitted title becomes "Chapter {n}".

    public interface IReadOnlyMetaData
        IReadOnlyList<ChapterData> Chapters { get; }
        IReadOnlyDictionary<string, string> Entries { get; }
    public class MetaData : IReadOnlyMetaData
        MetaData()  /  MetaData(MetaData cloneSource)
        Dictionary<string, string> Entries { get; }
        List<ChapterData> Chapters { get; }
    public class ReadOnlyMetaData : IReadOnlyMetaData
        ReadOnlyMetaData(MetaData metaData)
    public class MetaDataSerializer
        static readonly MetaDataSerializer Instance
        string Serialize(IReadOnlyMetaData metaData)   // the ;FFMETADATA1 text

    public class FFMetadataBuilder       // CodeBrix.VideoProcessing (root)
        static FFMetadataBuilder Empty()
        FFMetadataBuilder WithTag(string key, string value)
        FFMetadataBuilder WithChapter(string title, long durationMs)
        FFMetadataBuilder WithChapter(string title, double durationSeconds)
        string GetMetadataFileContent()

      Chapters are laid end to end, each starting where the previous one ended,
      on a 1/1000 timebase. WithTag throws if you add the same key twice.

  Feed either one to the argument builder:
      .AddMetaData(metaDataBuilder.Build())        // IReadOnlyMetaData overload
      .AddMetaData(ffMetadataBuilder)              // FFMetadataBuilder overload
      .AddMetaData(rawFfmetadataText)              // string overload
  Each writes a temporary FFMETADATA file, adds it as an extra input, and emits
  `-map_metadata {that input's index}`; the temp file is deleted afterwards.
  To copy metadata from an existing input instead, use
  `.MapMetaData()` (previous input) or `.MapMetaData(inputIndex)`. To strip all
  metadata, use `.WithoutMetadata()` on the OUTPUT options.

IMAGING BRIDGE (CodeBrix.VideoProcessing.Imaging - needs CodeBrix.Imaging)
---------------------------------------------------------------------------
    public static class FFMpegImage
        static Image Snapshot(string input, Size? size = null,
                  TimeSpan? captureTime = null, int? streamIndex = null,
                  int inputFileIndex = 0)
        static Task<Image> SnapshotAsync(string input, Size? size = null,
                  TimeSpan? captureTime = null, int? streamIndex = null,
                  int inputFileIndex = 0, CancellationToken cancellationToken = default)
        Returns a CodeBrix.Imaging.Image. The frame is produced by ffmpeg with
        the PNG image codec into a pipe and decoded with Image.Load - no
        temporary file is written. The returned Image is IDisposable: use it in
        a `using`.

    public static class ImageExtensions
        static bool AddAudio(this Image poster, string audio, string output)
        Writes the image to a temporary PNG in the process's current working
        directory, calls FFMpeg.PosterWithAudio, and deletes the temp file.

    public class ImageVideoFrameWrapper : IVideoFrame, IDisposable
        ImageVideoFrameWrapper(Image<Rgba32> image)   // throws on null
        Image<Rgba32> Source { get; }
        int Width { get; }   int Height { get; }
        string Format { get; }        // always "rgba"
        void Serialize(Stream pipe)
        Task SerializeAsync(Stream pipe, CancellationToken token)
        void Dispose()                // disposes the wrapped image
        Feed a sequence of these to RawVideoPipeSource to encode frames you
        generated in memory. Dispose() disposes Source, so do not dispose a
        wrapper whose image you still need.

EXTEND HELPERS (CodeBrix.VideoProcessing.Extend)
-------------------------------------------------
    public class PcmAudioSampleWrapper : IAudioSample
        PcmAudioSampleWrapper(byte[] sample)
        void Serialize(Stream stream)
        Task SerializeAsync(Stream stream, CancellationToken token)
        The only shipped IAudioSample. It writes the byte[] straight through, so
        YOU choose the sample layout: describe it to ffmpeg with
        RawAudioPipeSource.Format ("s16le", "s8", "f32le", ...), SampleRate and
        Channels, and hand it matching bytes.

    public static class TimeSpanExtensions
        static string ToLongString(this TimeSpan timeSpan)
        Renders "HH:mm:ss.fff" with days folded into the hours (so 1 day 2 hours
        becomes "26:00:00.000"). This is the format Seek/EndSeek emit.

    public static class UriExtensions
        static bool SaveStream(this Uri uri, string output)
        Shorthand for FFMpeg.SaveM3U8Stream(uri, output).

EXCEPTIONS (CodeBrix.VideoProcessing.Exceptions)
-------------------------------------------------
    public enum FFMpegExceptionType { Conversion, File, Operation, Process }

    public class FFMpegException : Exception
        FFMpegException(FFMpegExceptionType type, string message,
                        Exception innerException = null, string ffMpegErrorOutput = "")
        FFMpegException(FFMpegExceptionType type, string message,
                        string ffMpegErrorOutput = "")
        FFMpegException(FFMpegExceptionType type, string message)
        FFMpegExceptionType Type { get; }
        string FFMpegErrorOutput { get; }     // the captured stderr

    public class FFMpegStreamFormatException : FFMpegException
        FFMpegStreamFormatException(FFMpegExceptionType type, string message,
                                    Exception innerException = null)
        Thrown by RawVideoPipeSource when a frame's Width/Height/Format differs
        from the first frame's.

    public class FFMpegEncoderNotFoundException : FFMpegException
        FFMpegEncoderNotFoundException(string encoderName, string message,
            Exception innerException = null, string ffMpegErrorOutput = "")
        string EncoderName { get; }           // e.g. "libsvtav1"
        Thrown by ProcessSynchronously / ProcessAsynchronously, with
        Type == Process, when ffmpeg's stderr says "Unknown encoder '...'".
        The message is the usual "ffmpeg exited with non-zero exit-code (...)"
        text; for libsvtav1 an explanation and the
        FFOptions.AllowAv1EncoderFallback suggestion come in front of it.

    public class FFMpegArgumentException : Exception
        FFMpegArgumentException(string message = null, Exception innerException = null)
        Thrown when a filter argument list is empty at render time.

    public class FFOptionsException : Exception
        FFOptionsException(string message, Exception innerException = null)
        Thrown by the root checks when BinaryFolder is null.

    public class FFProbeException : Exception
        FFProbeException(string message, Exception inner = null)
        Thrown by FFProbeHelper.VerifyFFProbeExists when ffprobe is missing.
    public class FFProbeProcessException : FFProbeException
        FFProbeProcessException(string message,
            IReadOnlyCollection<string> processErrors, Exception inner = null)
        IReadOnlyCollection<string> ProcessErrors { get; }
        Part of the upstream-compatible surface, but nothing in this library
        throws it - a failing ffprobe run raises FFMpegException instead.
    public class FormatNullException : FFProbeException
        FormatNullException()          // "Format not specified"

  WHICH ONE TO CATCH
    * a failed CONVERSION       -> FFMpegException, Type == Process,
                                   stderr in FFMpegErrorOutput
    * a failed PROBE            -> FFMpegException, Type == Process
    * a missing probe input     -> FFMpegException, Type == File
    * a missing conversion input (verifyExists: true)
                                -> System.IO.FileNotFoundException, raised when
                                   the process starts
    * ffmpeg / ffprobe missing  -> FFMpegException (Type == Operation) /
                                   FFProbeException
    * a cancelled run           -> OperationCanceledException
    * a bad frame in a pipe     -> FFMpegStreamFormatException
    * an empty filter list      -> FFMpegArgumentException
    * a second -vf / -af chain on one output, or a ZScaleOptions that is empty
      or has a non-finite Npl
                                -> FFMpegArgumentException
    * a bad value handed to one of the newer filter / option arguments
                                -> System.ArgumentException,
                                   System.ArgumentOutOfRangeException or
                                   System.ArgumentNullException, thrown by the
                                   constructor as you BUILD the chain (an empty
                                   filter name or metadata key, a negative
                                   stream index or byte count, a non-positive or
                                   non-finite number, a null options object)
  Catching FFMpegException plus OperationCanceledException plus
  System.IO.IOException covers essentially every failure mode at RUN time; the
  System.Argument* validation above fires while you are still building, so it is
  a programming error rather than something to catch.

VENDORED PROCESS LAYER (CodeBrix.VideoProcessing.Instances)
------------------------------------------------------------
Used internally to launch ffmpeg/ffprobe. It is public, so you can drive any
other child process with it if you want a consistent line-streaming API.

    public static class Instance
        static IProcessInstance Start(string path, string arguments = "",
            EventHandler<string> outputHandler = default,
            EventHandler<string> errorHandler = default)
        static IProcessInstance Start(ProcessStartInfo startInfo,
            EventHandler<string> outputHandler, EventHandler<string> errorHandler)
        static IProcessResult Finish(string path, string arguments = "",
            EventHandler<string> outputHandler = default,
            EventHandler<string> errorHandler = default)
        static IProcessResult Finish(ProcessStartInfo startInfo,
            EventHandler<string> outputHandler = default,
            EventHandler<string> errorHandler = default)
        static Task<IProcessResult> FinishAsync(string path, string arguments = "",
            CancellationToken cancellationToken = default,
            EventHandler<string> outputHandler = default,
            EventHandler<string> errorHandler = default)
        static Task<IProcessResult> FinishAsync(ProcessStartInfo startInfo,
            CancellationToken cancellationToken = default,
            EventHandler<string> outputHandler = default,
            EventHandler<string> errorHandler = default)

    public class ProcessArguments
        ProcessArguments(string path, string arguments)
        ProcessArguments(ProcessStartInfo processStartInfo)
        bool IgnoreEmptyLines { get; set; }
        int DataBufferCapacity { get; set; } = int.MaxValue
        event EventHandler<IProcessResult> Exited
        event EventHandler<string> OutputDataReceived
        event EventHandler<string> ErrorDataReceived
        IProcessInstance Start()
    public static class ProcessArgumentsExtensions
        static IProcessResult StartAndWaitForExit(this ProcessArguments processArguments)
        static Task<IProcessResult> StartAndWaitForExitAsync(
            this ProcessArguments processArguments,
            CancellationToken cancellationToken = default)

  CAUTION: a SECOND public static class with the same name and the same two
  extension methods exists in the ROOT namespace
  (CodeBrix.VideoProcessing.ProcessArgumentsExtensions). Importing both
  `CodeBrix.VideoProcessing` and `CodeBrix.VideoProcessing.Instances` in one
  file makes `processArguments.StartAndWaitForExit()` ambiguous (CS0121) - call
  it through the class name, e.g.
  `CodeBrix.VideoProcessing.Instances.ProcessArgumentsExtensions
      .StartAndWaitForExit(processArguments)`.

    public interface IProcessInstance : IDisposable
        Task SendInputAsync(string input);   void SendInput(string input);
        Task<IProcessResult> WaitForExitAsync(CancellationToken cancellationToken = default);
        IProcessResult WaitForExit();
        IProcessResult Kill();
        IReadOnlyCollection<string> OutputData { get; }
        IReadOnlyCollection<string> ErrorData { get; }
        event EventHandler<IProcessResult> Exited;
        event EventHandler<string> OutputDataReceived;
        event EventHandler<string> ErrorDataReceived;
    public interface IProcessResult
        int ExitCode { get; }
        IReadOnlyList<string> OutputData { get; }
        IReadOnlyList<string> ErrorData { get; }
    public class ProcessInstance : IProcessInstance    // the implementation
    public class ProcessResult : IProcessResult        // the implementation

    Exceptions (CodeBrix.VideoProcessing.Instances.Exceptions):
    public class InstanceException : Exception
        InstanceException(string msg, Exception innerException)
    public class InstanceFileNotFoundException : InstanceException
        InstanceFileNotFoundException(string fileName, Exception innerException)
    public class InstanceProcessAlreadyExitedException : Exception
        InstanceProcessAlreadyExitedException(Exception innerException)


COMPLETE EXAMPLES
=================

1. Transcode with progress reporting and cancellation
------------------------------------------------------
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using CodeBrix.VideoProcessing;
    using CodeBrix.VideoProcessing.Enums;

    public static class Transcoder
    {
        public static async Task<bool> ToMp4Async(
            string input, string output, CancellationToken token)
        {
            IMediaAnalysis info = await FFProbe.AnalyseAsync(
                input, cancellationToken: token);

            try
            {
                return await FFMpegArguments
                    .FromFileInput(input)
                    .OutputToFile(output, overwrite: true, opt => opt
                        .WithVideoCodec(VideoCodec.LibX264)
                        .WithConstantRateFactor(23)
                        .WithSpeedPreset(Speed.Fast)
                        .WithAudioCodec(AudioCodec.Aac)
                        .WithAudioBitrate(AudioQuality.Good)
                        .WithFastStart())
                    .NotifyOnProgress(
                        percent => Console.Write($"\r{percent:F1}%   "),
                        info.Duration)
                    .NotifyOnProgress(
                        done => Console.Write($"[{done:hh\\:mm\\:ss}] "))
                    .NotifyOnError(line => Console.Error.WriteLine(line))
                    .WithLogLevel(FFMpegLogLevel.Error)
                    // give ffmpeg 3 seconds to flush a playable file on cancel
                    .CancellableThrough(token, timeout: 3000)
                    .ProcessAsynchronously();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("cancelled");
                return false;
            }
        }
    }

2. Pipe frames generated in memory INTO ffmpeg (CodeBrix.Imaging)
------------------------------------------------------------------
    using System.Collections.Generic;
    using CodeBrix.Imaging;
    using CodeBrix.Imaging.PixelFormats;
    using CodeBrix.VideoProcessing;
    using CodeBrix.VideoProcessing.Enums;
    using CodeBrix.VideoProcessing.Imaging;
    using CodeBrix.VideoProcessing.Pipes;

    static IEnumerable<IVideoFrame> Frames(int count, int w, int h)
    {
        for (int i = 0; i < count; i++)
        {
            var image = new Image<Rgba32>(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    image[x, y] = new Rgba32(
                        (byte)((x + i) % 256), (byte)(y % 256), (byte)i, 255);
                }
            }

            // ImageVideoFrameWrapper disposes the image when the frame is
            // disposed; yield it inside a using so each frame is released as
            // soon as it has been serialized to the pipe.
            using var frame = new ImageVideoFrameWrapper(image);
            yield return frame;
        }
    }

    var source = new RawVideoPipeSource(Frames(120, 320, 240)) { FrameRate = 30 };

    FFMpegArguments
        .FromPipeInput(source)
        .OutputToFile("generated.mp4", overwrite: true, opt => opt
            .WithVideoCodec(VideoCodec.LibX264)
            .ForcePixelFormat("yuv420p"))
        .ProcessSynchronously();

  Every frame MUST report the same Width, Height and Format as the first one, or
  RawVideoPipeSource throws FFMpegStreamFormatException.

3. Pipe raw PCM audio in, and read encoded bytes out
-----------------------------------------------------
    using System.Collections.Generic;
    using System.IO;
    using CodeBrix.VideoProcessing;
    using CodeBrix.VideoProcessing.Extend;
    using CodeBrix.VideoProcessing.Pipes;

    var samples = new List<IAudioSample>
    {
        new PcmAudioSampleWrapper(new byte[] { 0, 0 }),
        new PcmAudioSampleWrapper(new byte[] { 0, 0 }),
    };

    var audioSource = new RawAudioPipeSource(samples)
    {
        Format = "s8",       // must describe the bytes you actually wrote
        Channels = 2,
        SampleRate = 8000,
    };

    using var encoded = new MemoryStream();

    FFMpegArguments
        .FromPipeInput(audioSource)
        .OutputToPipe(new StreamPipeSink(encoded), opt => opt
            .ForceFormat("mp3"))     // a pipe has no extension: say the format
        .ProcessSynchronously();

    encoded.Position = 0;   // encoded now holds the mp3 bytes

4. Snapshot to a file, to an image, and an animated GIF
--------------------------------------------------------
    using System;
    using System.Drawing;
    using CodeBrix.VideoProcessing;
    using CodeBrix.VideoProcessing.Imaging;

    // to a file (extension must be .png/.jpg/.bmp/.webp)
    FFMpeg.Snapshot("input.mp4", "thumb.png",
        size: new Size(640, -1),            // -1 == keep the aspect ratio
        captureTime: TimeSpan.FromSeconds(5));

    // to an in-memory CodeBrix.Imaging image (no temp file)
    using (CodeBrix.Imaging.Image image =
               FFMpegImage.Snapshot("input.mp4", new Size(320, -1)))
    {
        image.SaveAsPng("thumb-small.png");
    }

    // an animated GIF of 3 seconds starting at 00:00:10
    FFMpeg.GifSnapshot("input.mp4", "preview.gif",
        size: new Size(480, -1),
        captureTime: TimeSpan.FromSeconds(10),
        duration: TimeSpan.FromSeconds(3));

5. Several outputs from one input
----------------------------------
    using CodeBrix.VideoProcessing;
    using CodeBrix.VideoProcessing.Enums;

    // (a) MultiOutput - independent encodes, one pass over the input
    FFMpegArguments
        .FromFileInput("input.mp4")
        .MultiOutput(outputs => outputs
            .OutputToFile("sd.mp4", true, o => o.Resize(1280, 720))
            .OutputToFile("hd.mp4", true, o => o.Resize(1920, 1080))
            .OutputToUrl("rtmp://server/live/key", o => o.ForceFormat("flv")))
        .ProcessSynchronously();

    // (b) OutputToTee - ONE encode duplicated to several destinations
    FFMpegArguments
        .FromFileInput("input.mp4")
        .OutputToTee(outputs => outputs
            .OutputToFile("archive.mp4", false, o => o.WithFastStart())
            .OutputToUrl("http://server/path", o => o
                .ForceFormat("mpegts")
                .SelectStream(0, channel: Channel.Video)),
            // options applied to the single shared encode:
            opt => opt
                .WithVideoCodec(VideoCodec.LibX264)
                .WithAudioCodec(AudioCodec.Aac))
        .ProcessSynchronously();

6. Read and write metadata, tags and chapters
-----------------------------------------------
    using System;
    using System.Linq;
    using CodeBrix.VideoProcessing;
    using CodeBrix.VideoProcessing.Builders.MetaData;
    using CodeBrix.VideoProcessing.Enums;

    // WRITE: tags + chapters onto a new file
    FFMpegArguments
        .FromFileInput("input.webm")
        .AddMetaData(FFMetadataBuilder.Empty()
            .WithTag("title", "My Video")
            .WithTag("artist", "Nobody")
            .WithChapter("Intro", 12.5)
            .WithChapter("Main", 90.0))
        .OutputToFile("tagged.mp4", true, opt => opt
            .WithVideoCodec(VideoCodec.LibX264))
        .ProcessSynchronously();

    // the MetaDataBuilder route, for the richer tag vocabulary
    IReadOnlyMetaData meta = new MetaDataBuilder()
        .WithTitle("Kanon und Gigue")
        .WithArtists("Pachelbel")
        .WithAlbumArtists("Pachelbel")
        .WithGenres("Classics", "Baroque")
        .WithCopyright("Public domain")
        .AddChapter(TimeSpan.FromSeconds(30), "Kanon")
        .AddChapter(TimeSpan.FromSeconds(45), "Gigue")
        .Build();

    string ffmetadata = MetaDataSerializer.Instance.Serialize(meta);  // optional
    FFMpegArguments
        .FromFileInput("input.mp3")
        .AddMetaData(meta)
        .OutputToFile("tagged.mp3", true, opt => opt.WithCopyCodec())
        .ProcessSynchronously();

    // READ it back
    IMediaAnalysis probed = FFProbe.Analyse("tagged.mp4");
    probed.Format.Tags.TryGetValue("title", out string title);
    Console.WriteLine($"{title} - {probed.Format.GetCreationTime()}");
    foreach (ChapterData chapter in probed.Chapters)
    {
        Console.WriteLine($"{chapter.Start}-{chapter.End}  {chapter.Title}");
    }
    foreach (AudioStream a in probed.AudioStreams)
    {
        Console.WriteLine($"#{a.Index} {a.CodecName} {a.Channels}ch " +
                          $"{a.SampleRateHz}Hz {a.GetLanguage()}");
    }

7. Burn subtitles into the picture, and draw text over it
----------------------------------------------------------
    using CodeBrix.VideoProcessing;
    using CodeBrix.VideoProcessing.Arguments;
    using CodeBrix.VideoProcessing.Enums;

    FFMpegArguments
        .FromFileInput("input.mp4")
        .OutputToFile("subtitled.mp4", true, opt => opt
            .WithVideoCodec(VideoCodec.LibX264)
            .WithVideoFilters(f => f
                .HardBurnSubtitle(SubtitleHardBurnOptions
                    .Create("subtitles.srt")
                    .SetCharacterEncoding("UTF-8")
                    .SetOriginalSize(1920, 1080)
                    .SetSubtitleIndex(0)
                    .WithStyle(StyleOptions.Create()
                        .WithParameter("FontName", "DejaVu Serif")
                        .WithParameter("FontSize", "24")
                        .WithParameter("PrimaryColour", "&HAA00FF00")))
                .DrawText(DrawTextOptions
                    .Create("Draft copy", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")
                    .WithParameter("fontcolor", "white")
                    .WithParameter("fontsize", "24")
                    .WithParameter("box", "1")
                    .WithParameter("boxcolor", "black@0.5")
                    .WithParameter("x", "(w-text_w)/2")
                    .WithParameter("y", "(h-text_h)/2"))))
        .ProcessSynchronously();

8. Escape hatches: a raw flag and a raw IArgument
--------------------------------------------------
    using CodeBrix.VideoProcessing;
    using CodeBrix.VideoProcessing.Arguments;
    using CodeBrix.VideoProcessing.Enums;

    // a filter graph this API does not model, plus a custom IArgument.
    // -max_muxing_queue_size is a real ffmpeg OUTPUT option that this library
    // models with no fluent method, which is exactly what IArgument is for.
    // Do NOT hand-roll -noautorotate here: AutoRotate(bool) already ships it,
    // and it only works on the INPUT side anyway.
    sealed class MuxingQueueSizeArgument : IArgument
    {
        private readonly int _packets;
        public MuxingQueueSizeArgument(int packets) { _packets = packets; }
        public string Text => $"-max_muxing_queue_size {_packets}";
    }

    string mixed = FFMpegArguments
        .FromFileInput(new[] { "a.mp3", "b.mp3" })
        .OutputToFile("mixed.mp3", true, opt => opt
            .WithArgument(new MuxingQueueSizeArgument(1024))
            .WithCustomArgument(
                "-filter_complex \"[0:0][1:0]amix=inputs=2:duration=longest[final]\" " +
                "-map \"[final]\"")
            .WithAudioCodec(AudioCodec.LibMp3Lame)
            .WithAudioBitrate(128)
            .WithAudioSamplingRate()
            .WithoutMetadata())
        .Arguments;      // inspect it before running


MINIMUM VIABLE PROJECT
======================
MyVideoTool.csproj

    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>disable</Nullable>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="CodeBrix.VideoProcessing.MitLicenseForever"
                          Version="*" />
      </ItemGroup>
    </Project>

  (Replace Version="*" with the version you intend to pin.)

Program.cs

    using System;
    using System.Drawing;
    using System.Threading.Tasks;
    using CodeBrix.VideoProcessing;
    using CodeBrix.VideoProcessing.Enums;
    using CodeBrix.VideoProcessing.Exceptions;
    using CodeBrix.VideoProcessing.Helpers;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("usage: MyVideoTool <input> <output.mp4>");
                return 2;
            }

            // Optional: point at a private ffmpeg build instead of PATH.
            // GlobalFFOptions.Configure(o => o.BinaryFolder = "/opt/ffmpeg/bin");

            try
            {
                FFMpegHelper.VerifyFFMpegExists(GlobalFFOptions.Current);
                FFProbeHelper.VerifyFFProbeExists(GlobalFFOptions.Current);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"ffmpeg/ffprobe not available: {ex.Message}");
                return 3;
            }

            IMediaAnalysis info = await FFProbe.AnalyseAsync(args[0]);
            Console.WriteLine($"{info.Duration} " +
                $"{info.PrimaryVideoStream?.Width}x{info.PrimaryVideoStream?.Height}");

            try
            {
                await FFMpegArguments
                    .FromFileInput(args[0])
                    .OutputToFile(args[1], overwrite: true, opt => opt
                        .WithVideoCodec(VideoCodec.LibX264)
                        .WithConstantRateFactor(23)
                        .WithAudioCodec(AudioCodec.Aac)
                        .WithFastStart())
                    .NotifyOnProgress(p => Console.Write($"\r{p:F0}%"), info.Duration)
                    .ProcessAsynchronously();
            }
            catch (FFMpegException ex)
            {
                Console.Error.WriteLine(ex.FFMpegErrorOutput);
                return 1;
            }

            FFMpeg.Snapshot(args[1], "thumb.png", new Size(640, -1));
            Console.WriteLine("\ndone");
            return 0;
        }
    }

Run it with `ffmpeg` and `ffprobe` installed and on PATH (Debian-based Linux:
`sudo apt install ffmpeg`).


PERFORMANCE TIPS
================
  * The cost is ffmpeg's, not this library's. The wrapper adds one child
    process, a regex over stderr lines, and (for pipes) one background copy
    task. Optimise the ffmpeg command line, not the C#.
  * Leave FFOptions.UseCache = true. The codec / pixel-format / container
    catalogues are parsed from `ffmpeg -codecs`, `-encoders`, `-decoders`,
    `-pix_fmts` and `-formats`. With the cache on, each catalogue is read ONCE
    per process; with it off, EVERY VideoCodec.LibX264-style property access
    launches ffmpeg again.
  * Prefer string codec names (`WithVideoCodec("libx264")`) in hot paths or in
    code that only renders a command line: the strongly-typed VideoCodec /
    AudioCodec / VideoType / FileExtension members go through FFMpeg.GetCodec
    and therefore need ffmpeg on the machine even when you are not converting.
  * Use `.WithCopyCodec()` / `.CopyChannel()` whenever you are only re-muxing.
    Remuxing is orders of magnitude faster than re-encoding.
  * Seek on the INPUT side, not the output side: `FromFileInput(path, true,
    o => o.Seek(t))` lets ffmpeg jump to the keyframe, whereas seeking in the
    output options decodes everything up to `t` first.
  * Reach for UsingMultithreading(true)/UsingThreads(n) only when you know the
    encoder benefits; libx264 already threads internally.
  * Prefer ProcessAsynchronously in server code. ProcessSynchronously blocks
    the calling thread for the whole conversion.
  * Pipes avoid temp-file I/O entirely, but every frame crosses a named pipe -
    keep IVideoFrame.Serialize allocation-free where you can (the shipped
    ImageVideoFrameWrapper allocates one byte[] per frame).
  * Analysing a Stream pipes the WHOLE stream to ffprobe; analysing a path lets
    ffprobe seek. Prefer the path overload when you have a file.
  * Progress parsing only runs when you register a callback; error-line parsing
    only when you register NotifyOnError or a progress callback. Registering
    nothing is the cheapest configuration.


COMMON PITFALLS TO AVOID
========================
  * ffmpeg/ffprobe missing at runtime. This is the number-one failure. Verify at
    startup with FFMpegHelper.VerifyFFMpegExists / FFProbeHelper.VerifyFFProbeExists,
    and remember that BinaryFolder is searched as
    {BinaryFolder}/{x64|x86}/ before {BinaryFolder}/ before PATH.
  * ONE -vf (and one -af) per output. A second WithVideoFilters call on the same
    output throws FFMpegArgumentException; build the whole chain in one call.
  * Crop() does NOT go through the filter list, so the throw above does not
    catch it. CropArgument renders its own `-vf crop=...`, so combining
    `.Crop(...)` with `.WithVideoFilters(...)` still puts TWO `-vf` flags on one
    output and ffmpeg honours only the last. Do the crop inside the filter chain
    instead - `.WithVideoFilters(f => f.Custom("crop", "w:h:x:y") ...)` - and
    always eyeball `.Arguments`.
  * WithGifPaletteArgument renders `-filter_complex`, which also conflicts with
    a `-vf` on the same output.
  * NotifyOnProgress(Action<double>, TimeSpan) needs the total duration and it
    is NOT optional - probe the input first. Without it the percentage callback
    is never invoked (only the TimeSpan overload fires).
  * The async FFProbe overloads take cancellationToken as the THIRD parameter,
    before customArguments. Always pass it by name.
  * Pipe frames must be consistent: every IVideoFrame handed to a
    RawVideoPipeSource must match the first frame's Width, Height and Format or
    you get FFMpegStreamFormatException. An empty frame sequence throws
    InvalidOperationException.
  * A pipe output has no file extension, so ffmpeg cannot guess the container:
    call ForceFormat(...) in the output options of every OutputToPipe.
    StreamPipeSink.Format is a property of the sink, not the ffmpeg flag.
  * RawAudioPipeSource.Format/SampleRate/Channels must describe the bytes you
    actually write. PcmAudioSampleWrapper passes your byte[] through untouched.
  * OutputToTee requires at least one output (ArgumentException otherwise) and
    exactly one output argument per tee branch; tee branch options are rendered
    as bracketed tee parameters, so only put options the tee muxer understands
    there. If you want independent encodes, use MultiOutput instead.
  * FFOptions.Encoding is [JsonIgnore]; the value that round-trips through
    ffmpeg.config.json is EncodingWebName.
  * GlobalFFOptions.Configure(FFOptions) REPLACES the process-wide options;
    Configure(Action<FFOptions>) mutates them in place. Both are process-wide
    and not thread-safe to change mid-flight - configure once at startup.
    FFMpegArgumentProcessor.Configure(...) is the per-run alternative and never
    touches the globals.
  * `Size` is System.Drawing.Size. A width or height of -1 (or 0) means "derive
    it from the other dimension"; the snapshot helpers also swap the two when
    the source stream has a 90/180 degree rotation.
  * FFMpeg.Convert throws ArgumentException when either source dimension is odd
    (a yuv420p requirement), and FFMpegException when the output extension does
    not match the requested container.
  * Snapshot output extensions are restricted to .png/.jpg/.bmp/.webp, and
    GifSnapshot to .gif.
  * FFMpegArguments is single-use and stateful: each terminal call appends the
    output argument to the SAME instance. Build a fresh
    `FFMpegArguments.FromXxx(...)` chain for every conversion.
  * VerbosityLevel (Arguments namespace, global `-loglevel`) and FFMpegLogLevel
    (Enums namespace, `WithLogLevel` / FFOptions.LogLevel) are two different
    enums with different numeric values. Do not cast between them.
  * MetaDataBuilder.WithEntry APPENDS to an existing key ("old; new") instead of
    replacing it; FFMetadataBuilder.WithTag throws on a duplicate key.
  * CropArgument's constructor takes (size, top, left) while the fluent
    Crop(size, left, top) takes them the other way round.
  * A cancelled run throws OperationCanceledException from Process* unless you
    pass throwOnError: false; a non-zero ffmpeg exit throws FFMpegException with
    the stderr in FFMpegErrorOutput. Catch both.
  * A missing INPUT file is not caught while you build the chain. With
    verifyExists: true it surfaces as System.IO.FileNotFoundException when the
    conversion starts; a missing PROBE input is FFMpegException with
    Type == File. FFProbeProcessException exists but is never thrown - do not
    write a catch block for it.
  * Two different public classes are both named ProcessArgumentsExtensions, one
    in CodeBrix.VideoProcessing and one in CodeBrix.VideoProcessing.Instances,
    with the same extension methods. Importing both namespaces in one file makes
    StartAndWaitForExit/StartAndWaitForExitAsync ambiguous (CS0121).
  * OutputToFile(path, overwrite: false) does not just omit `-y`: it also throws
    FFMpegException(File) at start-up when the output file already exists.
  * -autorotate / -noautorotate is an INPUT option. AutoRotate(...) placed in an
    OUTPUT callback renders, and ffmpeg ignores it; put it in the addArguments
    callback of FromFileInput / AddFileInput instead.
  * Never hand a path straight to Custom("raw filter text") - that overload
    escapes nothing. Use Lut3D(path), or Custom(name, ("file", path)), so the
    path gets the full filtergraph escaping. A Windows path is the classic
    casualty: the drive colon ends the option and the backslashes disappear.
  * zscale needs to know the colour space it is converting FROM. On a source
    with unspecified colour metadata, or straight after a filter that hands it
    RGB, it fails with "code 3074: no path between colorspaces" until you set
    Matrix (or zscale's own input-side min/tin/pin through Custom).
  * Do not assume every ffmpeg has SVT-AV1. A libsvtav1 encode on a build
    without it throws FFMpegEncoderNotFoundException. Either install a build
    that has it, or opt in to FFOptions.AllowAv1EncoderFallback (libaom-av1,
    much slower) - and select the encoder with WithVideoCodec("libsvtav1") or a
    custom argument that is exactly "-c:v libsvtav1", so the fallback can
    recognise it.
  * Nullable reference types are DISABLED in this assembly, so the compiler will
    not warn you about the many members that can legitimately be null
    (PrimaryVideoStream, Tags, Disposition, MediaFormat fields). Null-check them.


WHAT THIS PACKAGE DOES NOT DO
=============================
  * It does not decode or encode anything itself. There is no codec, no
    demuxer, no filter implementation in this assembly - ffmpeg does all of it
    in a separate process.
  * It does not ship, download or install the ffmpeg/ffprobe binaries. The
    upstream auto-downloader extension was not ported; install them yourself or
    set FFOptions.BinaryFolder.
  * It does not P/Invoke libavcodec/libavformat, so there is no in-process
    frame-accurate decoding API and no access to ffmpeg internals.
  * It does not give you a filter-graph object model. Anything beyond the
    modelled -vf/-af filters goes through WithCustomArgument or a custom
    IArgument.
  * It has no UI, no player, no rendering surface and no hardware-device
    enumeration; WithHardwareAcceleration only passes `-hwaccel <name>` through
    to ffmpeg.
  * It does not do image processing. Resizing, drawing and format conversion of
    still images belong to CodeBrix.Imaging; this package only bridges to it
    (FFMpegImage, ImageVideoFrameWrapper, ImageExtensions.AddAudio).
  * It does not stream-copy safety-check your command line, validate codec /
    container combinations, or repair broken inputs - ffmpeg's own error output
    is what you get.
  * The FFMpegCore.Extensions.* upstream packages (SkiaSharp,
    System.Drawing.Common, Downloader) are not ported and have no equivalent
    package here.
  * It is not thread-safe at the configuration level: GlobalFFOptions and the
    ffmpeg capability cache are process-wide singletons.


WORKING EXAMPLES ON GITHUB
==========================
The test suite is the largest body of verified, compilable usage. Browse it at
https://github.com/ellisnet/CodeBrix.VideoProcessing/tree/main/tests - the paths
in the table below are relative to
https://github.com/ellisnet/CodeBrix.VideoProcessing/tree/main/

  Feature                             File
  ----------------------------------  ------------------------------------------
  Every rendered command line, all
  fluent methods, filters, tee and
  multi-output, drawtext, subtitle
  hard-burn, pad, gif palette,
  Audible keys, multi-input mixing    tests/CodeBrix.VideoProcessing.Tests/
                                        ArgumentBuilderTest.cs
  Transcoding, snapshots, gif
  snapshots, video pipes, progress
  callbacks, metadata + chapters,
  cancellation, image bridge          tests/CodeBrix.VideoProcessing.Tests/
                                        VideoTest.cs
  Audio conversion, audio pipes,
  PcmAudioSampleWrapper,
  RawAudioPipeSource settings,
  poster-with-audio                   tests/CodeBrix.VideoProcessing.Tests/
                                        AudioTest.cs
  FFProbe.Analyse on paths, URIs and
  streams; GetFrames / GetPackets;
  rotation, HDR and chapter parsing   tests/CodeBrix.VideoProcessing.Tests/
                                        FFProbeTests.cs
  MetaDataBuilder, MetaDataSerializer
  and -map_metadata index maths       tests/CodeBrix.VideoProcessing.Tests/
                                        MetaDataBuilderTests.cs
  FFOptions defaults, ffmpeg.config.
  json loading, per-run Configure     tests/CodeBrix.VideoProcessing.Tests/
                                        FFMpegOptionsTests.cs and
                                        FFMpegArgumentProcessorTest.cs
  The newer FFMpegArgumentOptions
  options - subtitle codec, encoder
  params, numeric preset, metadata
  and stream metadata, dispositions,
  cues_to_front, reserve_index_space,
  autorotate                          tests/CodeBrix.VideoProcessing.Tests/
                                        FFMpegArgumentOptionsTests.cs
  The newer video filters - Custom,
  Fps, Lut3D, Format, ZScale,
  Tonemap, Rotate - their rendered
  text, their escaping and the one
  -vf per output guard                tests/CodeBrix.VideoProcessing.Tests/
                                        VideoFilterOptionsTests.cs
  A lut3d + zscale + fps + format
  chain actually run through ffmpeg   tests/CodeBrix.VideoProcessing.Tests/
                                        VideoFilterIntegrationTests.cs
  PixelFormat catalogue parsing       tests/CodeBrix.VideoProcessing.Tests/
                                        PixelFormatTests.cs
  The vendored process layer          tests/CodeBrix.VideoProcessing.Tests/
                                        InstancesTests.cs
  Generating IVideoFrame sequences
  on CodeBrix.Imaging                 tests/CodeBrix.VideoProcessing.Tests/
                                        Utilities/BitmapSource.cs

Direct link to the argument-builder tests, the best single file to read when you
need to know exactly what a fluent call renders:
https://github.com/ellisnet/CodeBrix.VideoProcessing/tree/main/tests/CodeBrix.VideoProcessing.Tests/ArgumentBuilderTest.cs


QUICK REFERENCE CARD
====================
    Package .... CodeBrix.VideoProcessing.MitLicenseForever   (MIT)
    Depends on . CodeBrix.Imaging.ApacheLicenseForever
    Needs ...... the ffmpeg and ffprobe executables at RUNTIME
    TFM ........ net10.0 or later

    PROBE
      IMediaAnalysis a = FFProbe.Analyse(path);
      await FFProbe.AnalyseAsync(path, cancellationToken: token);
      a.Duration / a.Format / a.Chapters / a.PrimaryVideoStream
      a.VideoStreams / a.AudioStreams / a.SubtitleStreams / a.ErrorData
      FFProbe.GetFrames(path).Frames ; FFProbe.GetPackets(path).Packets

    CONVERT
      FFMpegArguments
          .FromFileInput(in) | FromPipeInput | FromUrlInput | FromDeviceInput
          | FromConcatInput | FromDemuxConcatInput
          .AddFileInput(...) / .AddMetaData(...) / .MapMetaData()
          .WithGlobalOptions(o => o.WithVerbosityLevel(VerbosityLevel.Error))
          .OutputToFile(out, overwrite, opt => ...)
          | .OutputToPipe(sink, opt => ...) | .OutputToUrl(...)
          | .OutputToTee(outs, opt) | .MultiOutput(outs)
          .NotifyOnProgress(pct => ..., total) / .NotifyOnProgress(t => ...)
          .NotifyOnOutput(...) / .NotifyOnError(...) / .WithLogLevel(...)
          .Configure(o => ...) / .CancellableThrough(token, timeoutMs)
          .ProcessSynchronously() | await .ProcessAsynchronously()
      .Arguments  -> the rendered command line (no process is started)

    OUTPUT OPTIONS (most used)
      WithVideoCodec / WithAudioCodec / WithCopyCodec / CopyChannel
      WithConstantRateFactor / WithVideoBitrate / WithAudioBitrate
      WithSpeedPreset / WithFramerate / WithAudioSamplingRate
      Resize / Crop / WithVideoFilters / WithAudioFilters
      Seek / EndSeek / WithDuration / WithFrameOutputCount / Loop
      SelectStream(s) / DeselectStream(s) / DisableChannel
      ForceFormat / ForcePixelFormat / WithFastStart / WithoutMetadata
      OverwriteExisting / UsingShortest / UsingThreads / UsingMultithreading
      WithHardwareAcceleration / WithBitStreamFilter / WithTagVersion
      WithSubtitleCodec / WithEncoderParams / WithMetadata / WithStreamMetadata
      WithDisposition / CuesToFront / ReserveIndexSpace
      WithGifPaletteArgument / WithCustomArgument / WithArgument

    INPUT OPTIONS (in the addArguments callback of FromFileInput/AddFileInput)
      AutoRotate(false) -> -noautorotate, emitted before its -i

    VIDEO FILTERS   f => f.Scale | Transpose | Mirror | Pad | DrawText
                         | HardBurnSubtitle | BlackDetect | BlackFrame
                         | Custom | Fps | Lut3D | Format | ZScale | Tonemap
                         | Rotate
                    ONE WithVideoFilters call per output - a second throws.
                    Custom(name, (k, v)...) and Lut3D escape their values;
                    Custom("raw text") escapes nothing.
    AUDIO FILTERS   f => f.Pan | DynamicNormalizer | HighPass | LowPass
                         | AudioGate | SilenceDetect
                    ONE WithAudioFilters call per output - a second throws.
                    No Custom(...) here: add a CustomFilterArgument to
                    f.Arguments yourself - it is an IAudioFilterArgument too.

    ONE-SHOTS
      FFMpeg.Snapshot / SnapshotAsync / GifSnapshot / GifSnapshotAsync
      FFMpeg.SubVideo / SubVideoAsync / Convert / Join / JoinImageSequence
      FFMpeg.PosterWithAudio / Mute / ExtractAudio / ReplaceAudio
      FFMpeg.SaveM3U8Stream / GetCodecs / GetPixelFormats / GetContainerFormats

    PIPES
      new RawVideoPipeSource(frames) { FrameRate = 30 }
      new RawAudioPipeSource(samples) { Format = "s16le", SampleRate = 44100,
                                        Channels = 2 }
      new StreamPipeSource(stream) ; new StreamPipeSink(stream)
      implement IVideoFrame / IAudioSample for your own data

    IMAGES (CodeBrix.VideoProcessing.Imaging)
      FFMpegImage.Snapshot / SnapshotAsync -> CodeBrix.Imaging.Image
      new ImageVideoFrameWrapper(image)    -> IVideoFrame ("rgba")
      poster.AddAudio(audioPath, outputPath)

    CONFIG
      GlobalFFOptions.Configure(new FFOptions { BinaryFolder = "..." });
      GlobalFFOptions.Configure(o => o.LogLevel = FFMpegLogLevel.Error);
      GlobalFFOptions.Configure(o => o.AllowAv1EncoderFallback = true);
                          // libsvtav1 missing -> encode with libaom-av1
      ffmpeg.config.json in the working directory is loaded automatically.

    ERRORS
      FFMpegException (Type, FFMpegErrorOutput) - conversions AND probes
      FFMpegEncoderNotFoundException (EncoderName) - ffmpeg lacks an encoder;
                                for libsvtav1 the message offers the fallback
      FFMpegStreamFormatException - a pipe frame that changed size/format
      FFMpegArgumentException - an empty -vf / -af list, a second -vf / -af on
                                one output, an empty ZScaleOptions, a
                                non-finite ZScaleOptions.Npl
      System.ArgumentException / ArgumentOutOfRangeException /
      ArgumentNullException   - the newer filter and option arguments validate
                                eagerly in their constructors, so a bad name,
                                key, index or number throws while you build
      FFOptionsException - BinaryFolder set to null
      FFProbeException - ffprobe not found; FormatNullException - no "format"
      System.IO.FileNotFoundException - missing input, at process start
      OperationCanceledException - a cancelled run

================================================================================
END OF AGENT-README
================================================================================
