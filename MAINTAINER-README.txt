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
This repository produces exactly ONE NuGet package.

    Package id ........ CodeBrix.VideoProcessing.MitLicenseForever
    Project ........... src/CodeBrix.VideoProcessing/CodeBrix.VideoProcessing.csproj
    Assembly / root ns  CodeBrix.VideoProcessing
    Consumer doc ...... AGENT-README.txt (repo root) - shipped inside the nupkg
    License ........... MIT

The library is a fully managed, cross-platform FFmpeg / FFprobe wrapper. It
launches the external `ffmpeg` and `ffprobe` executables and parses their
output; it contains no native code and no P/Invoke.


REPOSITORY LAYOUT
=================
    src/CodeBrix.VideoProcessing/          -- the one and only library / package
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
        Instances/                         -- the vendored process wrapper
        Instances/Exceptions/
        FFOptions.cs, GlobalFFOptions.cs, FFMetadataInputArgument.cs
        InternalsVisibleTo.cs              -- grants internals to
                                              CodeBrix.VideoProcessing.Tests
    tests/CodeBrix.VideoProcessing.Tests/  -- xUnit v3 + SilverAssertions
        Resources/                         -- sample media copied to the test
                                              output directory
        Utilities/                         -- BitmapSource (synthetic frames on
                                              CodeBrix.Imaging),
                                              OsSpecificFactAttribute
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
                                              Tests folder carries both test
                                              projects
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

Target framework: net10.0 only. No multi-targeting. The library has one package
reference (CodeBrix.Imaging.ApacheLicenseForever); everything else comes from
the shared framework.

The library csproj sets GeneratePackageOnBuild=true, so an ordinary Debug build
also produces a .nupkg (see PACKAGING below).


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
    ArgumentBuilderTest (renders and asserts command-line strings),
    PixelFormatTests, MetaDataBuilderTests, and most of FFMpegOptionsTests.
    Everything else is an integration test.

SKIPPED TESTS
    One FFProbeTests case (Uri_Duration) is skipped as consistently failing on
    hosted ubuntu agents. Nothing else in the suite is skipped.

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
There is no separate pack driver. The library csproj sets
GeneratePackageOnBuild=true, so `dotnet build` (or `dotnet pack`) on
src/CodeBrix.VideoProcessing produces CodeBrix.VideoProcessing.MitLicenseForever
.nupkg.

WHAT SHIPS IN THE NUPKG (the <None ... Pack="true"> items in the csproj):
    icon-codebrix-128.png     (PackageIcon)
    README.md                 (PackageReadmeFile)
    AGENT-README.txt          (the consumer documentation)
    THIRD-PARTY-NOTICES.txt   (upstream attribution and license texts)
    the generated XML documentation file

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
    * There is currently ONE deliberate behavioural divergence from upstream:
      how FFMpegArgumentProcessor chooses between a broken pipe and ffmpeg's own
      failure. See PIPE FAILURES VERSUS FFMPEG FAILURES below before
      touching that code.


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
================================================================================
END OF MAINTAINER-README
================================================================================
