# CodeBrix.VideoProcessing

A fully managed, cross-platform FFmpeg/FFprobe wrapper library for .NET that makes media analysis and conversion easy,
plus an engine-neutral video processing contract that the same wrapper implements.

CodeBrix.VideoProcessing is the FFmpeg/FFprobe wrapper. CodeBrix.VideoProcessing.Core is a companion library with no
dependencies at all: the contract for analysing media, decoding video frames and audio samples, writing standard MP4
video from your own frames and sound, and transcoding, trimming and joining files - through one interface that a
separate engine package implements for each platform. CodeBrix.VideoProcessing is that engine on Windows, Linux and
macOS.

CodeBrix.VideoProcessing and CodeBrix.VideoProcessing.Core are provided as .NET 10 libraries and associated
`CodeBrix.VideoProcessing.MitLicenseForever` and `CodeBrix.VideoProcessing.Core.MitLicenseForever` NuGet packages.

CodeBrix.VideoProcessing supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## Installation

```
dotnet add package CodeBrix.VideoProcessing.MitLicenseForever
```

| Package ID | Use it for | Brings in |
| --- | --- | --- |
| `CodeBrix.VideoProcessing.MitLicenseForever` | Driving FFmpeg/FFprobe directly, and the Windows/Linux/macOS engine for the Core contract | CodeBrix.VideoProcessing.Core, CodeBrix.Imaging |
| `CodeBrix.VideoProcessing.Core.MitLicenseForever` | Libraries that need video processing without depending on any particular engine | nothing |

Note that the NuGet package IDs and the namespaces are different - there is no package named plain `CodeBrix.VideoProcessing`:

* NuGet package ID: `CodeBrix.VideoProcessing.MitLicenseForever`
* Assembly and primary namespace: `CodeBrix.VideoProcessing` - i.e. `using CodeBrix.VideoProcessing;`
* NuGet package ID: `CodeBrix.VideoProcessing.Core.MitLicenseForever`
* Assembly and primary namespace: `CodeBrix.VideoProcessing.Core` - i.e. `using CodeBrix.VideoProcessing.Core;`

XML documentation (IntelliSense) ships alongside both assemblies.

The `CodeBrix.VideoProcessing.MitLicenseForever` package pulls in two dependencies automatically; no version pinning is needed in the consuming project:

* `CodeBrix.VideoProcessing.Core.MitLicenseForever` - the engine-neutral contract this package implements.
* `CodeBrix.Imaging.ApacheLicenseForever` - a fully managed, cross-platform, dependency-free CodeBrix imaging library, used only by the in-memory image bridge in `CodeBrix.VideoProcessing.Imaging`. Everything else in this library works without touching it. Note that this package is licensed under the Apache License 2.0 rather than MIT.

## CodeBrix.VideoProcessing supports:

* Analysing media files and streams with FFprobe (`FFProbe.Analyse` / `AnalyseAsync`) - duration, streams, codecs, resolution, bitrate, frames, packets, chapters and tags
* Converting, transcoding and muxing video and audio (`FFMpeg` / `FFMpegArguments`)
* A fluent argument builder covering scaling, cropping, seeking, bitrate, codecs, filters, hardware acceleration, concatenation, tee and multi-output, and dozens of other options
* Colour-managed video: zscale resizing and colour-space conversion, HDR-to-SDR tonemapping, and 3D LUT (`.cube`) grading, all inside a single filter chain
* Per-stream metadata and dispositions, subtitle codecs, WebM cue placement (`cues_to_front` / `reserve_index_space`), numeric AV1 speed presets, and free-form encoder parameter strings
* Extracting single-frame snapshots and animated GIF snapshots
* Piping raw audio/video frames into and out of FFmpeg via `IPipeSource` / `IPipeSink`
* Building and serialising FFmpeg metadata (`MetaDataBuilder`)
* A built-in process wrapper (`CodeBrix.VideoProcessing.Instances`)
* An in-memory image bridge under `CodeBrix.VideoProcessing.Imaging`: grab a snapshot as a CodeBrix.Imaging image (`FFMpegImage.Snapshot`), feed images into FFmpeg as frames (`ImageVideoFrameWrapper`), or mux an image + audio into a video (`ImageExtensions.AddAudio`)
* Serving as the Windows, Linux and macOS engine for CodeBrix.VideoProcessing.Core, registered with one call: `CodeBrixVideoProcessing.Register()`

## CodeBrix.VideoProcessing.Core supports:

* One registry (`VideoProcessors`) and one interface (`IVideoProcessor`), so a library can process video without knowing which engine the application chose
* Describing media files: container, duration, video streams (size, exact rational frame rate, bit depth, alpha, rotation, colour range / matrix / primaries / transfer) and audio streams
* Decoding video to I420 frames (and, where the engine offers it, NV12, RGBA, I444 or Gray frames at 8, 10 or 12 bits, with an optional alpha plane), and audio to interleaved 32-bit float samples, frame by frame with seeking
* Writing standard MP4 files (H.264 or HEVC video with AAC audio) from your own frames and sound, at a constant frame rate, with constant-quality or target-bitrate encoding, keyframe interval, colour tags, rotation metadata and fast start
* Whole-file transcode, trim (frame-accurate or keyframe-snapped) and lossless concatenation
* Capability flags, so a consumer can ask an engine what it supports before calling it
* An encoder registry (`VideoEncoders`) keyed by codec id, through which separate encoder packages plug in
* Its own frame, plane, frame-rate and media-description types - no image library, no dependencies

## Requirements

CodeBrix.VideoProcessing shells out to the `ffmpeg` and `ffprobe` executables, so they must be installed and discoverable at run time - either on the `PATH`, or in a folder configured through `GlobalFFOptions`. The library itself is fully managed C#: it does not bundle the FFmpeg binaries, does not download them, and does not P/Invoke into the FFmpeg libraries.

On Debian-based Linux, `sudo apt install ffmpeg` installs both. On Windows and macOS, install them yourself and either put them on the `PATH` or point `FFOptions.BinaryFolder` at the folder that holds them.

CodeBrix.VideoProcessing is not for Android - there is no ffmpeg executable to launch there, and `CodeBrixVideoProcessing.Register()` refuses to run on it. An Android application registers the `CodeBrix.VideoProcessing.Android` engine instead, and code written against CodeBrix.VideoProcessing.Core works unchanged on both.

CodeBrix.VideoProcessing.Core needs nothing at run time, but it contains no engine: the application must register one (see the sample below) before anything asks `VideoProcessors.Current`.

CodeBrix.VideoProcessing.Core also defines the seam for standalone video encoders. An encoder package (for AV1, `CodeBrix.VideoEncoding.Av1`, registered with `CodeBrixVideoEncodingAv1.Register()`) adds an `IVideoEncoderFactory` to the `VideoEncoders` registry under its codec id; a consumer asks `VideoEncoders.Get(VideoCodecIds.Av1)`, creates an `IVideoEncoder` from a `VideoEncoderRequest`, and pulls compressed packets out with `Encode` / `Flush` / `TryReadPacket`. Encoders take Core frames in and hand packets out - they know nothing about containers, alpha streams or file formats.

## Sample Code

### Analyse a media file

```csharp
using CodeBrix.VideoProcessing;

var info = FFProbe.Analyse("input.mp4");

Console.WriteLine($"Duration: {info.Duration}");
Console.WriteLine($"Video codec: {info.PrimaryVideoStream?.CodecName}");
Console.WriteLine($"Resolution: {info.PrimaryVideoStream?.Width}x{info.PrimaryVideoStream?.Height}");
```

### Convert a video

```csharp
using CodeBrix.VideoProcessing;
using CodeBrix.VideoProcessing.Enums;

FFMpegArguments
    .FromFileInput("input.webm")
    .OutputToFile("output.mp4", overwrite: true, options => options
        .WithVideoCodec(VideoCodec.LibX264)
        .WithConstantRateFactor(21)
        .WithAudioCodec(AudioCodec.Aac))
    .ProcessSynchronously();
```

### Build a video filter chain

```csharp
using CodeBrix.VideoProcessing;

FFMpegArguments
    .FromFileInput("input.mp4")
    .OutputToFile("output.mp4", overwrite: true, options => options
        .WithVideoFilters(filters => filters
            .Scale(1280, 720)
            .Fps(30)
            .Format("yuv420p")))
    .ProcessSynchronously();
```

### Save a thumbnail

```csharp
using System.Drawing;
using CodeBrix.VideoProcessing;

FFMpeg.Snapshot("input.mp4", "thumbnail.png", new Size(1920, 1080), TimeSpan.FromSeconds(10));
```

### Process video through the engine-neutral contract

```csharp
using CodeBrix.VideoProcessing;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;

// Once, at application start-up (Windows, Linux, macOS):
CodeBrixVideoProcessing.Register();

// Anywhere - including libraries that reference only CodeBrix.VideoProcessing.Core:
IVideoProcessor engine = VideoProcessors.Current;
MediaInfo info = engine.Analyse("input.mov");

var request = new StandardVideoRequest
{
    OutputPath = "output.mp4",
    Width = info.PrimaryVideo.Width,
    Height = info.PrimaryVideo.Height,
    FrameRate = info.PrimaryVideo.FrameRate,
    Quality = VideoQuality.ConstantQuality(20)
};

using IVideoFrameSource source = engine.OpenVideo("input.mov");
using IStandardVideoWriter writer = engine.CreateWriter(request);
while (source.TryReadFrame(out VideoFrameData frame))
{
    writer.WriteFrame(frame);
}
writer.Finish();
```

## Documentation

Each NuGet package includes `AGENT-README.txt`, a complete API reference and usage guide written for AI coding agents - point your agent at that file when it is writing code against the library. In the repository, the CodeBrix.VideoProcessing.Core file lives at `src/CodeBrix.VideoProcessing.Core/AGENT-README.txt`; `README-INDEX.txt` maps them.

Additional sample code and usage examples are available in the `CodeBrix.VideoProcessing.Tests` project:
https://github.com/ellisnet/CodeBrix.VideoProcessing/tree/main/tests/CodeBrix.VideoProcessing.Tests

## License

CodeBrix.VideoProcessing and CodeBrix.VideoProcessing.Core are licensed under the MIT License - see the
[LICENSE](https://github.com/ellisnet/CodeBrix.VideoProcessing/blob/main/LICENSE) file.

For licensing and provenance information about the open source code included in
this package, see [THIRD-PARTY-NOTICES.txt](https://github.com/ellisnet/CodeBrix.VideoProcessing/blob/main/THIRD-PARTY-NOTICES.txt).
