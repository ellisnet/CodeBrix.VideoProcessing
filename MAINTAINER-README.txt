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
This repository produces TWO NuGet packages.

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

The root AGENT-README.txt is the CodeBrix.VideoProcessing package's file, NOT a
repository-wide one. README-INDEX.txt maps the two.

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

Project reference graph inside the repository:

    CodeBrix.VideoProcessing  --ProjectReference-->  CodeBrix.VideoProcessing.Core

The ProjectReference packs as a package DEPENDENCY (not a bundled assembly).
Both projects carry the SAME date-stamped version block, so one build/pack run
stamps both with one version and the dependency is declared at that same
version. A third package, CodeBrix.VideoProcessing.Android (the Android
engine), is planned for this repository and will follow the same pattern.


REPOSITORY LAYOUT
=================
    src/CodeBrix.VideoProcessing.Core/     -- the contract package
        (root)                             -- VideoProcessors, IVideoProcessor,
                                              VideoProcessorCapabilities,
                                              VideoProcessorExtensions,
                                              VideoProgress and the three
                                              exceptions
        Media/                             -- MediaInfo, Video/AudioStreamInfo,
                                              FrameRate, colour / chroma enums
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
                                              Tests folder carries all three
                                              test projects
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

Target framework: net10.0 only. No multi-targeting. CodeBrix.VideoProcessing
has one package reference (CodeBrix.Imaging.ApacheLicenseForever) and one
project reference (CodeBrix.VideoProcessing.Core); the Core has none at all;
everything else comes from the shared framework.

Both library csprojs set GeneratePackageOnBuild=true, so an ordinary Debug
build also produces both .nupkg files (see PACKAGING below).


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
There is no separate pack driver. Both library csprojs set
GeneratePackageOnBuild=true, so `dotnet build` (or `dotnet pack`) of the slnx
produces CodeBrix.VideoProcessing.MitLicenseForever.<v>.nupkg and
CodeBrix.VideoProcessing.Core.MitLicenseForever.<v>.nupkg. The
CodeBrix.VideoProcessing nuspec declares a dependency on
CodeBrix.VideoProcessing.Core.MitLicenseForever at the Core package's version
from the same run. PUBLISH BOTH TOGETHER - a VideoProcessing package whose Core
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
    * Target framework net10.0 only; no multi-targeting.
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

ENGINE TESTS (tests/CodeBrix.VideoProcessing.Tests/Engine/) need ffmpeg with
libx264, libx265, aac and ffv1 (the alpha fixture is a lossless FFV1 clip
generated at test time). They take well under a minute.


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
    * BOUNDED. There was no existing pipe or drain timeout in the processor to
      reuse, so the wait is bounded by the internal
      FFMpegArgumentProcessor.OutputPipeDrainTimeout (30 s, counted from
      ffmpeg's exit; tests shorten it). ffmpeg can only have left one pipe
      buffer's worth unread, so a working sink drains in a fraction of that.
      On timeout the sink is cancelled, the pipe disposed, and Process() throws
      FFMpegException (Type = Process) "An output pipe sink did not drain ..."
      - raised regardless of throwOnError, with whatever the abandoned sink
      threw as InnerException. It is never swallowed.
    * CANCELLATION stays prompt. A cancel before ffmpeg exits makes the exit
      handler cancel the sinks and post the output pipes at once (upstream's
      behaviour exactly); a cancel that arrives while a sink is draining
      cancels sinkCancellationSource from OnCancelEvent. Either way the run ends
      in OperationCanceledException, which still outranks everything.
    * A sink's own exception (thrown part way, or after reading to EOF - the
      latter used to be masked by the cancellation) propagates out of
      ProcessSynchronously / ProcessAsynchronously unchanged. Both share
      Process(), so both get the fix.
Fenced by tests/CodeBrix.VideoProcessing.Tests/OutputPipeDrainTests.cs.


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
================================================================================
END OF MAINTAINER-README
================================================================================
