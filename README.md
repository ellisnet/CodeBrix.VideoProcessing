# CodeBrix.VideoProcessing

A fully managed, cross-platform FFmpeg/FFprobe wrapper library for .NET that makes media analysis and conversion easy.
CodeBrix.VideoProcessing is a port of the popular [FFMpegCore](https://github.com/rosenbjerg/FFMpegCore) library (with the [Instances](https://github.com/rosenbjerg/Instances) process wrapper vendored directly in), exposing the same functionality under the `CodeBrix.VideoProcessing` namespace.
CodeBrix.VideoProcessing's only NuGet dependency is `CodeBrix.Imaging` (itself a fully managed, cross-platform, dependency-free CodeBrix library), used by the in-memory image bridge; it is provided as a .NET 10 library and associated `CodeBrix.VideoProcessing.MitLicenseForever` NuGet package.

CodeBrix.VideoProcessing supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

> **Runtime prerequisite:** like FFMpegCore, this library shells out to the `ffmpeg` and `ffprobe` executables — they must be installed and discoverable (on the `PATH`, or configured via `GlobalFFOptions`). The library itself is fully managed; it does not bundle the FFmpeg binaries.

## CodeBrix.VideoProcessing supports:

* Analysing media files and streams with FFprobe (`FFProbe.Analyse` / `AnalyseAsync`) — duration, streams, codecs, resolution, bitrate, and more
* Converting, transcoding and muxing video and audio (`FFMpeg` / `FFMpegArguments`)
* A fluent argument builder covering scaling, cropping, seeking, bitrate, codecs, filters, hardware acceleration, concatenation, and dozens of other options
* Extracting single-frame snapshots and animated GIF snapshots
* Piping raw audio/video frames into and out of FFmpeg via `IPipeSource` / `IPipeSink`
* Building and serialising FFmpeg metadata (`MetaDataBuilder`)
* A built-in, elegant process wrapper (the vendored `CodeBrix.VideoProcessing.Instances` namespace)
* An in-memory image bridge under `CodeBrix.VideoProcessing.Imaging` (built on CodeBrix.Imaging): grab a snapshot as a `CodeBrix.Imaging` image (`FFMpegImage.Snapshot`), feed images into FFmpeg as frames (`ImageVideoFrameWrapper`), or mux an image + audio into a video (`ImageExtensions.AddAudio`)

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

### Save a thumbnail

```csharp
using System.Drawing;
using CodeBrix.VideoProcessing;

FFMpeg.Snapshot("input.mp4", "thumbnail.png", new Size(1920, 1080), TimeSpan.FromSeconds(10));
```

## License

The project is licensed under the MIT License. see: https://en.wikipedia.org/wiki/MIT_License

CodeBrix.VideoProcessing incorporates source code from the FFMpegCore and Instances projects, both of which are licensed under the MIT License. See `THIRD-PARTY-NOTICES.txt` for full attribution and license texts.
