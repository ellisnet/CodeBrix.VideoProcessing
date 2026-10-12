================================================================================
EXTRAS-README: CodeBrix.VideoProcessing
Samples, tools and other content in this repository that is not part of a NuGet
package
================================================================================

Besides the libraries, this repository holds ONE sample application - the
Android device-proof app under samples/ - the four projects under tests/, and
the media they use. There are no demos or tools.


samples/VideoProcessingDiagnostics
==================================
Path:  samples/VideoProcessingDiagnostics/
What:  A one-screen .NET for Android app (API 33 and newer, arm64 and x86_64)
       that proves the CodeBrix.VideoProcessing.Android engine on a real
       device. It references the library as source, bundles
       tests/Assets/clip-h264-aac.mp4 and clip-hevc10-aac.mp4, and one second
       after launch runs the whole CodeBrix.VideoProcessing.Core contract in
       22 steps - analyse, snapshot, every offered frame layout with seeking,
       audio with resampling, MP4 writing from I420 / NV12 / RGBA frames (and
       HEVC where the device can), trim, concatenate, transcode, the
       capability refusals, cancellation and 10-bit frames (where the device
       has a P010 decoder) - logging one PASS / PASSED-WITH-NOTE / FAIL line per
       step and a final "VPDIAG RESULT:" line to logcat (tag VPDiag). The MP4s
       it writes are copied to the app's external files folder for adb pull.
Run:   See MAINTAINER-README.txt, "THE ANDROID ENGINE" (build, install, start,
       logcat, pull, force-stop). Not packable, not published.
Shows: The engine used exactly as an application uses it: Register at start-up,
       then only the Core contract - off the UI thread.


tests/CodeBrix.VideoProcessing.Android.Tests
============================================
Path:  tests/CodeBrix.VideoProcessing.Android.Tests/
What:  Host (net10.0) xUnit v3 + SilverAssertions tests of the Android engine's
       platform-independent half (src/CodeBrix.VideoProcessing.Android/
       Managed/, compiled in as linked sources): pixel and audio conversions,
       the resampler and mixer, the quality -> bitrate table, the constant-rate
       scheduler, the MP4 fast-start / tag rewriter and timing reader, the
       MediaFormat mapping, the concatenation rule, the 10-bit (P010) readers
       and the capability probe, the capability gate and registration. Not packable, not published.
Run:   dotnet test CodeBrix.VideoProcessing.slnx (runs every suite). The MP4
       tests that need real files use the host ffmpeg and skip without it.


tests/Assets
============
Path:  tests/Assets/
What:  clip-h264-aac.mp4 - a synthetic 4-second H.264 + AAC clip made with
       ffmpeg's test-pattern and tone generators - and clip-hevc10-aac.mp4 - a
       synthetic 3-second lossless HEVC Main 10 + AAC clip whose luma is a ramp
       of 960 distinct 10-bit levels; PROVENANCE.txt records the exact
       commands. Used by the Android host tests and bundled into the
       device-proof app. Not shipped in any nupkg.


tests/CodeBrix.VideoProcessing.Tests
====================================
Path:  tests/CodeBrix.VideoProcessing.Tests/
What:  The xUnit v3 + SilverAssertions test suite for the library. It is not
       packable and is not published.
Run:   dotnet test CodeBrix.VideoProcessing.slnx
       Requires `ffmpeg` and `ffprobe` on PATH; on Debian-based Linux,
       `sudo apt install ffmpeg`.
Shows: The suite doubles as the worked-example collection for the package -
       ArgumentBuilderTest.cs asserts the exact command line every fluent call
       renders, FFMpegArgumentOptionsTests.cs does the same for the newer
       output options (subtitle codec, encoder params, numeric preset, metadata
       and stream metadata, dispositions, cues_to_front, reserve_index_space,
       autorotate), VideoFilterOptionsTests.cs covers the newer video filters
       (Custom, Fps, Lut3D, Format, ZScale, Tonemap, Rotate), their escaping
       and the one -vf per output guard, VideoFilterIntegrationTests.cs runs a
       lut3d + zscale + fps + format chain through a real ffmpeg,
       VideoTest.cs and AudioTest.cs exercise real conversions, pipes,
       progress and metadata, and FFProbeTests.cs covers analysis of files,
       URIs and streams. The Engine/ folder tests the package as the
       CodeBrix.VideoProcessing.Core engine (analysis, frame and sample
       sources, the MP4 writer, transcode / trim / concatenate, Register).
       AGENT-README.txt links these files under "WORKING EXAMPLES ON GITHUB".


tests/CodeBrix.VideoProcessing.Core.Tests
=========================================
Path:  tests/CodeBrix.VideoProcessing.Core.Tests/
What:  The xUnit v3 + SilverAssertions test suite for the
       CodeBrix.VideoProcessing.Core contract package. Pure unit tests over an
       in-memory fake engine (FakeVideoProcessor.cs); needs no ffmpeg. Not
       packable, not published.
Run:   dotnet test CodeBrix.VideoProcessing.slnx (runs every suite)
Shows: ContractScenarioCoverage.cs walks the contract the way a consumer does
       (analyse, read frames, feed a writer) and pins the ownership and
       timestamp rules; the other files cover each contract type.


tests/CodeBrix.VideoProcessing.Tests.WaitingProgram
===================================================
Path:  tests/CodeBrix.VideoProcessing.Tests.WaitingProgram/
What:  A one-statement console application (`Console.Read();`) that blocks until
       a line arrives on its stdin. It exists purely so the vendored process
       wrapper's tests have a real child process to start, send input to, wait
       on and kill.
Run:   You never run it by hand. On their first run the Instances tests publish
       it themselves with
       `dotnet publish ../../../../CodeBrix.VideoProcessing.Tests.WaitingProgram
        -c Release -o ./waiting-program`
       into the test output directory.
Shows: Nothing about the public API - it is test scaffolding.


Optional / generated test data
==============================
Path:  tests/CodeBrix.VideoProcessing.Tests/Resources/
What:  Small sample media used by the integration tests: short mp4, webm, mkv,
       mov and audio-only clips (including rotated and HDR variants), WAV files
       at several bit depths, an mp3, a raw PCM file, an .srt subtitle file, a
       cover image and an image sequence folder. The test csproj copies them to
       the test output directory.
Note:  These files are inputs to the tests, not package content, and they are
       not shipped in the nupkg.

Path:  tests/CodeBrix.VideoProcessing.Tests/ffmpeg.config.json
What:  A minimal FFOptions configuration file ({"BinaryFolder": ""}) that lets
       the option tests verify GlobalFFOptions' automatic loading of
       `ffmpeg.config.json` from the working directory.

Path:  tests/CodeBrix.VideoProcessing.Tests/Utilities/
What:  BitmapSource.cs generates synthetic IVideoFrame sequences on
       CodeBrix.Imaging so the pipe tests need neither System.Drawing.Common nor
       SkiaSharp; OsSpecificFactAttribute.cs is an xUnit [Fact] that skips
       unless the current OS matches, replacing the upstream MSTest attribute.

================================================================================
END OF EXTRAS-README
================================================================================
