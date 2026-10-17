================================================================================
MAINTAINER-README: CodeBrix.VideoProcessing
Notes for people and agents MAINTAINING this repository - not for package
consumers
================================================================================

If you are consuming the NuGet package, read AGENT-README.txt instead. This file
covers building, testing, packaging and vendored-source maintenance for the
repository itself.


PURPOSE AND SCOPE
=================
This repository produces THREE NuGet packages.

    Package id ........ CodeBrix.VideoProcessing.MitLicenseForever
    Project ........... src/CodeBrix.VideoProcessing/CodeBrix.VideoProcessing.csproj
    Assembly / root ns  CodeBrix.VideoProcessing
    Consumer doc ...... AGENT-README.txt (repo root) - shipped inside the nupkg
    License ........... MIT

    Package id ........ CodeBrix.VideoProcessing.Core.MitLicenseForever
    Project ........... src/CodeBrix.VideoProcessing.Core/CodeBrix.VideoProcessing.Core.csproj
    Assembly / root ns  CodeBrix.VideoProcessing.Core
    Consumer doc ...... src/CodeBrix.VideoProcessing.Core/AGENT-README.txt -
                        shipped inside that nupkg as AGENT-README.txt
    License ........... MIT

    Package id ........ CodeBrix.VideoProcessing.Android.MitLicenseForever
    Project ........... src/CodeBrix.VideoProcessing.Android/CodeBrix.VideoProcessing.Android.csproj
    Assembly / root ns  CodeBrix.VideoProcessing.Android
    Consumer doc ...... src/CodeBrix.VideoProcessing.Android/AGENT-README.txt -
                        shipped inside that nupkg as AGENT-README.txt
    License ........... MIT
    Target ............ net10.0-android36.1, SupportedOSPlatformVersion 33

The root AGENT-README.txt is the CodeBrix.VideoProcessing package's file, NOT a
repository-wide one. README-INDEX.txt maps the three.

CodeBrix.VideoProcessing is a fully managed, cross-platform FFmpeg / FFprobe
wrapper. It launches the external `ffmpeg` and `ffprobe` executables and parses
their output; it contains no native code and no P/Invoke. It is ALSO the
desktop (Windows / Linux / macOS) ENGINE for the Core contract - see THE CORE
CONTRACT AND THE FFMPEG ENGINE below.

CodeBrix.VideoProcessing.Core is the engine-neutral contract (VideoProcessors
registry, IVideoProcessor, media description, frame / sample sources, the
standard MP4 writer, transcode / trim / concatenate requests), plus the
ENCODER SEAM (Encoding/: the VideoEncoders registry, IVideoEncoderFactory,
IVideoEncoder, VideoEncoderRequest, EncodedVideoPacket). It has ZERO
package dependencies - keep it that way: no CodeBrix.Imaging, nothing. It must
not know anything about the CodeBrixVideo formats (CBVF, WebM authoring); those
belong to the libraries that CONSUME the contract.

CodeBrix.VideoProcessing.Android is the Android ENGINE for the Core contract,
on MediaExtractor / MediaCodec / MediaMuxer / MediaMetadataRetriever - see THE
ANDROID ENGINE below. It has no reference to the ffmpeg package and no natives.

Project reference graph inside the repository:

    CodeBrix.VideoProcessing          --ProjectReference-->  CodeBrix.VideoProcessing.Core
    CodeBrix.VideoProcessing.Android  --ProjectReference-->  CodeBrix.VideoProcessing.Core

Each ProjectReference packs as a package DEPENDENCY (not a bundled assembly).
All three projects carry the SAME date-stamped version block, so one build/pack
run stamps them all with one version and each dependency is declared at that
same version.


REPOSITORY LAYOUT
=================
    src/CodeBrix.VideoProcessing.Core/     -- the contract package
        (root)                             -- VideoProcessors, IVideoProcessor,
                                              VideoProcessorCapabilities,
                                              VideoProcessorExtensions,
                                              VideoProgress and the three
                                              exceptions
        Media/                             -- MediaInfo, Video/AudioStreamInfo,
                                              SubtitleStreamInfo, ChapterInfo,
                                              FrameRate, colour / chroma enums
        Tools/                             -- the external-tools facet:
                                              IExternalToolHost, IExternalTools,
                                              ExternalTool, ExternalToolArguments
                                              (the pinned quoting),
                                              ExternalToolCommand,
                                              ExternalToolResult, and the ONE
                                              ExternalToolRunner +
                                              ExternalToolLocator
        Frames/                            -- VideoFrameData, VideoPlane,
                                              VideoPixelLayout, frame and sample
                                              sources, their options and infos
        Output/                            -- StandardVideoRequest,
                                              IStandardVideoWriter, VideoQuality,
                                              profiles, Transcode / Trim /
                                              Concatenate requests
        Encoding/                          -- the encoder seam: VideoEncoders
                                              (registry), VideoCodecIds,
                                              IVideoEncoderFactory,
                                              IVideoEncoder,
                                              VideoEncoderRequest,
                                              VideoEncoderCapabilities (+ the
                                              layout / depth flag enums),
                                              EncodedVideoPacket and the two
                                              encoder exceptions
        AGENT-README.txt                   -- that package's consumer doc
        InternalsVisibleTo.cs              -- grants internals to
                                              CodeBrix.VideoProcessing.Core.Tests
    tests/CodeBrix.VideoProcessing.Core.Tests/
                                           -- xUnit v3 + SilverAssertions; pure
                                              unit tests over a fake in-memory
                                              engine (FakeVideoProcessor.cs) and
                                              a fake encoder
                                              (FakeVideoEncoderFactory.cs,
                                              FakeVideoEncoder.cs) - no ffmpeg
                                              needed
    src/CodeBrix.VideoProcessing/          -- the FFmpeg wrapper library / package
        FFMpeg/                            -- FFMpeg, FFMpegArguments,
                                              FFMpegArgumentOptions,
                                              FFMpegArgumentProcessor,
                                              FFMpegGlobalArguments,
                                              FFMpegMultiOutputOptions,
                                              FFMpegCache, SnapshotArgumentBuilder
          Arguments/                       -- one type per ffmpeg argument, plus
                                              the filter option builders
          Builders/MetaData/               -- MetaDataBuilder, MetaData,
                                              ReadOnlyMetaData, ChapterData,
                                              MetaDataSerializer
          Enums/                           -- enums plus the Codec /
                                              ContainerFormat / PixelFormat
                                              catalogue types
          Exceptions/                      -- FFMpeg* exception types
          Pipes/                           -- IPipeSource / IPipeSink /
                                              IVideoFrame / IAudioSample and the
                                              shipped implementations
        FFProbe/                           -- FFProbe, the ffprobe JSON model
                                              (FFProbeAnalysis, FFProbeStream,
                                              Format, Chapter), the friendly
                                              MediaAnalysis mapping, frame and
                                              packet analysis, FFProbe exceptions
        Extend/                            -- string / uri / timespan /
                                              key-value helpers,
                                              PcmAudioSampleWrapper
        Helpers/                           -- FFMpegHelper, FFProbeHelper
        Imaging/                           -- FFMpegImage, ImageExtensions,
                                              ImageVideoFrameWrapper
                                              (the CodeBrix.Imaging bridge)
        Engine/                            -- the Core-contract engine (original
                                              CodeBrix code): FFmpegVideoProcessor
                                              (public) and its internals -
                                              FFmpegProbe, FFmpegNames,
                                              FFmpegToolProcess,
                                              FFmpegExternalTools,
                                              FFmpegVideoFrameSource,
                                              FFmpegAudioSampleSource,
                                              FFmpegStandardVideoWriter,
                                              QueuedPipeSource,
                                              FFmpegOutputOptions
        CodeBrixVideoProcessing.cs         -- the engine's Register() class
        Instances/                         -- the vendored process wrapper
        Instances/Exceptions/
        FFOptions.cs, GlobalFFOptions.cs, FFMetadataInputArgument.cs
        InternalsVisibleTo.cs              -- grants internals to
                                              CodeBrix.VideoProcessing.Tests
    src/CodeBrix.VideoProcessing.Android/  -- the Android engine package
        (root)                             -- CodeBrixVideoProcessingAndroid
                                              (Register), InternalsVisibleTo.cs
                                              (-> CodeBrix.VideoProcessing.
                                              Android.Tests), AGENT-README.txt
        Engine/                            -- the Android-only code:
                                              AndroidVideoProcessor (public),
                                              AndroidMediaAnalyser,
                                              AndroidVideoFrameSource,
                                              AndroidAudioSampleSource,
                                              AndroidStandardVideoWriter,
                                              AndroidFileOperations,
                                              AndroidImageCopy, AndroidMedia
        Managed/                           -- PLATFORM-INDEPENDENT internals (no
                                              Android type anywhere): YUV / RGB
                                              conversion and scaling, PCM,
                                              channel mixer, resampler, the
                                              quality -> bitrate table, keyframe
                                              seconds, the constant-rate
                                              scheduler, the MP4 box tree,
                                              rewriter (fast start + tags) and
                                              timing reader, MediaFormat
                                              mapping, the concatenation rule,
                                              the request gate, capabilities and
                                              the registration logic. The host
                                              tests compile these files directly.
    tests/CodeBrix.VideoProcessing.Android.Tests/
                                           -- HOST tests (net10.0) of the
                                              Managed/ files, linked in as
                                              sources; the MP4 tests make
                                              fixtures with the host ffmpeg
    tests/Assets/                          -- clip-h264-aac.mp4 and the 10-bit
                                              ramp clip-hevc10-aac.mp4
                                              (synthetic, see PROVENANCE.txt
                                              there), used by the Android host
                                              tests and bundled into the
                                              device-proof app
    samples/VideoProcessingDiagnostics/    -- the Android device-proof app (see
                                              THE ANDROID ENGINE)
    tests/CodeBrix.VideoProcessing.Tests/  -- xUnit v3 + SilverAssertions
        Resources/                         -- sample media copied to the test
                                              output directory
        Engine/                            -- the engine tests, plus
                                              EngineTestMedia (synthetic fixtures
                                              made with the host ffmpeg, and
                                              ffprobe / MP4-box inspection)
        Utilities/                         -- BitmapSource (synthetic frames on
                                              CodeBrix.Imaging),
                                              OsSpecificFactAttribute,
                                              FFMpegEncoderList (reads
                                              `ffmpeg -encoders` for
                                              conditional skips)
        ffmpeg.config.json                 -- exercises FFOptions file loading
        AssemblyInfo.cs                    -- disables test parallelization
    tests/CodeBrix.VideoProcessing.Tests.WaitingProgram/
                                           -- a tiny console helper the
                                              Instances process tests publish
                                              and drive
    CodeBrix.VideoProcessing.slnx          -- solution. Its Solution Items
                                              folder carries .gitignore,
                                              AGENT-README.txt,
                                              EXTRAS-README.txt, global.json,
                                              icon-codebrix-128.png, LICENSE,
                                              MAINTAINER-README.txt,
                                              README-INDEX.txt, README.md and
                                              THIRD-PARTY-NOTICES.txt; the
                                              Tests folder carries all four
                                              test projects and the Samples
                                              folder the device-proof app
    global.json                            -- selects the
                                              Microsoft.Testing.Platform test
                                              runner. It does NOT pin an SDK
                                              version. See TESTING below
    AGENT-README.txt, MAINTAINER-README.txt, EXTRAS-README.txt,
    README-INDEX.txt, README.md, LICENSE, THIRD-PARTY-NOTICES.txt,
    icon-codebrix-128.png


BUILDING
========
    dotnet restore CodeBrix.VideoProcessing.slnx
    dotnet build   CodeBrix.VideoProcessing.slnx

Target framework: net10.0 for CodeBrix.VideoProcessing, the Core and every
test project; net10.0-android36.1 (SupportedOSPlatformVersion 33) for
CodeBrix.VideoProcessing.Android and the sample. No multi-targeting.
CodeBrix.VideoProcessing has one package reference
(CodeBrix.Imaging.ApacheLicenseForever) and one project reference (the Core);
the Android package has only the Core project reference; the Core has none at
all; everything else comes from the shared framework / the Android workload.

Building the slnx therefore needs the .NET "android" workload, an Android SDK
with the API 36 platform and a JDK the workload accepts. The Android csprojs
default AndroidSdkDirectory to $HOME/Android/Sdk and JavaSdkDirectory to the
Debian JDK 21 path when those exist (override with -p:... or ANDROID_HOME /
JAVA_HOME).

All three library csprojs set GeneratePackageOnBuild=true, so an ordinary
Debug build also produces all three .nupkg files (see PACKAGING below).


TESTING
=======
    dotnet test CodeBrix.VideoProcessing.slnx

THE TEST RUNNER IS Microsoft.Testing.Platform (MTP), selected by global.json at
the repo root:

    { "test": { "runner": "Microsoft.Testing.Platform" } }

Because the setting lives in global.json rather than in the test csproj, it
applies to every `dotnet test` run anywhere in the repository. The file pins no
SDK version, so the newest installed .NET 10 SDK is still used. Keep it
committed - without it `dotnet test` falls back to the older VSTest bridge.

Test dependencies: xunit.v3, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk
and SilverAssertions.ApacheLicenseForever. coverlet.collector is deliberately
NOT referenced any more; there is no code-coverage collection in this
repository, so do not put it back expecting one.

PREREQUISITES
    * `ffmpeg` and `ffprobe` MUST be installed and on PATH - most of the suite
      is integration tests that shell out to them. On Debian-based Linux:
      `sudo apt install ffmpeg`.
    * The Instances tests spawn `dotnet` processes and, on first run, publish
      the WaitingProgram helper to ./waiting-program in the test output
      directory (they run `dotnet publish ... -c Release -o ./waiting-program`
      themselves; nothing to do by hand).
    * Resources/ media files are copied to the test output directory by the
      test csproj.

REQUIRED SETTING - the test assembly declares, in
tests/CodeBrix.VideoProcessing.Tests/AssemblyInfo.cs:

    using Xunit.Sdk;   // ParallelMode
    using Xunit.v3;    // ParallelizationAttribute

    [assembly: Parallelization(Mode = ParallelMode.None)]

This is MANDATORY. The integration tests spawn many ffmpeg / ffprobe / dotnet
processes; running the test CLASSES in parallel floods the machine and looks
like a hang. Keep parallelization off.

ANDROID HOST TESTS
    tests/CodeBrix.VideoProcessing.Android.Tests runs on the development
    machine (net10.0). The library targets net10.0-android36.1, which a
    desktop test project cannot reference, so - as in CodeBrix.Audio.Android -
    the project compiles src/CodeBrix.VideoProcessing.Android/Managed/*.cs as
    LINKED sources and references the Core project. That is why Managed/ must
    never use an Android type. Everything that calls MediaCodec / MediaExtractor
    / MediaMuxer is proven on devices by samples/VideoProcessingDiagnostics
    instead. The MP4 rewriter / timing tests that need real files make them
    with the host ffmpeg directly and skip themselves (Assert.SkipUnless) when
    ffmpeg is not on PATH; the rest need nothing.

FAST TESTS THAT DO NOT NEED FFMPEG
    The WHOLE of CodeBrix.VideoProcessing.Core.Tests (it tests the contract
    through FakeVideoProcessor, an in-memory engine). Every test that touches
    the process-wide VideoProcessors registry lives in VideoProcessorsTests,
    which clears the registry before and after each test through the internal
    Clear(); keep registry tests in that one class so no other class can see a
    half-built registry.
    In CodeBrix.VideoProcessing.Tests: ArgumentBuilderTest (renders and asserts command-line strings),
    PixelFormatTests, MetaDataBuilderTests, most of FFMpegOptionsTests, and the
    command-rewrite and message tests in Av1EncoderFallbackTests.
    Everything else is an integration test.

SKIPPED TESTS
    One FFProbeTests case (Uri_Duration) is skipped as consistently failing on
    hosted ubuntu agents. Nothing else in the suite is skipped unconditionally.

    CONDITIONAL SKIPS - Av1EncoderFallbackTests. The tests that run ffmpeg
    against a libsvtav1 request can only mean something where that ffmpeg has
    NO libsvtav1, so they call Assert.SkipWhen(FFMpegEncoderList.Has("libsvtav1"))
    and skip themselves wherever it is present. The fallback ones also call
    Assert.SkipUnless(FFMpegEncoderList.Has("libaom-av1")). Expect them to RUN on
    a build like gyan.dev's Windows "essentials", and to SKIP on the usual
    Linux distribution and Homebrew builds, which include SVT-AV1. A skip there
    is the test working as designed, not a problem. FFMpegEncoderList reads
    `ffmpeg -encoders` itself, NOT through the library code under test.

    HISTORY, because the previous note here was wrong and may still be believed:
    seven Video_Cancel* tests in VideoTest.cs used to carry [Fact(Skip = ...)]
    blaming "ffmpeg runtime-cancellation hangs on this host / some ffmpeg
    builds". That diagnosis did not hold up. Run one at a time, six of the seven
    pass; the suite-level hang came from ONE of them,
    Video_Cancel_CancellationToken_Throws, and its cause was in the test, not in
    ffmpeg and not in the port:

        CancellableThrough(CancellationToken) keeps exactly ONE registration and
        disposes the previous one. The test registered cts.Token, then called
        CancellableThrough(TestContext.Current.CancellationToken) again before
        ProcessSynchronously, which silently unhooked cts.Token. Nothing was
        left to cancel ffmpeg, and since the lavfi input is infinite the test
        hung forever instead of failing.

    Removing that second registration fixes it, and all seven now run. Note the
    sharp edge for anything else that touches these tests: passing a second
    CancellationToken to the same processor REPLACES the first rather than
    adding to it. Video_Cancel and Video_Cancel_Async call CancellableThrough
    more than once and are fine only because they cancel through the
    out-parameter Action, which that dispose does not affect.

OS-SPECIFIC TESTS
    Utilities/OsSpecificFactAttribute.cs provides [OsSpecificFact(OsPlatforms.X)]
    - an xUnit [Fact] that skips unless the current OS matches. It replaces the
    upstream MSTest OsSpecificTestMethod attribute.


PACKAGING AND PUBLISHING
========================
There is no separate pack driver. All three library csprojs set
GeneratePackageOnBuild=true, so `dotnet build` (or `dotnet pack`) of the slnx
produces CodeBrix.VideoProcessing.MitLicenseForever.<v>.nupkg,
CodeBrix.VideoProcessing.Core.MitLicenseForever.<v>.nupkg and
CodeBrix.VideoProcessing.Android.MitLicenseForever.<v>.nupkg. The two engine
nuspecs each declare ONE dependency, on
CodeBrix.VideoProcessing.Core.MitLicenseForever at the Core package's version
from the same run. PUBLISH THEM TOGETHER - an engine package whose Core
dependency was never pushed cannot be restored.

WHAT SHIPS IN THE CodeBrix.VideoProcessing NUPKG:
    icon-codebrix-128.png     (PackageIcon)
    README.md                 (PackageReadmeFile)
    AGENT-README.txt          (the root consumer documentation)
    THIRD-PARTY-NOTICES.txt   (upstream attribution and license texts)
    the generated XML documentation file

WHAT SHIPS IN THE CodeBrix.VideoProcessing.Core NUPKG:
    icon-codebrix-128.png, README.md (the shared root README), LICENSE,
    THIRD-PARTY-NOTICES.txt, the generated XML documentation file, and
    src/CodeBrix.VideoProcessing.Core/AGENT-README.txt packed at the package
    root as AGENT-README.txt.

WHAT SHIPS IN THE CodeBrix.VideoProcessing.Android NUPKG:
    lib/net10.0-android36.1/CodeBrix.VideoProcessing.Android.dll + its XML
    documentation file, and four content files: icon-codebrix-128.png,
    README.md (the shared root README), THIRD-PARTY-NOTICES.txt, and
    src/CodeBrix.VideoProcessing.Android/AGENT-README.txt packed at the package
    root as AGENT-README.txt. No native libraries; one dependency (the Core, at
    the same version).

MAINTAINER-README.txt, EXTRAS-README.txt and README-INDEX.txt are repository
files and are deliberately NOT packed.

VERSIONING - date-stamped and auto-incrementing, computed in the csproj from
System.DateTime.UtcNow:

    1.<x>.<y>.<z>
      1  major     always 1 for this library
      x  minor     whole years since _VersionBaseYear (2026 -> 0)
      y  build     day of year, 1-based, UTC
      z  revision  minute of day, UTC, 0..1439

The value is strictly increasing over time. Consequences to remember:
    * every build produces a NEW version, and with GeneratePackageOnBuild=true
      a fresh .nupkg;
    * two builds inside the SAME UTC minute produce the SAME version - do not
      publish two packages from within one minute;
    * this is date-stamp versioning, not SemVer: major/minor do not signal API
      compatibility. Re-baseline by changing _VersionBaseYear.

PackageRequireLicenseAcceptance is true and PackageLicenseExpression is MIT.
Never write version numbers into AGENT-README.txt - they go stale.


PROVENANCE AND VENDORED SOURCES
===============================
    * The library is a faithful PORT of FFMpegCore 5.4.0
      (github.com/rosenbjerg/FFMpegCore, MIT, by Malte Rosenbjerg, Vlad Jerca,
      Max Bagryantsev), re-namespaced FFMpegCore.* ->
      CodeBrix.VideoProcessing.* and retargeted to net10.0. Only the main
      "FFMpegCore" library project was incorporated.
    * The Instances 3.0.2 process-wrapper library
      (github.com/rosenbjerg/Instances, MIT) is VENDORED directly in under
      CodeBrix.VideoProcessing.Instances, so there is no dependency on a
      separate "Instances" package.
    * Every ported .cs file carries a `//was previously: <upstream-ns>;`
      provenance comment on its namespace line. KEEP those comments when you
      edit a ported file, and add one when you port another file.
    * The FFMpegCore.Extensions.* projects (SkiaSharp, System.Drawing.Common,
      Downloader) were NOT ported. Their image helpers are replaced by the
      cross-platform CodeBrix.Imaging bridge in
      src/CodeBrix.VideoProcessing/Imaging/; the auto-downloader has no
      equivalent here.
    * THIRD-PARTY-NOTICES.txt carries the full attribution and license texts for
      both upstream projects and must be kept in sync with any further porting.
    * When fixing a bug that also exists upstream, prefer a change that stays
      recognisably close to the upstream code - the port's value is that it can
      be diffed against FFMpegCore.
    * There are currently THREE deliberate behavioural divergences from
      upstream, all in FFMpegArgumentProcessor. One is how it chooses between a
      broken pipe and ffmpeg's own failure - see PIPE FAILURES VERSUS FFMPEG
      FAILURES. One is a CodeBrix bug fix: output pipes are read to EOF before
      they are disposed - see OUTPUT PIPES DRAIN BEFORE THEY ARE DISPOSED. The
      third is an ADDITION: the missing-libsvtav1 explanation and the opt-in
      libaom-av1 fallback - see AV1 ENCODER FALLBACK. Read all three sections
      below before touching that code.


CODING CONVENTIONS
==================
    * Target framework net10.0 only, no multi-targeting - except the Android
      engine and its sample, which are net10.0-android36.1 (API 33 minimum).
    * Nullable reference types DISABLED. Do NOT add '?' to reference types and
      do NOT use the null-forgiveness '!' operator. Value-type nullables (int?,
      Size?, TimeSpan?, FFMpegLogLevel?) are fine.
    * Implicit usings DISABLED. All usings explicit; file-scoped namespaces
      only; System.* usings sorted first.
    * Ported files keep their `//was previously: <upstream-ns>;` comment.
    * Tests: xUnit v3 + SilverAssertions. Test classes are named
      <Area>Test/<Area>Tests; assertions use the
      `(actual).Should().Be(expected);` form.
    * No project-level warning suppression beyond the one documented exception
      below.

THE CORE PROJECT HAS NO EXCEPTIONS AT ALL: GenerateDocumentationFile is on, there
is NO NoWarn, and every public member carries an XML doc comment. Keep it that
way when the contract grows.

SITUATIONAL EXCEPTION (main library csproj only; the CodeBrix.Platform.OpenGL
and CodeBrix.AssemblyTools precedent, and NOT to be "fixed"):

    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>1591</NoWarn>

The ported upstream surface (nearly 200 public types and hundreds of members)
is only partially documented upstream. The XML doc file is produced so the
package ships the upstream docs, but CS1591 is suppressed rather than inventing
summaries for every public member. Nullable and ImplicitUsings stay OFF - they
are NOT excepted. The imaging bridge files under Imaging/ are fully documented,
so they need no exception.


THE CORE CONTRACT AND THE FFMPEG ENGINE
=======================================
CodeBrix.VideoProcessing implements CodeBrix.VideoProcessing.Core's
IVideoProcessor as Engine/FFmpegVideoProcessor, registered by
CodeBrixVideoProcessing.Register() (idempotent, locked; throws
PlatformNotSupportedException on Android, pointing at the Android engine). The
engine is ORIGINAL CodeBrix code layered on the port - it has no upstream and
no `//was previously:` comments. It is written to the Core project's standard:
every public member documented even though this csproj carries the NoWarn 1591
exception.

DESIGN DECISIONS - read before changing the engine:
    * READERS USE STDOUT, NOT OutputToPipe. The frame source, sample source,
      and Snapshot run ffmpeg through FFmpegToolProcess
      (System.Diagnostics.Process, ArgumentList, binary stdout). The ported
      OutputPipeArgument path is NOT safe for a pull reader: when ffmpeg exits,
      FFMpegArgumentProcessor's exit continuation calls Post(), which disposes
      the server pipe while the reader may still have unread data in it. A
      deliberately slow StreamPipeSink reading a raw decode of
      input_3sec.webm received 31,053,312 of 31,104,000 bytes and no error.
      Reading stdout has no such race. (The ported race is left as it is -
      see the found-along-the-way list in the session report.)
    * ONE PROBE PATH. The engine's Analyse (FFmpegProbe) calls
      FFProbe.AnalyseAsync and maps the friendly IMediaAnalysis model onto
      MediaInfo; it never parses ffprobe JSON itself. The exact values the
      contract needs live on the friendly model: VideoStream.RealFrameRate and
      ExactFrameRate (Core FrameRate rationals - the engine prefers the real
      rate unless it is above 1000 fps), FrameCount (nb_frames),
      DisplayMatrixRotation, and tick-precise durations. If the engine ever
      needs another ffprobe field, add it to FFProbeStream and the friendly
      model - do not start a second parser. FFProbe exceptions are translated
      to Core ones (FormatNullException / non-zero exit / bad JSON ->
      MediaFormatException; ffprobe not runnable -> VideoProcessingException).
    * THE WRITER USES THE PORT'S PIPE MACHINERY: one ffmpeg, two
      InputPipeArguments (via FFMpegArguments.FromPipeInput / AddPipeInput),
      each fed by a QueuedPipeSource (an IPipeSource over an unbounded
      Channel of pooled buffers). Input pipes do not have the reader race:
      finishing the queue lets PipeArgument.During disconnect, which is EOF to
      ffmpeg.
    * WRITER BACK-PRESSURE (FFmpegStandardVideoWriter.WaitForRoom): a Write
      call waits while its own stream has a backlog (8 frames / about one
      second of audio chunks), BUT stops waiting once the other stream's queue
      is empty and its own has not moved for 100 ms - ffmpeg is then waiting for
      the other stream (its scheduler, and x265's look-ahead, can want audio
      well ahead of video). A hard 10 s without progress also ends the wait.
      The first version waited whenever the other stream had "caught up" in
      media time; with HEVC + audio that stalled every frame for its 5 s
      timeout (one test run took 13 minutes). Do not reintroduce a rule that
      can hold one stream while ffmpeg starves on the other.
    * FRAME-ACCURATE SEEK: the frame source restarts ffmpeg with input -ss a
      QUARTER FRAME before the target frame's grid time, plus an fps filter at
      the stream's rate. The target frame is then the first to survive
      ffmpeg's accurate seek; Seek_lands_on_the_same_picture_as_reading_
      sequentially pins this byte-for-byte.
    * ROTATION: frames are decoded with -noautorotate; the writer and
      TranscodeFile write a clockwise request as -display_rotation:v:0 with
      the NEGATED angle (ffmpeg's display matrix angle is counter-clockwise).
      ffmpeg 7.x ignores the legacy `-metadata:s:v rotate=`; do not use it.
    * Alpha WebM: ffmpeg's native VP8/VP9 decoders drop the alpha side
      channel; the frame source selects libvpx / libvpx-vp9 when alpha is
      requested from a stream tagged alpha_mode=1.
    * SOURCE PIXEL FORMATS (FFmpegVideoFrameSource.ChoosePixelFormat): the
      raw output format is picked so its planes ARE the contract's planes -
      yuv420p / yuv444p and their 10le / 12le and yuva* variants (ffmpeg's
      "le" formats already store deep samples little-endian in the low bits).
      GRAY IS DECODED AS yuv420p* AND ONLY Y IS KEPT: ffmpeg's own gray /
      gray10le / gray12le formats are full range, so a limited-range source
      came out stretched (verified: luma differed from I420's in 892,398 of
      921,600 samples). Gray with alpha goes through yuva420p* (yuva444p12le at
      12 bits). 12-bit I420 WITH alpha is refused (NotSupportedException):
      ffmpeg has no yuva420p12le, and down-sampling 4:4:4 chroma ourselves
      would be a guess. The standard writer takes 8-bit I420 / RGBA only and
      throws ArgumentException for anything else (I444 / Gray already fail
      StandardVideoRequest.Validate).
    * DELIVERED COLOUR MATRIX (FFmpegVideoFrameSource): VideoSourceInfo.ColorMatrix
      is the matrix of the samples in the pipe, as the contract says, not the
      file's tag. The first version copied the stream's tag, so an RGB source
      (PNG / RGBA in .mov, ffprobe color_space=gbr) came out as 4:2:0 YUV
      tagged Rgb - a tag the AV1 encoder package refuses for I420, which made
      in-process authoring refuse those sources. Now an RGB source delivered
      as YUV is converted with an EXPLICIT scale=out_color_matrix=bt709 (swscale's
      silent default for RGB->YUV is BT.601) and reports Bt709; RGBA delivery
      reports Rgb whatever the source; YUV sources keep their tag. The test
      proves the matrix by the luma of pure red (0.2126 of white under BT.709,
      0.299 under BT.601).

    * THE EXTERNAL-TOOLS FACET (Engine/FFmpegExternalTools, Core's Tools/
      namespace): the engine reports VideoProcessorCapabilities.ExternalTools
      and implements IExternalToolHost, so a consumer that composes its own
      ffmpeg / ffprobe lines (the desktop authoring library) runs them through
      the registered engine without referencing this package. BinaryFolder is
      the engine's FFOptions.BinaryFolder (its own options, else
      GlobalFFOptions.Current), read LIVE at each call. Locate MUST agree with
      GlobalFFOptions.GetFFMpegBinaryPath / GetFFProbeBinaryPath: <folder>/x64 (x86), then <folder>, then
      the PATH - a test pins the two side by side; change both or neither.
      Run = Core's ExternalToolRunner (the one runner; the engine has no
      process code for it). The PORT'S OWN OPERATIONS (FFMpegArgumentProcessor
      on the vendored Instances runner, FFProbe) and the engine's readers
      (FFmpegToolProcess) and writer are NOT moved onto the Core runner -
      faithful port; converging them is a separate, later decision.
      The QUOTING CONTRACT of ExternalToolArguments.Render reproduces the
      port's rendering (InputArgument / OutputArgument wrap paths in double
      quotes, nothing escaped; argument texts joined with single spaces), so
      lines a consumer records stay byte-identical when it moves from the port
      to the facet. Core.Tests/Tools/ExternalToolArgumentsTests pins it.
    * SUBTITLES AND CHAPTERS IN Analyse: FFmpegProbe maps the friendly model's
      SubtitleStreams and Chapters onto MediaInfo.SubtitleStreams / Chapters.
      The port's ChapterData keeps only title / start / end and puts the
      placeholder "TitleValueNotSet" in an untitled chapter - the engine maps
      that to a null Title; chapter Tags carry the title only.

ENGINE TESTS (tests/CodeBrix.VideoProcessing.Tests/Engine/) need ffmpeg with
libx264, libx265, aac and ffv1 (the alpha fixture is a lossless FFV1 clip
generated at test time). They take well under a minute.


THE ANDROID ENGINE
==================
src/CodeBrix.VideoProcessing.Android implements the Core contract IN FULL on
Android's MediaExtractor, MediaCodec, MediaMuxer and MediaMetadataRetriever:
AndroidVideoProcessor (Name "android-mediacodec"), registered by
CodeBrixVideoProcessingAndroid.Register() (idempotent, locked, one shared
instance, created on first use because creating it reads the codec list). The
consumer-facing behaviour (capability table with reasons, quality -> bitrate
table, fast start, threading, cancellation) is in its AGENT-README; this is the
maintainer's view.

DESIGN DECISIONS - read before changing the engine:
    * TWO HALVES. Engine/ holds everything that touches an Android type;
      Managed/ holds everything that does not, and the host test project
      compiles Managed/ as linked sources. Put new logic in Managed/ whenever
      it can be phrased without an Android type (that is how the MediaFormat
      mapping, the capability set, the request gate and the registration are
      host-tested), and never let an Android type leak into Managed/.
    * SYNCHRONOUS MediaCodec ON THE CALLING THREAD. Sources, the writer and the
      whole-file operations queue input and drain output in one loop on the
      caller's thread; no reader threads, no async callbacks. Every loop has a
      15-second no-progress watchdog (AndroidMedia.StallTimeout) so a codec
      process that dies (see THE HP LAPTOP below) fails with
      VideoProcessingException instead of hanging.
    * CONSTANT RATE = ConstantRateScheduler. Decoded pictures go to the
      NEAREST grid slot of the stream's rate (slot 0 = the first sample time),
      gaps repeat the previous picture, a crowded slot keeps the latest, no
      slot after the last picture. It mirrors the desktop engine's fps filter
      closely enough that frame indexes and counts agree on constant-rate
      files (the device proof checks 120 / 240 / 39 / 60 frame counts).
    * THE FRAME RATE COMES FROM PRESENTATION TIMES. MediaExtractor's
      KEY_FRAME_RATE is a rounded number, so for MP4 / MOV the engine reads the
      sample tables (Mp4TrackTiming): presentation times = stts decode times +
      ctts offsets, sorted; the rate is timescale / the dominant step. Do NOT
      go back to stts deltas alone: MediaMuxer writes B-frame streams with
      decode deltas like 9000, 9, 8982, 9 ... (90 kHz) - the dominant decode
      delta is 9 and the average gives 30.125 fps, which put a duplicated frame
      at every join of a concatenation (found on the Pixel).
    * SNAPSHOT IS THE FRAME SOURCE, NOT MediaMetadataRetriever. The contract
      wants the first frame at or after `at`, I420, stored orientation; the
      retriever gives the "closest" frame as a display-rotated ARGB bitmap.
    * B-FRAMES IN SAMPLE COPIES. Copied video samples are written with their
      presentation times exactly as read (decode order is not presentation
      order; MediaMuxer derives the ctts). A trim's end HOLDS video samples at
      or after To and writes them only if a later sample shown before To
      arrives (it references them) - stopping at the first sample >= To lost
      B-frames before To (found on the Pixel). Audio samples of the next
      concatenated input that would overlap the previous input's sound are
      dropped; each input starts at the previous one's last picture + one
      frame period (track durations include B-frame delay / AAC padding).
    * THE MP4 POST-PASS (Mp4Rewriter) does fast start AND the title /
      creation-time tags, which MediaMuxer has no API for. MediaMuxer on API 33
      usually reserves space at the front and writes moov there itself
      ("ftyp moov free mdat"); when moov does not fit it goes to the end and
      the post-pass moves it (concatenation outputs show "ftyp moov mdat" on
      both proof devices). Offsets are patched per top-level box moved;
      stco -> co64 upgrade when needed; the new file is written beside the old
      and moved over it.
    * QUALITY IS A BITRATE: QualityBitrateTable is deterministic (integer
      ten-thousandths of a bit per pixel, exact rational fps); keep the table
      in the AGENT-README in step if it changes. The result is clamped to the
      encoder's BitrateRange; VBR when supported.
    * NOT SET: AlphaFrameSource, I444FrameSource (see the AGENT-README for
      why).
    * 10-BIT FRAMES (HighBitDepthFrameSource, DEVICE-DEPENDENT). The probe is
      AndroidEngineCapabilities.Compute / FindP010Decoders over CodecTypeInfo
      entries (one per codec and MIME type, with its colorFormats) - pure
      Managed/ code, host-tested with fake codec lists; AndroidMedia.
      ListCodecs fills the entries from MediaCodecList. The decode decision is
      HighBitDepthConversion.ChooseMode (8 -> EightBit; 10 + known 8-bit
      source -> WidenEightBit, value << 2; 10 + 10-bit or unknown-depth source
      -> P010 REQUIRED; 12 -> NotSupportedException, also in the request
      gate). P010 words are value << 6; HighBitDepthConversion.ReadPlane /
      ReadP010Buffer shift them down to the contract's low bits and are
      host-tested on planar / semi-planar / padded / odd-width / cropped
      layouts and bit-exact against the host ffmpeg's p010le vs yuv420p10le
      decode of the ramp clip. On the device AndroidImageCopy.ReadI420Deep
      copies each Image plane's bytes once and hands them to that same
      reader. In P010 mode the source decodes the first picture INSIDE the
      constructor so a decoder that answers with 8-bit pictures fails OpenVideo
      (NotSupportedException naming codec + stream), not the first
      TryReadFrame. The writer, Snapshot and the whole-file operations stay
      8-bit.
    * 8-BIT FRAMES OF A 10-BIT STREAM GO THROUGH P010 where a decoder lists it
      (HighBitDepthConversion.AsksForP010), falling back to flexible output.
      Found on the Pixel 4 XL: c2.qti.hevc.decoder decodes a Main 10 stream
      to flexible 8-bit ByteBuffers with "CCodecBufferChannel:
      sendOutputBuffers: tried 5 realloc and failed" + UNKNOWN_ERROR (its
      HAL_PIXEL_FORMAT_YCbCr_420_P010_VENUS graphic buffers cannot be
      converted), so every 8-bit OpenVideo / Snapshot / transcode of 10-bit
      HEVC failed there; its P010 output works and is cut to the top 8 bits
      exactly like the flexible path's cut. Google's c2.android.hevc.decoder on
      that phone fails on Main 10 altogether ("C2SoftHevcDec: Fatal error in
      decoder 0x43dd") and lists no P010.
    * Concatenation falls back to a RE-ENCODE when the inputs do not match
      (ConcatenationRule: MIME, size / sample format, byte-identical csd),
      rather than failing as the contract's copy-only wording suggests.

THE DEVICE PROOF - samples/VideoProcessingDiagnostics
    A one-screen .NET for Android app (ApplicationId
    com.codebrix.videoprocessing.diagnostics) that references the library as
    SOURCE (no natives to carry), bundles tests/Assets/clip-h264-aac.mp4,
    clip-hevc10-aac.mp4 and clip-h264-aac-srt.mp4 as assets, and one second after launch runs the whole
    contract in 23 steps
    (Register, Analyse, Snapshot, OpenVideo I420 / Gray / NV12 + RGBA /
    StartTime + Seek, OpenAudio at the source format and at 22050 Hz mono,
    CreateWriter I420 + AAC with fast start and tags, NV12 + RGBA input, HEVC,
    TrimFile both ways, ConcatenateFiles both ways, TranscodeFile, the
    capability refusals, cancellation, then 10-BIT: I420 10-bit with the ramp
    verified - 960 distinct levels, frame 0 written to
    frame0-i420-10bit.raw for a host cmp against ffmpeg's yuv420p10le decode,
    the 8-bit delivery = 10-bit >> 2 - Gray + NV12 10-bit, and 10-bit from
    the 8-bit clip = 8-bit << 2; without HighBitDepthFrameSource those steps
    check the refusal and pass with a note), then step 23: Analyse of the
    bundled clip-h264-aac-srt.mp4 reports ONE subtitle stream (mov_text, from
    MediaExtractor's text/3gpp-tt, language eng) and no chapters, and the
    engine offers no external tools (TryGetExternalTools false). A "VPDiag 10-bit probe:" line
    lists the decoders that offer P010. One line per step goes to logcat under
    the tag VPDiag, then "VPDIAG RESULT: PASS: ..." or "VPDIAG RESULT: FAIL:
    ...". Before the steps a CODEC PROBE decodes and encodes one frame; when it
    fails, a step that then fails with VideoProcessingException is reported
    PASSED-WITH-NOTE (with the probe's reason) - on a device whose codecs work
    nothing is masked.

        dotnet build samples/VideoProcessingDiagnostics/VideoProcessingDiagnostics.csproj -c Release
        adb -s <serial> install -r samples/VideoProcessingDiagnostics/bin/Release/net10.0-android36.1/com.codebrix.videoprocessing.diagnostics-Signed.apk
        adb -s <serial> logcat -c
        adb -s <serial> shell cmd package resolve-activity --brief com.codebrix.videoprocessing.diagnostics
        adb -s <serial> shell am start -n <the component printed above>
        (wait for the RESULT line - about 20 s on a phone)
        adb -s <serial> logcat -d -s VPDiag
        adb -s <serial> pull /sdcard/Android/data/com.codebrix.videoprocessing.diagnostics/files/ .
        adb -s <serial> shell am force-stop com.codebrix.videoprocessing.diagnostics

    The pulled MP4s can be checked with the host ffprobe / ffmpeg (every one
    should decode with no errors), and the pulled frame0-i420-10bit.raw must be
    byte-identical to
        ffmpeg -i tests/Assets/clip-hevc10-aac.mp4 -frames:v 1 -f rawvideo -pix_fmt yuv420p10le -
    Proof devices so far: a Pixel 4 XL (arm64, API 33) - every step PASS,
    HighBitDepthFrameSource set, frame 0 bit-identical; and THE HP LAPTOP
    below (no working video codec, so its 10-bit steps are not meaningful).

THE HP LAPTOP (Android-x86, x86_64, API 33)
    Its software-codec process (media.swcodec) aborts the first time a VIDEO
    codec allocates a graphic buffer: logcat shows "F GraphicBufferMapper:
    gralloc-mapper is missing" (the image's mapper HAL is an arm64 build:
    "libbase.so is for EM_AARCH64"), a tombstone, and then
    'Codec2 component "c2.android.avc.decoder" died' - also killing any
    AAC codec in the same process at that moment. The only other codecs it has
    are ffmpeg-based vorbis / trial ones, so it has NO working video codec.
    The engine handles it as designed: Analyse, OpenAudio, the snapped trim
    and the matching concatenation (sample copies, no codec) PASS, and every
    video codec step fails with VideoProcessingException (the codec probe turns
    them into PASSED-WITH-NOTE). Do not fight the device.

ENCODER PACKAGES - how they plug into the Core
==============================================
Standalone encoders (the first is CodeBrix.VideoEncoding.Av1, in its own
repository) depend on the published CodeBrix.VideoProcessing.Core package -
never a ProjectReference across repositories - and plug in through
CodeBrix.VideoProcessing.Core.Encoding:

    * The package implements IVideoEncoderFactory (one shared, thread-safe
      instance per codec id from VideoCodecIds) and IVideoEncoder, and offers
      a static class named after the package (CodeBrixVideoEncodingAv1) whose
      idempotent, locked Register() calls VideoEncoders.Register(factory).
    * VideoEncoders mirrors VideoProcessors: a locked List, idempotent on the
      instance, internal Unregister / Clear for the tests. Lookups are by codec
      id (ignoring case) and the most recent registration for an id wins. The
      codec id is read ONCE at registration.
    * Create validates the request (VideoEncoderRequest.Validate), refuses
      what Capabilities lacks (VideoEncoderCapabilities.ThrowIfUnsupported)
      and keeps request.Clone(). Codec knobs travel in
      VideoEncoderRequest.CodecOptions (string -> string) and Speed is
      codec-specific - the Core never enumerates any codec's options. Add a
      typed property to VideoEncoderRequest only when it means the same thing
      for every codec.
    * Packets are numbered by submission (the Nth Encode is frame N, stamped
      FrameRate.TimestampOf(N)), are fresh buffers, and come out through the
      TryReadPacket pull loop; CodecConfigurationRecord (av1C / avcC / hvcC) is
      set no later than the first packet.
    * The encoder contract knows nothing about containers, alpha streams,
      RGBA conversion or CodeBrixVideo formats; an alpha channel is a second
      Gray (full-range) encoder driven in lock step by the consumer.
    * VideoEncoderNotRegisteredException.MessageFor names the AV1 package and
      its Register call for "av1"; when another encoder package appears, add
      its codec id and package name there (and to the tests in
      VideoEncodersTests).
VideoEncodersTests holds EVERY test that touches the process-wide encoder
registry (one class = run one at a time), just as VideoProcessorsTests does
for the engine registry.


FILTER-GRAPH ESCAPING - FilterGraphEscaper, and why the old arguments bypass it
==============================================================================
FFMpeg/Arguments/FilterGraphEscaper.cs is an INTERNAL static helper with exactly
two methods. It exists because ffmpeg unescapes a filter option value TWICE
while parsing a filtergraph: once when the "name=options" filter description is
tokenised (terminators [ ] , ;) and once when that options string is split into
key=value pairs (terminators : and =). Both passes drop a backslash in front of
any character.

    EscapeValue(v)              escapes \ ' : , ; [ ] =  TWICE OVER, so the
                                value survives both passes. Applying the same
                                one-level escape function twice is uniformly
                                correct - over-escaping a character that only
                                one pass cares about is harmless, because the
                                other pass just strips the extra backslash.
                                Verified against ffmpeg 7.1.5 with real paths
                                containing : , ; [ ] ' = \ and spaces.
    EscapeChainSeparators(t)    escapes , ; [ ] ONCE, leaving : = \ ' alone, so
                                an already-formed option string keeps working.
                                It is deliberately NOT quote-aware.

WHY THE OLDER ARGUMENTS DO NOT USE IT. VideoFiltersArgument / AudioFiltersArgument
have always rendered every value with a single `Value.Replace(",", "\\,")` and
nothing else. Scale, Transpose, Mirror, Crop, Pad, DrawText, HardBurnSubtitle,
BlackDetect, BlackFrame and the audio filters all depend on that: scale=200:300
must keep its colon, drawtext builds its own quoting through StringExtensions
.ToFFmpegLibavfilterPath, and ArgumentBuilderTest asserts their exact rendered
text. Routing them through the new helper would change every one of those
strings, so they are left exactly as they were.

The two groups are told apart by the EMPTY marker interface
IPreEscapedFilterArgument. The chain builders read:

    var escapedValue = arg is IPreEscapedFilterArgument
        ? arg.Value
        : arg.Value.Replace(",", "\\,");

so an argument that carries the marker has escaped its own value and is emitted
verbatim; without the marker the historical comma-only escape still applies.
Any NEW filter argument that owns a free-text value (a path, a caption, a user
option string) escapes it with FilterGraphEscaper and carries the marker. A new
filter whose value it fully controls (a number, an enum name) does not need
either - but carrying the marker costs nothing and is clearer.

THE ONE -vf PER OUTPUT GUARD. FFMpegArgumentOptions.WithVideoFilters and
WithAudioFilters throw FFMpegArgumentException when a chain has already been
added to the SAME options instance, because ffmpeg keeps only the last -vf / -af
it is given and drops the others silently. The check is per options instance, so
each output of a MultiOutput, and an input's own callback, still gets its own
chain. It deliberately does NOT look at CropArgument or GifPaletteArgument,
which render their own -vf / -filter_complex: catching those would change
behaviour that existing tests pin down. That remains a documented pitfall.


PIPE FAILURES VERSUS FFMPEG FAILURES - why one of them is held back
===================================================================
DELIBERATE DIVERGENCE FROM UPSTREAM FFMpegCore, in
FFMpeg/FFMpegArgumentProcessor.cs. Upstream Process() awaits

    await Task.WhenAll(instance.WaitForExitAsync().ContinueWith(...),
                       _ffMpegArguments.During(cancellationTokenSource.Token));

and lets Task.WhenAll pick which failure surfaces. That picks wrongly whenever
ffmpeg is the thing that failed. When ffmpeg exits early - a codec it will not
open, a pixel format it refuses, an unwritable output - whatever is still
writing into its pipe fails with

    System.IO.IOException: Pipe is broken

and THAT is what the caller sees. ffmpeg's exit code and stderr, the only
things that say why it quit, are never reported, because HandleCompletion is
not reached.

WHAT THIS PORT DOES INSTEAD. Task.WhenAll throws only once BOTH tasks have
settled, so the exit code is already known by the time it does. Process()
therefore catches just that case:

    catch (IOException pipeFailure) when (processResult != null && processResult.ExitCode != 0)

stashes it in _pipeFailure and returns normally, so HandleCompletion raises the
usual FFMpegException carrying ffmpeg's stderr, with the IOException as its
InnerException.

THREE THINGS THIS DELIBERATELY DOES NOT CHANGE - keep them that way:
    * ONLY IOException is held back. An FFMpegStreamFormatException from
      RawVideoPipeSource.CheckFrameAndThrow, or anything else a pipe source
      throws, still propagates straight out of Process(). Four VideoTest cases
      assert the exact type FFMpegStreamFormatException, and xUnit's
      Assert.Throws matches the exact type.
    * It is held back ONLY when ffmpeg actually failed (ExitCode != 0). A
      broken pipe alongside a clean ffmpeg exit propagates as it always did.
    * HandleCompletion rethrows a held-back failure - through
      ExceptionDispatchInfo, so the original stack trace survives - whenever it
      is not going to raise FFMpegException itself. That is what keeps
      ProcessSynchronously(throwOnError: false) behaving exactly as before.

Cancellation outranks both: when _cancelled is set, _pipeFailure is discarded
and OperationCanceledException is thrown, because there the broken pipe is
merely a side effect of the cancel.


OUTPUT PIPES DRAIN BEFORE THEY ARE DISPOSED - a CodeBrix fix of an upstream bug
===============================================================================
CODEBRIX FIX ON TOP OF THE FFMpegCore PORT, in FFMpeg/FFMpegArgumentProcessor.cs
(plus two small internal hooks in FFMpegArguments.cs and OutputPipeArgument.cs).
Upstream's exit handler is

    processResult = t.Result;
    cancellationTokenSource.Cancel();
    _ffMpegArguments.Post();

and the SAME token is the one an OutputPipeArgument hands its IPipeSink. So the
instant ffmpeg exits, every sink is cancelled and every output pipe disposed -
even though the pipe still holds whatever ffmpeg wrote that the sink has not
read yet. ffmpeg cannot exit while it still has unwritten output (it blocks on
a full pipe), so this only bites a sink that is SLOWER than ffmpeg - and then
it bites every time. PipeArgument.During swallows the OperationCanceledException,
so the tail is lost with NO error. Measured: a slow StreamPipeSink reading a
rawvideo decode of input_3sec.webm received 31,053,312 / 31,096,832 of
31,104,000 bytes and the run "succeeded".

WHAT THIS PORT DOES INSTEAD
    * OutputPipeArgument.SinkCancellationToken (internal): the processor sets it
      to a token of its own (sinkCancellationSource) before During starts; the
      sink reads under it. The During token still bounds the wait for ffmpeg to
      CONNECT, so an output pipe ffmpeg never opened (it failed first) is still
      abandoned at exit exactly as upstream. Left unset - During called directly
      - the sink reads under the During token as before.
    * At exit the processor still cancels the During token and posts every
      argument EXCEPT the output pipes (FFMpegArguments.PostAllExceptOutputPipes).
      Input pipes are deliberately unchanged: a source still writing when ffmpeg
      exits is writing into a closed read end, and fails as it always did.
    * The connected sink reads to EOF (ffmpeg's write end closed), During
      completes, and only then are the output pipes posted
      (FFMpegArguments.PostOutputPipes, in Process()'s finally; idempotent).
    * BOUNDED. The wait is bounded by the PUBLIC FFOptions.OutputPipeDrainTimeout
      (default 30 s, counted from ffmpeg's exit; >= 0 or InfiniteTimeSpan,
      checked by ResolveOutputPipeDrainTimeout before ffmpeg starts). The
      internal nullable FFMpegArgumentProcessor.OutputPipeDrainTimeout, when
      set, overrides it for that processor (tests use it). ffmpeg can only
      have left one pipe buffer's worth unread, so a working sink drains in a
      fraction of that. On timeout the sink is cancelled, the pipe disposed,
      and Process() throws FFMpegException (Type = Process) "An output pipe
      sink did not drain ..." - raised regardless of throwOnError, with
      whatever the abandoned sink threw as InnerException. It is never
      swallowed.
    * CANCELLATION stays prompt and never waits for the drain. A cancel before
      ffmpeg exits makes the exit handler cancel the sinks and post the output
      pipes at once (upstream's behaviour exactly); a cancel that arrives while
      a sink is draining completes cancelRequested from OnCancelEvent, which
      ends the drain wait at once, and the sinks are torn down the same way.
      duringTask is NOT awaited on its own in the Task.WhenAll (that waited
      without bound for a sink that ignores its token): after a cancel or a
      drain timeout a torn-down sink gets TornDownSinkGrace (1 s) to return,
      then is abandoned (its exception observed, not raised).
    * A CANCEL IS REPORTED AS A CANCEL. A sink that ignores its token fails on
      the disposed pipe - ObjectDisposedException ("Cannot access a closed
      pipe"), IOException - and that used to reach the caller instead of the
      cancel (and only after the full 30 s when the cancel came during the
      drain). After a user cancel, ObjectDisposedException / IOException /
      OperationCanceledException from the sinks or pipes are mapped
      (IsTearDownFailure) to OperationCanceledException carrying the token
      given to CancellableThrough, with the original as InnerException. Any
      other exception a sink throws still propagates unchanged.
    * A sink's own exception (thrown part way, or after reading to EOF - the
      latter used to be masked by the cancellation) propagates out of
      ProcessSynchronously / ProcessAsynchronously unchanged. Both share
      Process(), so both get the fix.
Fenced by tests/CodeBrix.VideoProcessing.Tests/OutputPipeDrainTests.cs.


A KILL ENDS THE WHOLE PROCESS TREE - a CodeBrix fix of an upstream bug
======================================================================
CODEBRIX FIX ON TOP OF THE FFMpegCore PORT, in Instances/ProcessInstance.cs.
Upstream's ProcessInstance.Kill() (and the kill in WaitForExitAsync's
cancellation callback) is Process.Kill(), which ends ONLY the process that was
started. The "ffmpeg" on the PATH is not always ffmpeg: a Chocolatey or Scoop
shim on Windows (C:\ProgramData\chocolatey\bin\ffmpeg.exe is one) is a small
launcher that starts the real ffmpeg as a CHILD and waits for it. Killing the
shim leaves the real ffmpeg running, still holding the redirected stdout and
stderr, and ProcessInstance reports the exit only once both reach EOF - so a
cancel waited until ffmpeg finished the whole job on its own. Measured:
OutputPipeDrainTests' token-ignoring-sink cancel took 113 s through the shim,
0.66 s against the real binary.

WHAT THIS PORT DOES INSTEAD: Process.Kill(entireProcessTree: true), as
Engine/FFmpegToolProcess.cs and Core's ExternalToolRunner already do. ffmpeg
and ffprobe start no children of their own, so where ffmpeg is launched
directly (the usual Linux and macOS install, or a BinaryFolder pointing at the
real binary) the tree is that one process and nothing changes. Supported on
Windows, Linux and macOS; it does not depend on the ffmpeg version.


AV1 ENCODER FALLBACK - the missing-libsvtav1 explanation and the opt-in fallback
===============================================================================
ADDITION OF THIS PORT - there is nothing like it in FFMpegCore. The CodeBrix
video family authors AV1 and defaults to SVT-AV1 (libsvtav1), but plenty of
ffmpeg builds - gyan.dev's Windows "essentials" build among them - have no
libsvtav1 while still having libaom-av1. On such a machine every AV1 encode
used to fail with nothing but "ffmpeg exited with non-zero exit-code (...)".

THE PIECES
    FFOptions.AllowAv1EncoderFallback   public, default false; round-trips
                                        through ffmpeg.config.json and Clone().
    FFMpeg/Av1EncoderFallback.cs        INTERNAL static helper: the command
                                        rewrite (Apply), the cached
                                        `ffmpeg -encoders` check
                                        (IsEncoderAvailable), the stderr scan
                                        (FindUnknownEncoder) and the message
                                        text (DescribeMissingSvtAv1).
    Exceptions/FFMpegEncoderNotFoundException.cs
                                        public FFMpegException subclass,
                                        Type == Process, plus EncoderName.
    FFMpegArgumentProcessor             ApplyAv1EncoderFallback (called from
                                        PrepareProcessArguments), the extra
                                        branch in HandleCompletion, and the
                                        public Av1EncoderFallbackApplied /
                                        Av1EncoderFallbackNotes.

THE ERROR. HandleCompletion builds the historical message exactly as before.
If ffmpeg's stderr contains "Unknown encoder '<name>'", it throws
FFMpegEncoderNotFoundException instead of a plain FFMpegException - still with
the same message, inner exception (_pipeFailure) and FFMpegErrorOutput. Only
when <name> is libsvtav1 is DescribeMissingSvtAv1(...) + " " put IN FRONT of
the message. The prefix is the ONLY change to the text: the historical message
follows in full, and the tests assert that it does. The prefix has two forms:
with the fallback off it suggests turning it on; with the fallback on it says
why the fallback could not be used.

THE FALLBACK. ApplyAv1EncoderFallback runs after VerifyFFMpegExists and BEFORE
_ffMpegArguments.Text is rendered, so the command ffmpeg receives - and
processor.Arguments afterwards - already names libaom-av1. It does nothing
unless the run's options have AllowAv1EncoderFallback on. Apply then:
    * splits the ordered argument list into OUTPUT segments: the arguments
      after the previous IInputArgument / IOutputArgument, up to the next
      IOutputArgument. Input options (before an IInputArgument) are never
      touched;
    * returns without asking about encoders at all unless some output segment
      selects libsvtav1 - so a run that never mentions it spawns no extra
      process;
    * asks `ffmpeg -hide_banner -encoders` (per-run binary path, cached per path
      while UseCache is on, and a failed listing is never cached). libsvtav1
      present -> no change. Both missing -> no change, Problem recorded for the
      message;
    * rewrites ONLY the segments that select libsvtav1: codec -> libaom-av1,
      numeric preset -> -cpu-used clamped 0..8, -b:v 0 after -crf unless the
      segment sets a video bit rate, -svtav1-params dropped. The list is
      rewritten IN PLACE (Clear + AddRange), so Pre/During/Post and
      IDynamicArgument still see the same argument objects for everything else.
Typed arguments (VideoCodecArgument, NumericSpeedPresetArgument,
ConstantRateFactorArgument, VideoBitrateArgument, EncoderParamsArgument) are
recognised, and so is a CustomArgument whose WHOLE text is one of those options
(the regexes are anchored). A codec buried in a longer custom string is
deliberately not parsed out; the "fallback on" message names that case.

WHY THE PRESET IS CLAMPED RATHER THAN SCALED. CodeBrix.VideoPlayback.Authoring
already passes the SAME speed number to -cpu-used when it is asked for
libaom-av1, so the clamp keeps the family consistent: preset 8 means 8 either
way, and only 9..13 (which libaom has no room for) change.

WHY IT IS OPT-IN. libaom is many times slower than SVT-AV1 and its rate factor
does not land on identical quality, so silently switching could turn a
calibrated, minutes-long job into an hour-long one with different output. A
developer has to ask for that trade.


NOTES
=====
    * Public surface at the time of writing: nearly 200 public types across the
      root, Arguments, Enums, Builders.MetaData, Pipes, Extend, Helpers,
      Exceptions, Imaging, Instances and Instances.Exceptions namespaces.
      AGENT-README.txt documents all of them - including the newer filter and
      option argument types and the IPreEscapedFilterArgument marker - and its
      IArgument catalogue is the section that goes stale first; keep it in step
      when the surface changes.
    * src/CodeBrix.VideoProcessing/InternalsVisibleTo.cs grants internals to
      CodeBrix.VideoProcessing.Tests - the tests reach internal types such as
      MediaAnalysis and the FFMpegCache.
    * GlobalFFOptions reads an `ffmpeg.config.json` from the process's current
      directory on first access; the test project ships one (with an empty
      BinaryFolder) to exercise that path. Tests whose names begin with ZZZ
      deliberately mutate the process-wide options and restore them in a
      finally block; keep that prefix and that discipline if you add more.
    * The library writes temp files (FFMETADATA content, demux concat lists)
      into FFOptions.TemporaryFilesFolder and deletes them in the argument's
      Post() step.
    * Output pipes are posted LATE - after their sinks have drained, not at
      ffmpeg's exit. That is a CodeBrix fix of an upstream FFMpegCore bug (a
      slow sink silently lost the tail); see OUTPUT PIPES DRAIN BEFORE THEY ARE
      DISPOSED. Do not "re-sync" it away when diffing against upstream.
    * ProcessInstance kills the whole process tree, not just the process it
      started - also a CodeBrix fix; see A KILL ENDS THE WHOLE PROCESS TREE.
      Do not "re-sync" it away either.
================================================================================
END OF MAINTAINER-README
================================================================================
