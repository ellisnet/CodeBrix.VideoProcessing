================================================================================
EXTRAS-README: CodeBrix.VideoProcessing
Samples, tools and other content in this repository that is not part of a NuGet
package
================================================================================

This repository ships no sample applications, demos or tools. Everything that is
not a library is test content. The only non-package content is the three
projects under tests/ and the sample media they use.


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
