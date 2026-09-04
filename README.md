# CodeBrix.VideoProcessing

A fully managed, cross-platform FFmpeg/FFprobe wrapper library for .NET that makes media analysis and conversion easy.
CodeBrix.VideoProcessing is provided as a .NET 10 library and associated `CodeBrix.VideoProcessing.MitLicenseForever` NuGet package.

CodeBrix.VideoProcessing supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## Installation

```
dotnet add package CodeBrix.VideoProcessing.MitLicenseForever
```

Note that the NuGet package ID and the namespace are different - there is no package named plain `CodeBrix.VideoProcessing`:

* NuGet package ID: `CodeBrix.VideoProcessing.MitLicenseForever`
* Assembly and primary namespace: `CodeBrix.VideoProcessing` - i.e. `using CodeBrix.VideoProcessing;`

XML documentation (IntelliSense) ships alongside the assembly.

The package pulls in one dependency automatically; no version pinning is needed in the consuming project:

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

## Requirements

CodeBrix.VideoProcessing shells out to the `ffmpeg` and `ffprobe` executables, so they must be installed and discoverable at run time - either on the `PATH`, or in a folder configured through `GlobalFFOptions`. The library itself is fully managed C#: it does not bundle the FFmpeg binaries, does not download them, and does not P/Invoke into the FFmpeg libraries.

On Debian-based Linux, `sudo apt install ffmpeg` installs both. On Windows and macOS, install them yourself and either put them on the `PATH` or point `FFOptions.BinaryFolder` at the folder that holds them.

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

## Documentation

The NuGet package includes `AGENT-README.txt`, a complete API reference and usage guide written for AI coding agents - point your agent at that file when it is writing code against this library.

Additional sample code and usage examples are available in the `CodeBrix.VideoProcessing.Tests` project:
https://github.com/ellisnet/CodeBrix.VideoProcessing/tree/main/tests/CodeBrix.VideoProcessing.Tests

## License

CodeBrix.VideoProcessing is licensed under the MIT License - see the
[LICENSE](https://github.com/ellisnet/CodeBrix.VideoProcessing/blob/main/LICENSE) file.

For licensing and provenance information about the open source code included in
this package, see [THIRD-PARTY-NOTICES.txt](https://github.com/ellisnet/CodeBrix.VideoProcessing/blob/main/THIRD-PARTY-NOTICES.txt).
