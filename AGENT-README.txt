========================================================================
AGENT-README: CodeBrix.VideoProcessing
A Comprehensive Guide for AI Coding Agents
========================================================================

This document is the single source of truth for understanding, consuming,
and modifying CodeBrix.VideoProcessing. Read it in full before working on
the library. It describes what the library is, HOW IT WORKS internally,
the complete public API surface, the coding conventions, and the known
gotchas.


========================================================================
1. OVERVIEW
========================================================================
CodeBrix.VideoProcessing is a fully managed, cross-platform FFmpeg /
FFprobe wrapper for .NET 10. It lets you analyse media (durations,
streams, codecs, resolutions, bitrates), convert / transcode / mux video
and audio, extract snapshots and GIFs, pipe raw frames or byte streams
into and out of FFmpeg, build FFmpeg metadata, and bridge video frames to
and from in-memory images.

Lineage / provenance:
  * It is a faithful PORT of FFMpegCore 5.4.0 (github.com/rosenbjerg/
    FFMpegCore, MIT, by Malte Rosenbjerg, Vlad Jerca, Max Bagryantsev),
    re-namespaced FFMpegCore.* -> CodeBrix.VideoProcessing.* and
    retargeted to net10.0.
  * The Instances 3.0.2 process-wrapper library (github.com/rosenbjerg/
    Instances, MIT) is VENDORED directly in under
    CodeBrix.VideoProcessing.Instances, so there is no dependency on a
    separate "Instances" package.
  * Every ported .cs file carries a `//was previously: <upstream-ns>;`
    provenance comment on its namespace line.

IMPORTANT - this is a WRAPPER, not a codec. At runtime it launches the
external `ffmpeg` and `ffprobe` executables and parses their output. Those
binaries MUST be installed and discoverable:
  * on the system PATH, or
  * in a folder configured via GlobalFFOptions.Configure(new FFOptions {
    BinaryFolder = "..." }), or the Extensions.Downloader equivalent (not
    ported here).
The library itself is 100% managed C#; it does NOT bundle the FFmpeg
binaries and does NOT P/Invoke into libav.


========================================================================
2. INSTALLATION
========================================================================
NuGet package:  CodeBrix.VideoProcessing.MitLicenseForever

    dotnet add package CodeBrix.VideoProcessing.MitLicenseForever

The package id carries the ".MitLicenseForever" license suffix; the
namespace you write in code is just "CodeBrix.VideoProcessing". Target
framework: .NET 10.0 or higher.

Single NuGet dependency: CodeBrix.Imaging (CodeBrix.Imaging.Apache-
LicenseForever), used ONLY by the in-memory image bridge in the
CodeBrix.VideoProcessing.Imaging namespace. CodeBrix.Imaging is itself a
fully managed, cross-platform, dependency-free CodeBrix library (an
ImageSharp fork). System.Text.Json is used from the .NET 10 shared
framework, so it is not a separate package reference.


========================================================================
3. QUICK START
========================================================================
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

    FFMpeg.Snapshot("input.mp4", "thumb.png", new Size(1920, 1080), TimeSpan.FromSeconds(10));

    using CodeBrix.Imaging.Image image = FFMpegImage.Snapshot("input.mp4", captureTime: TimeSpan.FromSeconds(10));


========================================================================
4. KEY NAMESPACES
========================================================================
    using CodeBrix.VideoProcessing;                 // FFMpeg, FFProbe,
                                                    // FFMpegArguments,
                                                    // FFMpegArgumentOptions,
                                                    // FFMpegArgumentProcessor,
                                                    // FFOptions, GlobalFFOptions,
                                                    // SnapshotArgumentBuilder,
                                                    // FFMetadataInputArgument
    using CodeBrix.VideoProcessing.Arguments;       // one type per ffmpeg arg +
                                                    // VideoFilterOptions /
                                                    // AudioFilterOptions
    using CodeBrix.VideoProcessing.Builders.MetaData; // MetaDataBuilder, MetaData,
                                                    // ChapterData, MetaDataSerializer
    using CodeBrix.VideoProcessing.Enums;           // VideoCodec, AudioCodec, Codec,
                                                    // ContainerFormat, VideoSize,
                                                    // PixelFormat, Speed, AudioQuality,
                                                    // FileExtension, FFMpegLogLevel,
                                                    // Mirroring, Transposition,
                                                    // HardwareAccelerationDevice
    using CodeBrix.VideoProcessing.Exceptions;      // FFMpegException, FFProbeException, ...
    using CodeBrix.VideoProcessing.Pipes;           // IPipeSource, IPipeSink,
                                                    // IVideoFrame, IAudioSample,
                                                    // RawVideoPipeSource,
                                                    // RawAudioPipeSource,
                                                    // StreamPipeSource, StreamPipeSink
    using CodeBrix.VideoProcessing.Imaging;         // FFMpegImage, ImageExtensions,
                                                    // ImageVideoFrameWrapper
    using CodeBrix.VideoProcessing.Instances;       // Instance, ProcessArguments,
                                                    // IProcessInstance, IProcessResult
    using CodeBrix.VideoProcessing.Instances.Exceptions;


========================================================================
5. HOW IT WORKS  (execution model - read this to understand the internals)
========================================================================
A conversion is built as a data structure, rendered to an ffmpeg command
line, executed as a child process, and its stdout/stderr are parsed. The
flow, end to end:

  (a) BUILD INPUTS.  FFMpegArguments is the root builder. A static factory
      (FromFileInput / FromPipeInput / FromUrlInput / FromDeviceInput /
      FromConcatInput / FromDemuxConcatInput) creates it with one input;
      instance AddXxxInput methods append more inputs; WithGlobalOptions
      adds pre-input global flags (e.g. -hide_banner). Internally it holds
      an ordered list of IInputArgument objects (each an IArgument that
      knows how to render its ffmpeg text).

  (b) BUILD OUTPUT + OPTIONS.  Calling OutputToFile / OutputToPipe /
      OutputToUrl / OutputToTee / MultiOutput takes an
      Action<FFMpegArgumentOptions> callback. FFMpegArgumentOptions is the
      OUTPUT-side builder: every WithXxx / Using / Select / Resize / Seek /
      ForceFormat method appends an IArgument to that output. This returns
      an FFMpegArgumentProcessor - the object that will actually run ffmpeg.

  (c) RENDER.  FFMpegArgumentProcessor exposes the fully rendered command
      line via the `.Arguments` (and `.Text`) property - useful for unit
      testing without running ffmpeg. Argument ordering is: global options,
      then each input's args, then each output's args. Pipe inputs/outputs
      contribute their own `-f rawvideo -pix_fmt ... -s WxH` / format flags.

  (d) EXECUTE.  ProcessSynchronously() / ProcessAsynchronously() resolve
      the ffmpeg binary path (GlobalFFOptions.GetFFMpegBinaryPath()), build
      a ProcessArguments (the vendored Instances type), and Start() it. The
      vendored process layer redirects stdin/stdout/stderr, streams output
      line-by-line, raises Exited/OutputDataReceived/ErrorDataReceived
      events, and supports WaitForExit / WaitForExitAsync / Kill /
      SendInput. On a non-zero exit code an FFMpegException is thrown with
      the captured stderr.

  (e) PROGRESS + LOGGING.  NotifyOnProgress / NotifyOnOutput / NotifyOnError
      register callbacks; the processor parses ffmpeg's stderr progress
      lines (time=..., etc.) into percentages / TimeSpans. WithLogLevel /
      the global VerbosityLevel argument control ffmpeg -loglevel.

  (f) CANCELLATION.  See section 6.


========================================================================
6. CANCELLATION MODEL  (important + has a known environmental gotcha)
========================================================================
FFMpegArgumentProcessor.CancellableThrough(...) wires cancellation:

    .CancellableThrough(out Action cancel, int timeout = 0)   // manual trigger
    .CancellableThrough(CancellationToken token, int timeout = 0)

When triggered (either the returned Action is invoked, or the token is
cancelled), the processor runs, on the cancelling thread:

    1. instance.SendInput("q")     // ask ffmpeg to quit GRACEFULLY
    2. wait up to `timeout` ms for the process to exit on its own
    3. if it has not exited: cancellationTokenSource.Cancel() + instance.Kill()

With the default timeout = 0, step 2 returns immediately and the process is
killed. A non-zero timeout gives ffmpeg a grace period to flush a valid
output file before being force-killed. If cancellation happened before the
process started, an OperationCanceledException is thrown up front.

KNOWN ISSUE (environmental, documented in the tests): on some hosts /
ffmpeg builds (observed with ffmpeg 7.1.5 on Debian), the graceful
SendInput("q") step does not return, so runtime cancellation of a running
ffmpeg can hang before reaching the Kill(). The ported cancellation code is
byte-identical to upstream FFMpegCore, and the vendored process Kill /
SendInput primitives pass their own unit tests - so this is an
ffmpeg-interaction issue, not a defect in this port. The affected
integration tests are marked [Fact(Skip=...)] (see section 12). If you rely
on runtime cancellation, prefer passing a non-zero `timeout` and/or verify
behaviour on your target ffmpeg build.


========================================================================
7. PIPE MODEL  (streaming frames / bytes without temp files)
========================================================================
Pipes let you feed data into ffmpeg or read its output in-process:

  INPUT sources (IPipeSource):
    * RawVideoPipeSource(IEnumerable<IVideoFrame>) - streams raw video
      frames. It reads the FIRST frame to fix the stream format and emits
      `-f rawvideo -r {FrameRate} -pix_fmt {Format} -s {Width}x{Height}`,
      then validates that EVERY subsequent frame has the same Width, Height
      and Format - a mismatch throws FFMpegStreamFormatException BEFORE the
      offending frame is serialized. FrameRate defaults to 25.
    * RawAudioPipeSource(IEnumerable<IAudioSample>) - streams raw PCM audio
      samples.
    * StreamPipeSource(Stream) / StreamPipeSource(Func<Stream,...>) - pipes
      an arbitrary byte Stream in.

  OUTPUT sinks (IPipeSink):
    * StreamPipeSink(Stream) - captures ffmpeg output into a Stream.

  Frame/sample contracts:
    * IVideoFrame { int Width; int Height; string Format;   // ffmpeg pix_fmt
        void Serialize(Stream); Task SerializeAsync(Stream, CancellationToken); }
    * IAudioSample { void Serialize(Stream); Task SerializeAsync(Stream, CancellationToken); }

Use FromPipeInput(IPipeSource) / OutputToPipe(IPipeSink, Action<...>). Under
the hood the library creates an OS named pipe (or anonymous pipe), passes
its path to ffmpeg, and copies bytes on a background task while ffmpeg runs.


========================================================================
8. CORE API REFERENCE
========================================================================

--- FFProbe (static) - media analysis ---------------------------------
    IMediaAnalysis Analyse(string path, FFOptions? = null, string? customArgs = null)
    Task<IMediaAnalysis> AnalyseAsync(string path, FFOptions? = null,
                                      CancellationToken = default, ...)
    Analyse / AnalyseAsync(Uri uri, ...)
    Analyse / AnalyseAsync(Stream stream, ...)          // pipes the stream to ffprobe
    FFProbeFrames GetFrames(...) / Task<FFProbeFrames> GetFramesAsync(...)
    FFProbePackets GetPackets(...) / Task<FFProbePackets> GetPacketsAsync(...)

  IMediaAnalysis exposes:
    TimeSpan Duration; MediaFormat Format;
    VideoStream PrimaryVideoStream; AudioStream PrimaryAudioStream;
    SubtitleStream PrimarySubtitleStream;
    List<VideoStream> VideoStreams; List<AudioStream> AudioStreams;
    List<SubtitleStream> SubtitleStreams; List<ChapterData> Chapters;
    IReadOnlyList<string> ErrorData;
  Streams carry Index, CodecName, CodecLongName, Duration, BitRate, Width/
  Height (video), SampleRate/Channels (audio), Rotation, PixelFormat, Tags,
  Disposition, etc. FFProbe deserializes ffprobe's JSON (System.Text.Json)
  into these DTOs (see FFProbe/FFProbeAnalysis.cs for the wire model and
  MediaAnalysis.cs for the mapping to the friendly IMediaAnalysis).

--- FFMpeg (static) - one-shot conveniences ---------------------------
    bool Snapshot(input, output, Size? size=null, TimeSpan? captureTime=null,
                  int? streamIndex=null, int inputFileIndex=0)
    Task<bool> SnapshotAsync(...); bool GifSnapshot(...); Task<bool> GifSnapshotAsync(...)
    bool SubVideo(input, output, TimeSpan start, TimeSpan end) / SubVideoAsync
    bool Convert(input, output, ContainerFormat, Speed, VideoSize, AudioQuality, bool multithreaded)
    bool Join(output, params inputs) ; bool JoinImageSequence(output, double frameRate, params images)
    bool PosterWithAudio(image, audio, output)
    bool ExtractAudio(input, output) ; bool ReplaceAudio(...) ; bool Mute(input, output)
    // codec / format / pixel-format catalog (parsed from `ffmpeg -codecs` etc.):
    IReadOnlyList<Codec> GetCodecs()/GetVideoCodecs()/GetAudioCodecs()/GetSubtitleCodecs()/GetDataCodecs()
    Codec GetCodec(name) ; bool TryGetCodec(name, out Codec)
    IReadOnlyList<ContainerFormat> GetContainerFormats() ; GetContainerFormat / TryGetContainerFormat
    IReadOnlyList<PixelFormat> GetPixelFormats() ; GetPixelFormat / TryGetPixelFormat

--- FFMpegArguments - INPUT side --------------------------------------
  Static factories (start a chain):
    FromFileInput(path, bool verifyExists=true, Action<FFMpegArgumentOptions>? = null)
    FromPipeInput(IPipeSource, Action<...>?)  FromUrlInput(Uri, ...)
    FromDeviceInput(device, ...)  FromConcatInput(IEnumerable<string>, ...)
    FromDemuxConcatInput(IEnumerable<string>, ...)
  Instance (append):
    AddFileInput / AddPipeInput / AddUrlInput / AddDeviceInput /
    AddConcatInput / AddDemuxConcatInput
    AddMetaData(string|FFMetadataBuilder) ; MapMetaData(int? inputIndex=null, ...)
    WithGlobalOptions(Action<FFMpegGlobalArguments>)
  Terminals (produce an FFMpegArgumentProcessor):
    OutputToFile(path, bool overwrite=true, Action<FFMpegArgumentOptions>?)
    OutputToPipe(IPipeSink, Action<...>?)  OutputToUrl(uri|string, ...)
    OutputToTee(...)  MultiOutput(Action<FFMpegMultiOutputOptions>)

--- FFMpegArgumentOptions - OUTPUT side (fluent, ~40 methods) ----------
  Codecs/bitrate: WithVideoCodec, WithAudioCodec, WithCopyCodec, CopyChannel,
    WithVideoBitrate, WithAudioBitrate, WithConstantRateFactor, WithVariableBitrate,
    WithAudioSamplingRate, WithSpeedPreset, WithFramerate
  Sizing/geometry: Resize, Crop (via filters: WithVideoFilters(f => f.Scale/Crop/Pad/
    Transpose/Mirror/DrawText/HardBurnSubtitle/BlackDetect/BlackFrame))
  Audio filters: WithAudioFilters(f => f.Pan/HighPass/LowPass/AudioGate/
    DynamicNormalizer/SilenceDetect)
  Timing/selection: Seek, EndSeek, WithDuration, Loop, WithFrameOutputCount,
    WithStartNumber, SelectStream(s), DeselectStream(s), DisableChannel
  Container/format: ForceFormat, ForcePixelFormat, WithFastStart, WithTagVersion,
    WithHardwareAcceleration, WithBitStreamFilter
  Misc: OverwriteExisting, UsingShortest, UsingMultithreading, UsingThreads,
    WithoutMetadata, WithGifPaletteArgument, WithArgument (raw IArgument),
    WithCustomArgument (raw string), WithAudibleActivationBytes/EncryptionKeys

--- FFMpegArgumentProcessor - run & control ---------------------------
    string Arguments { get; }   // rendered command line (test without running)
    bool ProcessSynchronously(bool throwOnError = true, FFOptions? = null)
    Task<bool> ProcessAsynchronously(bool throwOnError = true, FFOptions? = null)
    FFMpegArgumentProcessor Configure(Action<FFOptions>)   // per-run options
    FFMpegArgumentProcessor CancellableThrough(out Action cancel, int timeout=0)
    FFMpegArgumentProcessor CancellableThrough(CancellationToken, int timeout=0)
    FFMpegArgumentProcessor NotifyOnProgress(Action<double>|Action<TimeSpan>, TimeSpan? totalTime=null)
    FFMpegArgumentProcessor NotifyOnOutput(Action<string>) / NotifyOnError(Action<string>)
    FFMpegArgumentProcessor WithLogLevel(FFMpegLogLevel)

--- FFOptions / GlobalFFOptions ---------------------------------------
    FFOptions: BinaryFolder, TemporaryFilesFolder, WorkingDirectory,
               EncodingWebName (string), UseCache (bool), LogLevel (FFMpegLogLevel?)
    GlobalFFOptions.Current            // process-wide FFOptions (defaults)
    GlobalFFOptions.Configure(FFOptions | Action<FFOptions>)
    GlobalFFOptions.GetFFMpegBinaryPath(FFOptions?) / GetFFProbeBinaryPath(FFOptions?)

--- Metadata ----------------------------------------------------------
    MetaDataBuilder (CodeBrix.VideoProcessing.Builders.MetaData): WithTitle,
      WithArtists, WithAlbumArtists, WithComposers, WithGenres, WithAlbum,
      WithCopyright, WithComments, WithDate, WithEncoder, WithEntry,
      WithMajorBrand/MinorVersion/CompatibleBrands, AddChapter(...). Build()
      returns a MetaData; MetaDataSerializer.Instance.Serialize(metaData)
      renders the ;FFMETADATA1 text.
    FFMetadataBuilder (newer, in the root namespace): FFMetadataBuilder.Empty()
      .WithTag(key, value).WithChapter(title, seconds); pass to
      FFMpegArguments.AddMetaData(builder).

--- Imaging bridge (CodeBrix.VideoProcessing.Imaging, needs CodeBrix.Imaging) ---
    static Image FFMpegImage.Snapshot(string input, Size? size=null,
        TimeSpan? captureTime=null, int? streamIndex=null, int inputFileIndex=0)
    static Task<Image> FFMpegImage.SnapshotAsync(..., CancellationToken=default)
        -> returns a CodeBrix.Imaging.Image, decoded from the PNG the snapshot
           pipeline produces (SnapshotArgumentBuilder hardcodes VideoCodec.Image.Png).
    static bool ImageExtensions.AddAudio(this Image poster, string audio, string output)
        -> writes the image to a temp PNG and calls FFMpeg.PosterWithAudio.
    class ImageVideoFrameWrapper(Image<Rgba32>) : IVideoFrame, IDisposable
        -> wraps an RGBA image as a pipe frame (Format "rgba"); feed into
           RawVideoPipeSource.

--- Vendored Instances (CodeBrix.VideoProcessing.Instances) ------------
  A small, elegant Process wrapper used internally to launch ffmpeg/ffprobe;
  also usable directly:
    static class Instance:
      IProcessInstance Start(path, args)
      IProcessResult Finish(path, args, [outputHandler], [errorHandler])
      Task<IProcessResult> FinishAsync(path, args, CancellationToken, [handlers])
    class ProcessArguments(path, args) : IgnoreEmptyLines, DataBufferCapacity,
      events Exited/OutputDataReceived/ErrorDataReceived; Start(),
      StartAndWaitForExit(), StartAndWaitForExitAsync()
    interface IProcessInstance : OutputData, ErrorData, WaitForExit(),
      WaitForExitAsync(token), Kill(), SendInput(text), SendInputAsync(text)
    interface IProcessResult : int ExitCode, OutputData, ErrorData
    Exceptions (CodeBrix.VideoProcessing.Instances.Exceptions):
      InstanceException, InstanceFileNotFoundException,
      InstanceProcessAlreadyExitedException

--- Enums (CodeBrix.VideoProcessing.Enums) ----------------------------
  VideoCodec, AudioCodec, Codec, ContainerFormat, VideoSize, PixelFormat,
  Speed, AudioQuality, FileExtension, FFMpegLogLevel, Mirroring,
  Transposition, HardwareAccelerationDevice. VideoCodec/AudioCodec/Codec
  are rich types (not plain enums) that also model ffmpeg's codec catalog;
  VideoCodec.Image.Png etc. are used by the snapshot builder.

--- Exceptions (CodeBrix.VideoProcessing.Exceptions) ------------------
  FFMpegException (+ FFMpegStreamFormatException for pipe frame mismatches),
  FFProbeException / FFProbeProcessException / FormatNullException. All wrap
  non-zero exit codes / process failures and carry the captured stderr.


========================================================================
9. CODING CONVENTIONS (CodeBrix family)
========================================================================
  * Target framework net10.0 only; no multi-targeting.
  * Nullable reference types DISABLED. Do NOT add '?' to reference types
    and do NOT use the null-forgiveness '!' operator. Value-type nullables
    (int?, Size?, TimeSpan?, enum? such as FFMpegLogLevel?) are fine.
  * Implicit usings DISABLED. All usings explicit; file-scoped namespaces
    only; System.* usings sorted first.
  * xUnit v3 + SilverAssertions for tests. No project-level warning
    suppression beyond the one documented exception below.
  * Ported files keep their `//was previously: <upstream-ns>;` comment.

  SITUATIONAL EXCEPTION (main library csproj only; OpenGL/AssemblyTools
  precedent, and NOT to be "fixed"):
    <GenerateDocumentationFile>true</> together with <NoWarn>1591</NoWarn>.
    The upstream FFMpegCore surface (~150 types) is only partially
    documented; the XML doc file is produced (so the package ships the
    upstream docs) but CS1591 is suppressed rather than inventing summaries
    for every public member. Nullable and ImplicitUsings stay OFF (they are
    NOT excepted). The imaging bridge files are fully documented, so no
    exception is needed for them.


========================================================================
10. REPOSITORY LAYOUT
========================================================================
  src/CodeBrix.VideoProcessing/          -- the one and only library / package
      FFMpeg/                            -- FFMpeg, FFMpegArguments, options,
        Arguments/  Builders/MetaData/   -- processor, per-argument types,
        Enums/  Exceptions/  Pipes/      -- metadata builder, enums, exceptions, pipes
      FFProbe/                           -- FFProbe + JSON model + friendly analysis
      Extend/  Helpers/                  -- string/uri/timespan extends, ff*helpers
      Imaging/                           -- FFMpegImage, ImageExtensions,
                                            ImageVideoFrameWrapper (CodeBrix.Imaging)
      Instances/  Instances/Exceptions/  -- vendored process wrapper
      InternalsVisibleTo.cs              -- grants internals to the .Tests project
  tests/CodeBrix.VideoProcessing.Tests/  -- xUnit v3 + SilverAssertions
  tests/CodeBrix.VideoProcessing.Tests.WaitingProgram/ -- tiny console helper the
                                            Instances process tests publish + drive
  CodeBrix.VideoProcessing.slnx          -- solution


========================================================================
11. TESTING
========================================================================
    dotnet test CodeBrix.VideoProcessing.slnx

  PREREQUISITES:
    * `ffmpeg` and `ffprobe` MUST be installed and on PATH (most tests are
      integration tests that shell out to them). e.g. `sudo apt install ffmpeg`.
    * The Instances tests spawn `dotnet` processes and, on first run, publish
      the WaitingProgram helper to ./waiting-program.
    * The Resources/ media files are copied to the test output directory.

  REQUIRED SETTING - the test assembly declares:
      [assembly: CollectionBehavior(DisableTestParallelization = true)]
    (tests/CodeBrix.VideoProcessing.Tests/AssemblyInfo.cs). This is
    MANDATORY: the integration tests spawn many ffmpeg/ffprobe/dotnet
    processes, and running the test CLASSES in parallel floods the machine
    and appears to hang. Keep parallelization off.

  Tests that DON'T need ffmpeg (fast, pure): ArgumentBuilderTest (renders
  and asserts command-line strings), PixelFormatTests, MetaDataBuilderTests,
  most of FFMpegOptionsTests. The rest are integration tests.

  Current status (ffmpeg 7.1.5): 204 passed, 9 skipped, 0 failed (~34s).


========================================================================
12. KNOWN ISSUES / GOTCHAS
========================================================================
  * ffmpeg/ffprobe must be present at runtime (section 1). Missing binaries
    throw FFMpegException/FFProbeException (or the Instances
    InstanceFileNotFoundException underneath).
  * Runtime cancellation of a running ffmpeg can hang via the SendInput("q")
    graceful-quit path on some ffmpeg builds (section 6). 7 Video_Cancel*
    tests are [Fact(Skip=...)] for this reason; it is environmental, not a
    port defect. Consider a non-zero cancellation `timeout`.
  * Test parallelization must stay disabled (section 11).
  * Pipe frames must be consistent: every IVideoFrame in a RawVideoPipeSource
    must share the first frame's Width/Height/Format, or you get
    FFMpegStreamFormatException.
  * The FFMpegCore.Extensions.* upstream packages (SkiaSharp,
    System.Drawing.Common, Downloader) were NOT ported. The SkiaSharp /
    System.Drawing image helpers are replaced by the cross-platform
    CodeBrix.Imaging bridge in CodeBrix.VideoProcessing.Imaging; the
    Downloader (auto-download ffmpeg binaries) has no equivalent - install
    ffmpeg yourself or set FFOptions.BinaryFolder.


========================================================================
13. PROVENANCE / LICENSING
========================================================================
  MIT. Incorporates MIT-licensed source from FFMpegCore 5.4.0 and Instances
  3.0.2 (both by Malte Rosenbjerg et al.); see THIRD-PARTY-NOTICES.txt for
  full attribution and license texts. The CodeBrix.Imaging package reference
  is Apache-2.0 (compatible). The imaging bridge design is adapted from
  FFMpegCore's SkiaSharp/System.Drawing image extensions.

========================================================================
END OF AGENT-README
========================================================================
