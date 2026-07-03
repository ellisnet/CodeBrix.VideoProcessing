========================================================================
AGENT-README: CodeBrix.VideoProcessing
A Comprehensive Guide for AI Coding Agents
========================================================================

OVERVIEW
------------------------------------------------------------------------
CodeBrix.VideoProcessing is a fully managed, cross-platform FFmpeg /
FFprobe wrapper for .NET. It is a faithful port of the open-source
FFMpegCore 5.4.0 library, with the open-source Instances 3.0.2 process-
wrapper library vendored directly in, so the shipped NuGet package has no
runtime dependency on a separate "Instances" package. Every public type
that FFMpegCore exposes is available here with identical behavior, under
the CodeBrix.VideoProcessing.* namespaces instead of FFMpegCore.*.

Like FFMpegCore, this is a WRAPPER: at runtime it launches the external
`ffmpeg` and `ffprobe` executables. Those binaries must be installed and
discoverable (on PATH, or via GlobalFFOptions.Configure). The library
does not bundle them.


INSTALLATION
------------------------------------------------------------------------
NuGet package:  CodeBrix.VideoProcessing.MitLicenseForever

    dotnet add package CodeBrix.VideoProcessing.MitLicenseForever

The package id carries the ".MitLicenseForever" license suffix, but the
namespace you write in code is just "CodeBrix.VideoProcessing" (no
suffix). Target framework: .NET 10.0 or higher.

An optional companion package in this same repository,
CodeBrix.VideoProcessing.Extensions.Imaging.MitLicenseForever, adds an
in-memory image bridge built on CodeBrix.Imaging (see ARCHITECTURE).


KEY NAMESPACES
------------------------------------------------------------------------
    using CodeBrix.VideoProcessing;               // FFMpeg, FFProbe,
                                                  // FFMpegArguments,
                                                  // FFOptions, ...
    using CodeBrix.VideoProcessing.Arguments;     // individual argument
                                                  // types
    using CodeBrix.VideoProcessing.Builders.MetaData;  // MetaDataBuilder
    using CodeBrix.VideoProcessing.Enums;         // VideoCodec, AudioCodec,
                                                  // VideoSize, PixelFormat...
    using CodeBrix.VideoProcessing.Exceptions;    // FFMpegException,
                                                  // FFProbeException, ...
    using CodeBrix.VideoProcessing.Pipes;         // IPipeSource / IPipeSink
                                                  // and raw frame sources
    using CodeBrix.VideoProcessing.Instances;     // vendored process wrapper
    using CodeBrix.VideoProcessing.Instances.Exceptions;


CORE API REFERENCE
------------------------------------------------------------------------
FFProbe (static) - media analysis:
    IMediaAnalysis Analyse(string path, FFOptions ffOptions = null, ...)
    Task<IMediaAnalysis> AnalyseAsync(string path, ...)
    Analyse/AnalyseAsync(Stream), GetStreamJson, FrameAnalysis, etc.
  IMediaAnalysis exposes Duration, Format, PrimaryVideoStream,
  PrimaryAudioStream, VideoStreams, AudioStreams, SubtitleStreams.

FFMpeg (static) - conversion / snapshots / helpers:
    bool Snapshot(input, output, Size? size, TimeSpan? captureTime, ...)
    Task<bool> SnapshotAsync(...)
    bool GifSnapshot(...) / Task<bool> GifSnapshotAsync(...)
    bool Convert(...), Join(...), JoinImageSequence(...),
    PosterWithAudio(...), ExtractAudio(...), ReplaceAudio(...),
    Mute(...), and more.

FFMpegArguments + FFMpegArgumentOptions - the fluent builder:
    FFMpegArguments.FromFileInput(path) / FromPipeInput(...) /
    FromUrlInput(...) / FromDeviceInput(...)
        .OutputToFile(path, overwrite, options => options
            .WithVideoCodec(...).WithAudioCodec(...)
            .WithConstantRateFactor(...).Resize(...).Seek(...) ... )
        .ProcessSynchronously() / .ProcessAsynchronously()

FFOptions / GlobalFFOptions - configure the ffmpeg/ffprobe binary
folder, temp folder, encoding, etc. GlobalFFOptions.Configure(...) sets
process-wide defaults.

Error model: FFMpegException / FFProbeException (and subtypes in
CodeBrix.VideoProcessing.Exceptions) wrap non-zero exit codes and
process failures. The vendored process layer throws
InstanceException / InstanceFileNotFoundException /
InstanceProcessAlreadyExitedException from
CodeBrix.VideoProcessing.Instances.Exceptions.


CODING CONVENTIONS (CodeBrix family)
------------------------------------------------------------------------
  * Target framework net10.0 only; no multi-targeting.
  * Nullable reference types are DISABLED. Do not add '?' annotations to
    reference types and do not use the null-forgiveness '!' operator.
    Value-type nullables (int?, Size?, TimeSpan?, enum?) are fine.
  * Implicit usings are DISABLED. All usings are explicit, file-scoped
    namespaces only, System.* usings sorted first.
  * xUnit v3 + SilverAssertions for tests; no project-level warning
    suppression beyond the two documented exceptions below.
  * Ported files carry a `//was previously: <upstream-ns>;` provenance
    comment on their namespace line.

  SITUATIONAL EXCEPTIONS (documented, sanctioned by the CodeBrix
  observations for NRT-dependent / large-surface ports):

    1. CodeBrix.VideoProcessing.csproj sets
       <GenerateDocumentationFile>true</> together with <NoWarn>1591</>.
       The upstream FFMpegCore public surface (~150 types / hundreds of
       members) is only partially documented; the XML doc file is still
       produced (so the shipped package carries the upstream docs), but
       CS1591 is suppressed rather than inventing summaries for every
       member. Mirrors the CodeBrix.Platform.OpenGL precedent. Nullable
       and ImplicitUsings remain OFF (NOT excepted).


ARCHITECTURE
------------------------------------------------------------------------
Repository layout:

  src/CodeBrix.VideoProcessing/
      The main library. Preserves FFMpegCore's internal folder layout
      (FFMpeg/, FFMpeg/Arguments/, FFMpeg/Builders/MetaData/,
      FFMpeg/Enums/, FFMpeg/Exceptions/, FFMpeg/Pipes/, FFProbe/,
      Extend/, Helpers/) plus the vendored Instances/ subtree
      (Instances/ and Instances/Exceptions/). Zero external NuGet
      dependencies (System.Text.Json is used from the .NET 10 shared
      framework).

  src/CodeBrix.VideoProcessing.Extensions.Imaging/
      Optional image bridge. Re-expresses FFMpegCore's SkiaSharp /
      System.Drawing.Common image extensions purely in terms of
      CodeBrix.Imaging so it is cross-platform (Windows / Linux / macOS)
      with no SkiaSharp or System.Drawing.Common dependency:
        - FFMpegImage.Snapshot / SnapshotAsync -> returns a
          CodeBrix.Imaging Image (decoded from the PNG the snapshot pipe
          produces).
        - ImageExtensions.AddAudio -> writes an Image to a temp PNG and
          calls FFMpeg.PosterWithAudio.
        - ImageVideoFrameWrapper -> wraps an Image<Rgba32> as an
          IVideoFrame (Format "rgba") for RawVideoPipeSource.
      Depends only on CodeBrix.Imaging.ApacheLicenseForever.

  tests/CodeBrix.VideoProcessing.Tests/
      xUnit v3 + SilverAssertions. Ports the FFMpegCore and Instances
      test suites. Many tests are integration tests that require the
      ffmpeg/ffprobe binaries and the bundled media files under
      Resources/.

The vendored Instances layer (CodeBrix.VideoProcessing.Instances) is a
small process wrapper providing ProcessArguments / ProcessInstance /
IProcessResult, used internally by FFMpeg and FFProbe to launch and
capture the external binaries.


TESTING
------------------------------------------------------------------------
    dotnet test CodeBrix.VideoProcessing.slnx

Test prerequisites: the ffmpeg and ffprobe executables must be installed
and on PATH for the integration tests to pass. The Resources/ media
files are copied to the test output directory. Tests that only exercise
argument-string construction, enum/extension helpers, metadata building,
FFprobe JSON parsing, and the process wrapper do not require ffmpeg.

========================================================================
END OF AGENT-README
========================================================================
