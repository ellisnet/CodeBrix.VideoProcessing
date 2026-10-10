================================================================================
AGENT-README: CodeBrix.VideoProcessing.Core
A Guide for AI Coding Agents - CONSUMING or IMPLEMENTING the
CodeBrix.VideoProcessing.Core.MitLicenseForever NuGet package
================================================================================

OVERVIEW
========
CodeBrix.VideoProcessing.Core is the ENGINE-NEUTRAL CONTRACT for video
processing: analyse a media file, decode its video frames and audio samples,
write a standard MP4 (H.264 or HEVC video + AAC audio) from frames and sound you
supply, and transcode / trim / join files - all through ONE interface,
IVideoProcessor, that a separate ENGINE package implements per platform.

It also carries the SEAM for standalone video ENCODERS: a second registry,
VideoEncoders, keyed by codec id ("av1", "h264", "hevc"), where an encoder
package registers an IVideoEncoderFactory - frames in, compressed packets out
(see ENCODERS below).

It contains NO engine and NO encoder. It has ZERO package dependencies (not
even an image library): frames, planes, frame rates and media descriptions are
its own small types.

Two kinds of code use this package, and this file covers both:

    1. CONSUMERS - an application or a library (for example a video authoring
       library) that needs video processing and must not care which engine is
       underneath. A consumer references ONLY this package, asks the registry
       for VideoProcessors.Current, and talks to IVideoProcessor.
    2. ENGINE IMPLEMENTERS - a package that implements IVideoProcessor on top
       of some real machinery and registers it. Two engines exist:
         * CodeBrix.VideoProcessing.MitLicenseForever (assembly
           CodeBrix.VideoProcessing) - Windows, Linux, macOS; drives the
           external ffmpeg / ffprobe executables.
           Register with: CodeBrixVideoProcessing.Register();
         * CodeBrix.VideoProcessing.Android - Android; the platform's own
           MediaCodec / MediaExtractor / MediaMuxer.
           Register with: CodeBrixVideoProcessingAndroid.Register();

THE APPLICATION (not the library) picks and registers the engine, once, at
start-up. A library that consumes the contract must not reference an engine
package itself - that would force ffmpeg onto Android apps, or Android code onto
desktop apps.

Target framework: .NET 10 or later. 100% managed C#, no native code.


INSTALLATION
============
PackageId:  CodeBrix.VideoProcessing.Core.MitLicenseForever
Assembly:   CodeBrix.VideoProcessing.Core
License:    MIT

    dotnet add package CodeBrix.VideoProcessing.Core.MitLicenseForever

There is no package named plain "CodeBrix.VideoProcessing.Core"; the suffix
records the license. An application normally does not add this package by hand:
it arrives as a dependency of whichever engine package the application
references.


NAMESPACES
==========
    using CodeBrix.VideoProcessing.Core;          // VideoProcessors, IVideoProcessor,
                                                  // VideoProcessorCapabilities,
                                                  // VideoProcessorExtensions,
                                                  // VideoProgress, the exceptions
    using CodeBrix.VideoProcessing.Core.Media;    // MediaInfo, VideoStreamInfo,
                                                  // AudioStreamInfo, FrameRate,
                                                  // colour / chroma enums
    using CodeBrix.VideoProcessing.Core.Frames;   // VideoFrameData, VideoPlane,
                                                  // VideoPixelLayout,
                                                  // IVideoFrameSource,
                                                  // IAudioSampleSource, options
                                                  // and info types
    using CodeBrix.VideoProcessing.Core.Output;   // StandardVideoRequest,
                                                  // IStandardVideoWriter,
                                                  // VideoQuality, profiles,
                                                  // Transcode/Trim/Concatenate
                                                  // requests
    using CodeBrix.VideoProcessing.Core.Encoding; // VideoEncoders,
                                                  // IVideoEncoderFactory,
                                                  // IVideoEncoder,
                                                  // VideoEncoderRequest,
                                                  // EncodedVideoPacket,
                                                  // VideoCodecIds, encoder
                                                  // capabilities + exceptions
Inside a namespace under CodeBrix.VideoProcessing.Core, the bare name
"Encoding" means that namespace - write System.Text.Encoding in full there.


QUICK START - APPLICATION
=========================
    // Program start-up, desktop (Windows / Linux / macOS):
    CodeBrixVideoProcessing.Register();         // from CodeBrix.VideoProcessing

    // Program start-up, Android:
    CodeBrixVideoProcessingAndroid.Register();  // from CodeBrix.VideoProcessing.Android

Register is idempotent; calling it twice is harmless.


QUICK START - CONSUMER CODE
===========================
    using CodeBrix.VideoProcessing.Core;
    using CodeBrix.VideoProcessing.Core.Frames;
    using CodeBrix.VideoProcessing.Core.Media;
    using CodeBrix.VideoProcessing.Core.Output;

    IVideoProcessor engine = VideoProcessors.Current;  // throws if none registered

    MediaInfo info = engine.Analyse("in.mov");
    VideoStreamInfo video = info.PrimaryVideo;         // null when no video

    var request = new StandardVideoRequest
    {
        OutputPath = "out.mp4",
        Width = video.Width,
        Height = video.Height,
        FrameRate = video.FrameRate,
        Quality = VideoQuality.ConstantQuality(20),
        KeyframeIntervalFrames = 60,
        HasAudio = false
    };

    using (IVideoFrameSource source = engine.OpenVideo("in.mov"))
    using (IStandardVideoWriter writer = engine.CreateWriter(request))
    {
        while (source.TryReadFrame(out VideoFrameData frame))
        {
            // ... inspect or modify a COPY of the frame here ...
            writer.WriteFrame(frame);
        }

        writer.Finish();   // without Finish, Dispose deletes the output
    }

Ask before using an optional feature:

    if (engine.Supports(VideoProcessorCapabilities.DirectTrim))
        engine.TrimFile(new TrimRequest { InputPath = "in.mp4", OutputPath = "cut.mp4",
                                          From = TimeSpan.FromSeconds(2),
                                          To = TimeSpan.FromSeconds(5),
                                          SnapToKeyframes = true });


THE REGISTRY - VideoProcessors (static, thread-safe)
====================================================
    void Register(IVideoProcessor processor)
        Adds an engine. Idempotent on the INSTANCE (registering the same object
        twice registers it once and does not move it). Null throws
        ArgumentNullException.
    IVideoProcessor Current { get; }
        The engine registered most recently. Throws
        VideoProcessorNotRegisteredException when none is registered; its
        message names both engine packages and their Register calls - surface
        it to the developer as-is.
    bool IsRegistered { get; }
    bool TryGetCurrent(out IVideoProcessor processor)
    IReadOnlyList<IVideoProcessor> RegisteredProcessors { get; }
        Oldest registration first.

There is deliberately NO public Unregister: an engine registers for the life of
the process.


IVideoProcessor - THE CONTRACT
==============================
    string Name                                   e.g. "ffmpeg"
    VideoProcessorCapabilities Capabilities       optional features, see below
    MediaInfo Analyse(string path, CancellationToken ct = default)
    Task<MediaInfo> AnalyseAsync(string path, CancellationToken ct = default)
    VideoFrameData Snapshot(string path, TimeSpan at, CancellationToken ct = default)
    IVideoFrameSource OpenVideo(string path, VideoSourceOptions options = null)
    IAudioSampleSource OpenAudio(string path, AudioSourceOptions options = null)
    IStandardVideoWriter CreateWriter(StandardVideoRequest request,
                                      IProgress<VideoProgress> progress = null)
    void TranscodeFile(TranscodeRequest request,
                       IProgress<VideoProgress> progress = null,
                       CancellationToken ct = default)
    void TrimFile(TrimRequest request, IProgress<VideoProgress> progress = null,
                  CancellationToken ct = default)
    void ConcatenateFiles(ConcatenateRequest request,
                          IProgress<VideoProgress> progress = null,
                          CancellationToken ct = default)

MANDATORY CORE (every engine): Analyse/AnalyseAsync; OpenVideo delivering 8-bit
I420; OpenAudio delivering interleaved float; CreateWriter for Mp4H264Aac fed
with 8-bit I420 frames.

OPTIONAL - VideoProcessorCapabilities [Flags]:
    None                 only the mandatory core
    AlphaFrameSource     OpenVideo can deliver an alpha plane (frame.A)
    Nv12Input            CreateWriter accepts / OpenVideo can deliver NV12
    RgbaInput            CreateWriter accepts / OpenVideo can deliver RGBA
    HevcOutput           Mp4HevcAac output
    DirectTranscode      TranscodeFile
    DirectTrim           TrimFile
    DirectConcatenate    ConcatenateFiles
    Snapshot             Snapshot
    I444FrameSource      OpenVideo can deliver I444 frames
    GrayFrameSource      OpenVideo can deliver Gray (luma only) frames
    HighBitDepthFrameSource  OpenVideo can deliver 10- and 12-bit YUV / Gray
                         frames (VideoSourceOptions.BitDepth)
The last three are FRAME-SOURCE capabilities only: the standard MP4 writer takes
8-bit I420 / NV12 / RGBA and nothing else, whatever the engine. An engine with
a flag may still refuse one combination it cannot produce exactly (for example
12-bit I420 with alpha) with a NotSupportedException that names it.
An operation whose flag is missing throws NotSupportedException.

    bool engine.Supports(VideoProcessorCapabilities flags)        // ALL flags set?
    void engine.RequireCapability(flags, "what was attempted")    // throws the
                                                                  // standard
                                                                  // NotSupported

ERRORS - the same on every engine:
    ArgumentException family     bad arguments / failed request validation
    FileNotFoundException        the input file does not exist
    MediaFormatException         the input is not readable media, or lacks the
                                 stream asked for (e.g. OpenAudio on a silent
                                 file)
    VideoProcessingException     any other engine failure; the engine's own
                                 exception is the InnerException
    NotSupportedException        missing capability
    OperationCanceledException   cancelled
MediaFormatException and VideoProcessorNotRegisteredException both derive from
VideoProcessingException, so catching VideoProcessingException catches all
engine failures.

THREADING: an engine instance may be used from several threads at once. The
sources and writers it returns are single-threaded objects.


MEDIA DESCRIPTION - CodeBrix.VideoProcessing.Core.Media
=======================================================
MediaInfo (sealed, init-only properties; collections never null):
    string ContainerFormat        engine's words, may be null
    TimeSpan Duration
    IReadOnlyList<VideoStreamInfo> VideoStreams
    IReadOnlyList<AudioStreamInfo> AudioStreams
    IReadOnlyDictionary<string,string> Tags      case-insensitive
    VideoStreamInfo PrimaryVideo / AudioStreamInfo PrimaryAudio   (first, or null)
    bool HasVideo / HasAudio

VideoStreamInfo:
    int Index; string CodecName; int Width, Height (STORED size, not rotated);
    FrameRate FrameRate; long FrameCount (0 = unknown); TimeSpan Duration;
    int BitDepth (0 = unknown); string PixelFormatName (e.g. "yuv420p10le");
    VideoChromaSubsampling ChromaSubsampling; bool HasAlpha;
    int RotationDegrees  - CLOCKWISE display rotation, 0/90/180/270;
    VideoColorRange ColorRange; VideoColorMatrix ColorMatrix;
    VideoColorPrimaries ColorPrimaries; VideoColorTransfer ColorTransfer;
    Tags; int DisplayWidth / DisplayHeight (size after rotation).

AudioStreamInfo: Index, CodecName, SampleRate, Channels, BitDepth (0 when not
meaningful, e.g. AAC), Duration, Tags.

Unknown values are 0 / null / Unspecified - never guessed.

FrameRate (readonly struct) - an EXACT rational, stored reduced:
    new FrameRate(30000, 1001)      // throws on 0 or negative parts
    FrameRate.Unspecified / default // 0/0, IsSpecified == false
    int Numerator, Denominator; double FramesPerSecond; TimeSpan FrameDuration
    TimeSpan TimestampOf(long frameIndex)   // rounded UP to a whole tick
    long FrameIndexAt(TimeSpan time)        // frame on screen at that time
    long FrameCountFor(TimeSpan duration)   // partial frames count
    FrameRate.Parse("30000/1001") / Parse("25") / TryParse(...)
    ToString() -> "30000/1001" (round-trips through Parse)
FrameIndexAt(TimestampOf(i)) == i for every i - derive timestamps from frame
indexes with these two methods and drift never accumulates.

Enums: VideoColorRange {Unspecified, Limited, Full};
VideoColorMatrix {Unspecified, Bt601, Bt709, Bt2020NonConstant, Bt2020Constant,
Rgb, Other}; VideoColorPrimaries {Unspecified, Bt709, Bt601Pal, Bt601Ntsc,
Bt2020, DciP3, DisplayP3, Other}; VideoColorTransfer {Unspecified, Bt709, Srgb,
Linear, Pq, Hlg, Other}; VideoChromaSubsampling {Unknown, Yuv420, Yuv422,
Yuv444, Monochrome, Rgb, Other}.


FRAMES - CodeBrix.VideoProcessing.Core.Frames
=============================================
VideoPixelLayout: I420 (= 0, mandatory), Nv12 (1), Rgba (2), I444 (3), Gray (4).
    I420  planar 4:2:0: Y full size; U, V half width and height (rounded up)
    Nv12  Y full size; ONE interleaved Cb Cr plane at half height
    Rgba  ONE packed plane, R G B A, straight alpha; ALWAYS 8 bits
    I444  planar 4:4:4: Y, U, V all full size
    Gray  luma only: ONE Y plane (a monochrome picture, or an alpha channel
          carried as luma)

BIT DEPTH: YUV and Gray frames are 8, 10 or 12 bits per sample; RGBA is 8.
An 8-bit sample is ONE byte. A 10- or 12-bit sample is TWO bytes: unsigned
16-bit LITTLE-ENDIAN, value in the LOW bits (0..1023 / 0..4095) - the libaom /
dav1d convention, NOT P010's high-bit alignment. Every plane size (RowLength,
Stride) is in BYTES, so a 10-bit plane's RowLength is 2 x its width.

VideoPlane (readonly struct): ReadOnlyMemory<byte> Data; int Stride (bytes
between row starts); int RowLength (meaningful BYTES per row - 4 per pixel for
RGBA, 2 per sample above 8 bits); int Rows; bool IsEmpty (default value = "no
such plane"); ReadOnlySpan<byte> GetRow(int row); byte[] ToArray() (tightly
packed); CopyTo(Span<byte>); VideoPlane.FromArray(byte[] data, rowLength, rows).
The last row need not be padded. Read a deep sample with
BinaryPrimitives.ReadUInt16LittleEndian(plane.GetRow(r).Slice(2 * x, 2)).

VideoFrameData (readonly struct) - created ONLY through factories that check
every plane size exactly for the layout AND the depth (b = bytes per sample,
1 at 8 bits, 2 at 10/12):
    VideoFrameData.CreateI420(width, height, y, u, v, timestamp, frameIndex,
                              a = default, bitDepth = 8)
        y: (width*b) bytes x height; u, v: (((width+1)/2)*b) x ((height+1)/2)
    VideoFrameData.CreateNv12(width, height, y, uv, timestamp, frameIndex,
                              a = default, bitDepth = 8)
        uv: (2*((width+1)/2)*b) bytes x ((height+1)/2) rows, Cb Cr interleaved
    VideoFrameData.CreateI444(width, height, y, u, v, timestamp, frameIndex,
                              a = default, bitDepth = 8)
        y, u, v: (width*b) bytes x height rows
    VideoFrameData.CreateGray(width, height, y, timestamp, frameIndex,
                              a = default, bitDepth = 8)
        y: (width*b) bytes x height rows
    VideoFrameData.CreateRgba(width, height, rgba, timestamp, frameIndex)
        rgba: (4*width) bytes x height rows, R G B A, STRAIGHT alpha
A bit depth other than 8, 10 or 12 throws ArgumentOutOfRangeException; a plane
of the wrong size throws ArgumentException naming the plane.
Properties: Width, Height, Layout, BitDepth (8/10/12; 0 for the empty frame),
BytesPerSample (1 or 2; 0 for the empty frame), Timestamp, FrameIndex, IsEmpty,
Y (I420, NV12, I444, Gray), U and V (I420, I444), UV (NV12), Rgba (RGBA), A,
HasAlpha. A plane the layout does not have is the empty plane.
A (alpha, YUV and Gray layouts) is luma-sized, at the SAME bit depth as the
frame, FULL range (0 = transparent, 255 / 1023 / 4095 = opaque), straight (not
premultiplied).
    VideoFrameData Copy()                      deep, owned, tightly packed copy
                                               (same layout, depth, position)
    VideoFrameData WithPosition(ts, index)     same planes, new position

OWNERSHIP - READ THIS:
    * A frame from IVideoFrameSource.TryReadFrame is valid ONLY until the next
      TryReadFrame, Seek or Dispose on that source - engines reuse buffers.
      Call frame.Copy() to keep it.
    * A frame passed to IStandardVideoWriter.WriteFrame or IVideoEncoder.Encode
      may be reused by the caller as soon as that call returns.
    * Snapshot returns an owned frame.

IVideoFrameSource : IDisposable
    VideoSourceInfo Info
    bool TryReadFrame(out VideoFrameData frame)   false (and default frame) at end
    bool CanSeek
    void Seek(TimeSpan position)   next frame = first starting at/after position
Frames come in presentation order at a CONSTANT rate:
frame.Timestamp == Info.FrameRate.TimestampOf(frame.FrameIndex), and FrameIndex
counts from the START of the stream (also after a Seek or a StartTime).
Frames are in STORED orientation; Info.RotationDegrees says how to display them.

VideoSourceOptions: PixelLayout (I420 default; I444 needs I444FrameSource, Gray
needs GrayFrameSource, NV12 / RGBA need Nv12Input / RgbaInput), BitDepth (8
default; 10 or 12 need HighBitDepthFrameSource; never above 8 with RGBA),
IncludeAlpha (false default; needs AlphaFrameSource; the A plane comes at the
frame's depth), StartTime (zero default); Validate() (ArgumentOutOfRange for an
unknown layout, a depth other than 8/10/12, deep RGBA, or a negative start).

VideoSourceInfo: Width, Height (stored), FrameRate (constant), Duration,
EstimatedFrameCount, PixelLayout, BitDepth (as asked: 8, 10 or 12 - equal to
every delivered frame's BitDepth), SourceBitDepth, IsBitDepthReduced (e.g.
10-bit HDR delivered as 8-bit), SourceHasAlpha, HasAlpha (alpha actually
delivered), RotationDegrees (clockwise), ColorRange, ColorMatrix (of the
delivered samples).

IAudioSampleSource : IDisposable
    AudioSourceInfo Info           SampleRate, Channels, Duration, SourceCodecName
    int Read(Span<float> interleaved)   returns FRAMES (floats / channels); 0 at
                                        end; length must be a multiple of
                                        Channels; blocks until full or end
    bool CanSeek; void Seek(TimeSpan)
AudioSourceOptions: SampleRate (0 = keep), Channels (0 = keep), StartTime;
Validate().


WRITING - CodeBrix.VideoProcessing.Core.Output
==============================================
StandardVideoRequest (mutable settings object; Validate(); Clone()):
    OutputPath                       required; overwritten if present
    Profile                          Mp4H264Aac (default) | Mp4HevcAac
    Width, Height                    positive, EVEN (4:2:0)
    FrameRate                        required, CONSTANT
    InputPixelLayout                 I420 (default) | Nv12 | Rgba - layout of the
                                     frames you will pass; I444 and Gray fail
                                     Validate(); frames must be 8-bit
    Quality                          VideoQuality (default = engine default)
    KeyframeIntervalFrames           0 = engine default
    ColorRange (Limited), ColorMatrix (Bt709)    TAGS for your YUV samples; the
                                     engine converts nothing for YUV input
    RotationDegrees                  0/90/180/270 clockwise display METADATA
    FastStart                        true (default): MP4 index before the data
    HasAudio                         false (default)
    AudioSampleRate (48000), AudioChannels (2, 1..8),
    AudioBitrateKilobitsPerSecond (128)
    Title, CreationTime              optional tags

VideoQuality (readonly struct) - ONE struct with a mode:
    VideoQuality.ConstantQuality(int level)    0..51, lower = better (CRF-style;
                                               18 ~ visually lossless, 23 typical)
    VideoQuality.TargetBitrate(int kbps)
    VideoQuality.EngineDefault / default
    Mode (EngineDefault | ConstantQuality | TargetBitrate), QualityLevel,
    BitrateKilobitsPerSecond
Platform engines without a true CRF encoder map the level as best they can.

IStandardVideoWriter : IDisposable
    StandardVideoRequest Request; long FramesWritten; long AudioFramesWritten
    void WriteFrame(in VideoFrameData frame)
        in order; size and layout must equal the request and BitDepth must be 8
        (ArgumentException);
        the ENGINE stamps frame N at Request.FrameRate.TimestampOf(N) - the
        frame's own Timestamp / FrameIndex / alpha plane are ignored.
    void WriteAudio(ReadOnlySpan<float> interleaved)
        only when HasAudio; length a multiple of AudioChannels.
    void Finish()
        flush + finalise; required. Dispose WITHOUT Finish deletes the output.
Feed sound and pictures roughly in step (e.g. one frame's worth of samples per
frame); engines buffer the gap, and memory grows with it.
Progress (IProgress<VideoProgress>, given to CreateWriter) reports frames
written; Fraction is null because the total is not known.

TranscodeRequest: InputPath; Output (a StandardVideoRequest where "keep the
source's value" is allowed: Width AND Height 0, FrameRate unspecified,
AudioSampleRate 0, AudioChannels 0; HasAudio = keep the source's sound). The
default Output keeps everything and keeps audio. Pictures keep their stored
orientation and the source's rotation metadata is carried over; a non-zero
Output.RotationDegrees replaces it.

TrimRequest: InputPath, OutputPath, From, To (exclusive, > From),
SnapToKeyframes (false default = frame-accurate re-encode to H.264/AAC; true =
stream copy, fast and lossless, starting at the keyframe at or before From).

ConcatenateRequest: InputPaths (in order), OutputPath. Stream COPY - every
input must share codecs and parameters.

VideoProgress (readonly struct): double? Fraction (0..1 or null), long Frames,
TimeSpan Position (media time), TimeSpan Elapsed.


IMPLEMENTING AN ENGINE
======================
    1. Implement IVideoProcessor (Name, Capabilities, every member). Members
       for capabilities you do not declare call
       this.RequireCapability(flag, nameof(Member)) first.
    2. Validate every request with its Validate() method before doing work.
    3. Build MediaInfo / VideoSourceInfo with object initializers; leave
       unknown values at 0 / null / Unspecified. Report RotationDegrees
       CLOCKWISE, normalised to 0/90/180/270.
    4. Frame sources: deliver frames in stored orientation, at a constant rate,
       with Timestamp = Info.FrameRate.TimestampOf(FrameIndex). Reusing
       buffers between TryReadFrame calls is allowed (and expected).
       Deliver VideoSourceOptions.BitDepth (converting the source up or down)
       and report the source's own depth through SourceBitDepth. Deep samples
       are 16-bit little-endian, value in the low bits. A layout / depth /
       alpha combination you cannot produce EXACTLY is refused with
       NotSupportedException - never approximated.
    5. Writers: copy what you need out of the frame before WriteFrame returns;
       assign timestamps from the index; delete the output when disposed
       without Finish.
    6. Wrap engine failures in VideoProcessingException (or
       MediaFormatException for unreadable inputs), keeping the original as
       InnerException.
    7. Offer a static class named after the package with an idempotent,
       locked Register() that calls VideoProcessors.Register(instance) with ONE
       shared instance (see CodeBrixVideoProcessing.Register()).

ENCODERS - CodeBrix.VideoProcessing.Core.Encoding
================================================
A separate, smaller seam: an ENCODER turns frames into a codec's compressed
packets (one elementary stream). It knows nothing about containers, alpha
streams, RGBA conversion or any file format - frames in, packets out. Encoders
come from encoder PACKAGES; the AV1 one is CodeBrix.VideoEncoding.Av1. The
application registers them once at start-up, exactly like an engine:

    CodeBrixVideoEncodingAv1.Register();     // from CodeBrix.VideoEncoding.Av1

The encoder registry is independent of VideoProcessors: an encoder needs no
engine, and the ffmpeg / Android engines register no encoders.

VideoCodecIds (static): Av1 = "av1", H264 = "h264", Hevc = "hevc" - lower case;
the registry compares ids ignoring case.

VideoEncoders (static, thread-safe):
    void Register(IVideoEncoderFactory factory)
        Idempotent on the INSTANCE (a second Register of the same object does
        nothing and does not move it). For the SAME codec id, the factory
        registered most recently wins. Null throws ArgumentNullException; a
        blank CodecId throws ArgumentException.
    bool IsRegistered(string codecId)
    bool TryGet(string codecId, out IVideoEncoderFactory factory)
    IVideoEncoderFactory Get(string codecId)
        Throws VideoEncoderNotRegisteredException (a VideoProcessingException;
        .CodecId says which) whose message names the codec id - and for "av1"
        the package CodeBrix.VideoEncoding.Av1 and
        CodeBrixVideoEncodingAv1.Register(). Surface it as-is.
    IReadOnlyList<IVideoEncoderFactory> RegisteredFactories   oldest first
There is deliberately NO public Unregister.

IVideoEncoderFactory:
    string CodecId                          e.g. "av1"
    string DisplayName                      for logs
    VideoEncoderCapabilities Capabilities
    IVideoEncoder Create(VideoEncoderRequest request)
        validates the request, checks it against Capabilities
        (NotSupportedException), keeps its OWN copy of it.

VideoEncoderCapabilities (init-only):
    VideoEncoderPixelLayouts PixelLayouts   [Flags] I420 | Nv12 | Rgba | I444 | Gray
    VideoEncoderBitDepths BitDepths         [Flags] Depth8 | Depth10 | Depth12
    bool SupportsLossless; bool SupportsForcedKeyFrames
    bool SupportsLayout(VideoPixelLayout); bool SupportsBitDepth(int)
    void ThrowIfUnsupported(VideoEncoderRequest)    the standard refusal
    static ToFlag(VideoPixelLayout) / ToFlag(int bitDepth)

VideoEncoderRequest (mutable settings object; Validate(); Clone()):
    Width, Height                  positive (odd sizes allowed; the codec
                                   decides what it accepts)
    PixelLayout                    I420 (default) | Nv12 | Rgba | I444 | Gray
    BitDepth                       8 (default) | 10 | 12; RGBA 8 only
    FrameRate                      required, CONSTANT
    ColorPrimaries, ColorTransfer, ColorMatrix (Bt709 defaults), ColorRange
                                   (Limited default) - TAGS, nothing converted;
                                   Unspecified leaves a field untagged
    Quality                        VideoQuality (EngineDefault default); the
                                   0..51 scale is mapped by each encoder
    Lossless                       needs SupportsLossless; Quality must then
                                   stay EngineDefault
    Threads                        0 = encoder default
    MaxKeyFrameInterval            frames; 0 = encoder default
    Speed                          CODEC-SPECIFIC speed / effort preset (e.g.
                                   libaom cpu-used); 0 = encoder default
    IDictionary<string,string> CodecOptions   codec-specific knobs passed
                                   through untouched; never null; ordinal keys;
                                   the encoder rejects names it does not know
Validate(): ArgumentException family naming the bad property.

IVideoEncoder : IDisposable (single-threaded object):
    VideoEncoderRequest Request              the encoder's own copy
    ReadOnlyMemory<byte> CodecConfigurationRecord
        av1C (AV1CodecConfigurationRecord with the sequence header OBU) for
        AV1, avcC for H.264, hvcC for HEVC. EMPTY until the encoder has
        produced its sequence header; guaranteed available once TryReadPacket
        has returned the first packet.
    void Encode(in VideoFrameData frame, bool forceKeyFrame)
        frames in presentation order; size, layout and BitDepth must equal the
        request (ArgumentException); the caller may reuse the frame at once;
        an A plane is ignored. forceKeyFrame needs SupportsForcedKeyFrames
        (else NotSupportedException). After Flush: InvalidOperationException.
    void Flush()                             no more frames; idempotent
    bool TryReadPacket(out EncodedVideoPacket packet)
        PULL MODEL: call after EVERY Encode and after Flush, until false.
        False before Flush = "nothing more for now"; after Flush = "done".
    long FramesSubmitted; long PacketsProduced
Failures of the underlying encoder: VideoEncodingException (a
VideoProcessingException; the native failure is the InnerException).

EncodedVideoPacket (readonly struct):
    ReadOnlyMemory<byte> Data   a FRESH buffer the consumer owns
    TimeSpan Timestamp          Request.FrameRate.TimestampOf(FrameIndex)
    long FrameIndex             the Nth frame given to Encode is frame N (the
                                frame's own Timestamp / FrameIndex are ignored)
    bool IsKeyFrame; bool IsEmpty (default value)
Packets come out in DECODING order; for AV1 each packet is one temporal unit
and decoding order equals presentation order.

The loop:

    using CodeBrix.VideoProcessing.Core.Encoding;

    var request = new VideoEncoderRequest
    {
        Width = 1920, Height = 1080,
        PixelLayout = VideoPixelLayout.I420, BitDepth = 10,
        FrameRate = new FrameRate(30, 1),
        Quality = VideoQuality.ConstantQuality(24),
        MaxKeyFrameInterval = 120
    };

    IVideoEncoderFactory av1 = VideoEncoders.Get(VideoCodecIds.Av1);
    using (IVideoEncoder encoder = av1.Create(request))
    {
        foreach (VideoFrameData frame in frames)        // 10-bit I420 frames
        {
            encoder.Encode(frame, forceKeyFrame: false);
            while (encoder.TryReadPacket(out EncodedVideoPacket packet))
                Store(packet);                          // your container code
        }

        encoder.Flush();
        while (encoder.TryReadPacket(out EncodedVideoPacket packet))
            Store(packet);

        ReadOnlyMemory<byte> av1C = encoder.CodecConfigurationRecord;
    }

ALPHA is not the encoder's business: to keep transparency, encode it as a
SECOND stream - a Gray encoder (ColorRange = Full, same size, rate, depth and
MaxKeyFrameInterval) fed with
VideoFrameData.CreateGray(w, h, frame.A, ts, index, default, frame.BitDepth)
for every colour frame, in lock step.


IMPLEMENTING AN ENCODER PACKAGE
===============================
    1. Implement IVideoEncoderFactory (one shared, thread-safe instance per
       codec) and IVideoEncoder.
    2. In Create: request.Validate(); Capabilities.ThrowIfUnsupported(request);
       reject unknown CodecOptions names / bad values and out-of-range Speed
       with ArgumentException; keep request.Clone().
    3. Encode: check the frame (size, layout, BitDepth); copy what you keep
       before returning; number frames yourself (0, 1, 2, ...) and stamp
       packets with Request.FrameRate.TimestampOf(index).
    4. Hand out every packet as a FRESH byte buffer; set
       CodecConfigurationRecord no later than the first packet.
    5. Wrap native failures in VideoEncodingException with the original as
       InnerException.
    6. Offer a static class named after the package with an idempotent,
       locked Register() that calls VideoEncoders.Register(factory) with ONE
       shared instance (for AV1: CodeBrixVideoEncodingAv1.Register()).
    7. Know nothing about containers, alpha streams or RGBA conversion.


COMMON PITFALLS
===============
    * Calling VideoProcessors.Current in a library's static constructor or
      field initialiser - the application may not have registered yet. Ask at
      the moment of use.
    * Keeping a VideoFrameData from TryReadFrame without Copy() - the next read
      overwrites it.
    * Passing frames whose size differs from StandardVideoRequest.Width/Height,
      or odd sizes - 4:2:0 output needs even dimensions.
    * Expecting the writer to honour frame timestamps - it uses index x rate.
      Variable frame rate is out of scope.
    * Forgetting Finish() - Dispose alone throws the output away.
    * Assuming an optional capability - check Supports(...) first.
    * Treating RotationDegrees as counter-clockwise - it is CLOCKWISE.
    * Reading a 10/12-bit plane as bytes - samples are 16-bit little-endian
      and RowLength is in BYTES (2 x width).
    * Feeding I444, Gray or 10/12-bit frames to the standard MP4 writer - it
      takes 8-bit I420 / NV12 / RGBA only; use an encoder package instead.
    * Calling TryReadPacket only at the end - drain it after EVERY Encode, and
      again after Flush; skipping Flush loses the frames held back.
    * Expecting an encoder to split alpha off a frame - encode A as its own
      Gray stream.

================================================================================
END OF AGENT-README
================================================================================
