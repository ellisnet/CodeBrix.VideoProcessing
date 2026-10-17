================================================================================
AGENT-README: CodeBrix.VideoProcessing.Android
A Guide for AI Coding Agents - CONSUMING the
CodeBrix.VideoProcessing.Android.MitLicenseForever NuGet package
================================================================================

OVERVIEW
========
CodeBrix.VideoProcessing.Android is the ANDROID ENGINE for the engine-neutral
CodeBrix.VideoProcessing.Core contract. It implements IVideoProcessor in full -
Analyse, Snapshot, OpenVideo, OpenAudio, CreateWriter (standard MP4),
TranscodeFile, TrimFile and ConcatenateFiles - on the platform's own
MediaExtractor, MediaCodec, MediaMuxer and MediaMetadataRetriever.

    * No native code of its own, no JNI beyond the .NET for Android bindings,
      no ffmpeg. The package carries one managed assembly.
    * One dependency: CodeBrix.VideoProcessing.Core.MitLicenseForever (the
      contract), at the same version.
    * Code written against CodeBrix.VideoProcessing.Core works unchanged: the
      application registers this engine once at start-up, and every library
      that asks VideoProcessors.Current gets it.

For the contract itself (MediaInfo, VideoFrameData, StandardVideoRequest, the
capability flags, the ownership rules, the exceptions) read the AGENT-README of
CodeBrix.VideoProcessing.Core - it ships inside that package. This file covers
what is particular to the Android engine.

Target: .NET 10 Android applications (net10.0-android), API level 33
(Android 13) and newer. Works on every ABI .NET for Android runs on (there are
no native libraries to match).


INSTALLATION
============
PackageId:  CodeBrix.VideoProcessing.Android.MitLicenseForever
Assembly:   CodeBrix.VideoProcessing.Android
License:    MIT

    dotnet add package CodeBrix.VideoProcessing.Android.MitLicenseForever

There is no package named plain "CodeBrix.VideoProcessing.Android"; the suffix
records the license. Reference it from the Android APPLICATION project only - a
library that consumes the contract references CodeBrix.VideoProcessing.Core and
nothing else.

On Windows, Linux and macOS the engine is the ffmpeg-based
CodeBrix.VideoProcessing package instead (CodeBrixVideoProcessing.Register());
it refuses to run on Android.


NAMESPACES
==========
    using CodeBrix.VideoProcessing.Android;          // CodeBrixVideoProcessingAndroid
    using CodeBrix.VideoProcessing.Android.Engine;   // AndroidVideoProcessor
    using CodeBrix.VideoProcessing.Core;             // VideoProcessors, IVideoProcessor, ...
    using CodeBrix.VideoProcessing.Core.Media;       // MediaInfo, FrameRate, ...
    using CodeBrix.VideoProcessing.Core.Frames;      // VideoFrameData, sources, options
    using CodeBrix.VideoProcessing.Core.Output;      // StandardVideoRequest, writers, requests

Inside a namespace that starts with CodeBrix.VideoProcessing.Android, the bare
name "Android" means THAT namespace - write global::Android.App.Activity (or
put the usings at the top of the file, outside the namespace) for the platform
types.


QUICK START
===========
    // Once, at application start-up (Application.OnCreate or the first
    // Activity's OnCreate):
    CodeBrixVideoProcessingAndroid.Register();

    // Anywhere - including libraries that reference only the Core package.
    // Run long operations OFF the UI thread (see THREADING).
    IVideoProcessor engine = VideoProcessors.Current;
    MediaInfo info = engine.Analyse(path);

    var request = new StandardVideoRequest
    {
        OutputPath = Path.Combine(FilesDir.AbsolutePath, "out.mp4"),
        Width = 1280, Height = 720,
        FrameRate = new FrameRate(30, 1),
        Quality = VideoQuality.ConstantQuality(23),
        HasAudio = true, AudioSampleRate = 48000, AudioChannels = 2
    };

    using (IStandardVideoWriter writer = engine.CreateWriter(request))
    {
        foreach (VideoFrameData frame in frames)      // 8-bit I420, NV12 or RGBA
        {
            writer.WriteFrame(frame);
            writer.WriteAudio(oneFrameOfSound);       // interleaved float
        }

        writer.Finish();                              // without it the file is deleted
    }

Paths are ordinary file-system paths the app can open (FilesDir, CacheDir,
GetExternalFilesDir(null), ...). content:// URIs are not accepted - copy the
content to a file first.


REGISTRATION
============
CodeBrixVideoProcessingAndroid (static):
    void Register()
        Adds the ONE shared engine instance to VideoProcessors. Idempotent and
        thread-safe; the first call reads the device's codec list once.
    bool IsRegistered { get; }
    AndroidVideoProcessor Processor { get; }
        The single instance (created on first use, registered or not).

AndroidVideoProcessor : IVideoProcessor (sealed):
    const string EngineName = "android-mediacodec"
    string Name                       "android-mediacodec"
    VideoProcessorCapabilities Capabilities     computed once, see below
    bool HasHevcEncoder               the device has a "video/hevc" encoder
    IReadOnlyList<string> HighBitDepthDecoders
                                      the video decoders that list P010
                                      (10-bit) output, as "name (mime)";
                                      not empty exactly when
                                      HighBitDepthFrameSource is set
    (plus every IVideoProcessor member)

Create an AndroidVideoProcessor yourself only if you need an instance that is
not the registered one; there are no options to give it.


CAPABILITIES
============
    Flag                     Set?    Why
    -----------------------  ------  -------------------------------------------
    RgbaInput                always  RGBA frames are converted to / from I420 in
                                     managed code (writer input and OpenVideo
                                     output).
    Nv12Input                always  NV12 <-> I420 is a managed (de)interleave.
    GrayFrameSource          always  Gray = the Y plane of the decoded 4:2:0
                                     picture, exactly I420's luma.
    Snapshot                 always  Built on the engine's own frame source.
    DirectTranscode          always  Decode + re-encode through the engine.
    DirectTrim               always  Sample copy, or decode + re-encode.
    DirectConcatenate        always  Sample copy, or decode + re-encode.
    HevcOutput               when    Set only when MediaCodecList has a
                             device  "video/hevc" ENCODER (most current phones
                             has one do; check before asking for Mp4HevcAac).
    AlphaFrameSource         NEVER   Android's video decoders expose no alpha
                                     plane. (See ALPHA AND MODE3 below.)
    I444FrameSource          NEVER   The platform decoders deliver 4:2:0 only.
    ExternalTools            NEVER   An Android app has no external command-line
                                     tools to run; the engine works through the
                                     platform's media APIs and is not an
                                     IExternalToolHost, so TryGetExternalTools
                                     is false. (Desktop consumers that run
                                     ffmpeg lines get them from the ffmpeg
                                     engine.)
    Chapters                 NEVER   MediaExtractor and MediaMetadataRetriever
                                     expose no chapter markers, so Analyse
                                     always leaves MediaInfo.Chapters empty -
                                     which here says nothing about the file.
    HighBitDepthFrameSource  when    Set only when MediaCodecList has a VIDEO
                             device  DECODER whose capabilities list
                             has a   COLOR_FormatYUVP010 (10-bit output) for one
                             P010    of its types (HighBitDepthDecoders names
                             decoder them). Then OpenVideo with BitDepth = 10
                                     delivers I420, NV12 or Gray frames at 10
                                     bits; 12 bits are never offered. See
                                     10-BIT FRAMES below.

Every operation whose flag is missing throws NotSupportedException through the
contract's RequireCapability, naming "android-mediacodec" and the flag:
OpenVideo with PixelLayout I444, BitDepth 10 or 12 on a device without a P010
decoder, or IncludeAlpha = true (with a YUV / Gray layout); CreateWriter /
TranscodeFile with Mp4HevcAac on a device without an HEVC encoder. BitDepth 12
is refused with NotSupportedException on EVERY device (Android's decoders output
8-bit or 10-bit pictures only).

ALPHA AND MODE3: an Android app that AUTHORS video with transparency generates
its own frames (with their alpha) and encodes them with an encoder package
directly - for AV1, CodeBrix.VideoEncoding.Av1 (CodeBrixVideoEncodingAv1.
Register(), then VideoEncoders.Get("av1")), colour and alpha as two streams.
This engine is not involved in that path, and it cannot READ alpha back from a
file.


WHAT EACH OPERATION DOES
========================
Analyse / AnalyseAsync
    MediaExtractor reports the tracks; MediaMetadataRetriever adds the container
    MIME type, duration and tags. For MP4 / MOV files the engine also reads the
    sample tables itself: VideoStreamInfo.FrameRate is the EXACT rational from
    the presentation timestamps (30000/1001 stays 30000/1001; MediaExtractor's
    own value is a rounded number) and FrameCount is the sample count.
    Reported:
        MediaInfo.ContainerFormat   the MIME type, e.g. "video/mp4", "video/webm"
        MediaInfo.Duration          the longest track (microsecond-exact)
        VideoStreamInfo             CodecName in ffmpeg's words ("h264", "hevc",
                                    "av1", "vp9", "vp8", "mpeg4", ...; an
                                    unknown MIME type is reported as itself),
                                    stored Width / Height, FrameRate (exact for
                                    MP4 / MOV, else KEY_FRAME_RATE snapped to
                                    n/1001 or a whole number), FrameCount (MP4
                                    / MOV only), Duration, RotationDegrees
                                    (clockwise), ColorRange / ColorMatrix /
                                    ColorPrimaries / ColorTransfer (when the
                                    file tags them), BitDepth + ChromaSubsampling
                                    + PixelFormatName ("yuv420p" /
                                    "yuv420p10le") when the codec PROFILE fixes
                                    them, Tags["language"], Tags["mime"]
        AudioStreamInfo             CodecName ("aac", "opus", "vorbis", "flac",
                                    "mp3", "pcm", ...), SampleRate, Channels,
                                    Duration, BitDepth (raw PCM only)
        SubtitleStreamInfo          every track whose MIME starts with "text/"
                                    or is "application/x-subrip" /
                                    "application/ttml+xml": Index = the
                                    MediaExtractor track index, CodecName in
                                    ffmpeg's words ("mov_text" for an MP4's
                                    3GPP timed text "text/3gpp-tt", "webvtt",
                                    "subrip", "ttml", "eia_608"; else the MIME
                                    type), Language from KEY_LANGUAGE ("und" ->
                                    null), Title null (the platform reports
                                    none), Tags["language"], Tags["mime"]
        MediaInfo.Chapters          ALWAYS EMPTY - MediaExtractor and
                                    MediaMetadataRetriever expose no chapters
        MediaInfo.Tags              title, date, artist, album, album_artist,
                                    author, composer, writer, genre, year,
                                    location, track, disc, compilation - when
                                    present - and "creation_time" (ISO 8601 UTC,
                                    ffmpeg's name) when the date parses
    NOT reported (left 0 / null / Unspecified): HasAlpha (always false), the
    frame count of non-MP4 files, bit depth / pixel format when the profile
    does not fix them, stream tags other than language.

Snapshot(path, at)
    Decodes with the engine's own frame source from the sync sample at or
    before `at` and returns the FIRST frame of the constant-rate grid at or
    after it: an OWNED 8-bit I420 frame in STORED orientation, FrameIndex and
    Timestamp set - exactly the frame OpenVideo with StartTime = at would give
    first. (MediaMetadataRetriever.getFrameAtTime is NOT used: it returns a
    display-rotated ARGB bitmap of the "closest" frame, which is not what the
    contract describes.) Past the end: MediaFormatException.

OpenVideo(path, options)
    MediaExtractor + a MediaCodec decoder in synchronous mode, output read
    through the Image API (any plane layout the decoder chooses) into a
    tightly packed I420 buffer. Layouts: I420 (the buffer itself), Gray (its Y
    plane), NV12 and RGBA (converted into one more reused buffer; RGBA uses the
    stream's matrix and range - BT.601 up to 576 lines and BT.709 above when the
    file does not say - with alpha 255). 8-bit by default; I420, NV12 and Gray
    also at 10 bits where HighBitDepthFrameSource is set (see 10-BIT FRAMES).
    CONSTANT RATE: each decoded picture goes to the grid slot NEAREST its
    presentation time (slot 0 = the stream's first sample time); a slot with no
    picture repeats the previous one; several pictures in one slot keep the
    LATEST; nothing is produced after the last picture's slot. So
    frame.Timestamp == Info.FrameRate.TimestampOf(frame.FrameIndex) always, and
    FrameIndex counts from the start of the stream (also after StartTime or a
    Seek). A stream with no known frame rate throws MediaFormatException.
    SEEK: to the sync sample at or before the target, decoder flushed, earlier
    pictures decoded and dropped. CanSeek is true.
    ROTATION: pictures stay in stored orientation; Info.RotationDegrees says
    how to display them. Info.IsMirrored is ALWAYS false: the track format
    gives only a rotation angle ("rotation-degrees"), never the container's
    display matrix, so a mirrored source cannot be detected.
    OWNERSHIP: a frame is valid until the next TryReadFrame, Seek or Dispose
    (three buffers circulate); Copy() to keep it.
    Info.ColorRange is Full when the stream is tagged full range, else Limited.
    Info.ColorMatrix is the matrix of the DELIVERED samples: the stream's own
    for I420 / NV12 / Gray, Rgb for RGBA.

OpenAudio(path, options)
    MediaExtractor + a MediaCodec decoder -> the decoder's PCM (16-bit, float,
    8-, 24- or 32-bit, whichever its output format says) -> float -> channel
    mix -> resample -> interleaved float at options.SampleRate / Channels
    (0 keeps the source's).
    MIXING: same count copied; a MONO source is copied to every output
    channel; a MONO output is the average of all source channels; otherwise
    the first min(in, out) channels are copied - extra source channels are
    dropped, extra output channels are silent.
    RESAMPLING: LINEAR INTERPOLATION in exact integer position arithmetic (no
    drift). Deterministic and adequate for preview / authoring; it does not
    low-pass filter, so a large downsample can alias.
    StartTime and Seek are honoured to the sample.

CreateWriter(request, progress)
    A MediaCodec "video/avc" encoder ("video/hevc" for Mp4HevcAac), an AAC-LC
    MediaCodec encoder when HasAudio, and MediaMuxer (MP4), then the managed
    MP4 post-pass (FAST START and tags, below).
    FRAMES: 8-bit I420, NV12 or RGBA (InputPixelLayout); NV12 and RGBA are
    converted to I420 in managed code (RGBA with the request's ColorMatrix and
    ColorRange) and written into the encoder's input Image. The ENGINE stamps
    frame N at Request.FrameRate.TimestampOf(N); the frame's own Timestamp,
    FrameIndex and any alpha plane are ignored.
    QUALITY: see QUALITY -> BITRATE below; the result is clamped to the
    encoder's supported bitrate range. VBR mode when the encoder offers it.
    KEYFRAMES: KeyframeIntervalFrames becomes KEY_I_FRAME_INTERVAL in SECONDS
    as a float: frames / fps rounded to the nearest millisecond (never below
    0.001 s); 0 means the engine default of 2 seconds. The encoder converts
    back to frames and may round again (platform encoders usually round to
    whole frames).
    COLOUR: KEY_COLOR_RANGE from ColorRange (Unspecified written as limited);
    KEY_COLOR_STANDARD + KEY_COLOR_TRANSFER (SDR video) for Bt709, Bt601 and
    Bt2020NonConstant; other matrices leave them unset. They are TAGS - YUV
    input is not converted.
    ROTATION: MediaMuxer.setOrientationHint(RotationDegrees) - metadata only.
    AUDIO: float -> 16-bit PCM, fed in 1024-frame chunks, stamped from the
    running sample count. The muxer cannot start until both encoders have
    announced their formats, so coded samples that arrive earlier are held in
    memory. If HasAudio is true but WriteAudio is never called, Finish writes
    1024 frames of silence so the declared audio track exists.
    TAGS: Title and CreationTime are written by the post-pass (MediaMuxer has
    no API for them): title as moov/udta/meta/ilst "©nam", creation time into
    mvhd / tkhd / mdhd. Without CreationTime the file carries the time it was
    written (MediaMuxer's own value).
    Finish() completes the file; Dispose() without Finish() releases the codecs
    and DELETES the output. Progress: one report per frame (Fraction null).

TranscodeFile(request, progress, ct)
    OpenVideo + OpenAudio of the input -> the engine's own writer. "Keep" values
    resolve to the source's (an odd source size is rounded down to even). A size
    change uses a managed BILINEAR scaler; a rate change shows, at each output
    time, the source frame on screen then. Sound is resampled / re-mixed to the
    output format and kept in step with the pictures. The pictures keep their
    stored orientation and the source's rotation metadata is carried over; a
    non-zero Output.RotationDegrees replaces it. Progress Fraction = frames
    written / expected frames.

TrimFile(request, progress, ct)
    SnapToKeyframes = true: the coded samples are COPIED (MediaExtractor ->
    MediaMuxer), no codec involved. The cut starts at the video sync sample at
    or before From. Video samples at or after To are held back and written only
    if a later sample shown before To needs them as a reference (B-frames), so
    the output may END up to two pictures after To; it never loses a picture
    before To. Audio samples in [start, To) are copied. The output container
    follows the extension (.webm, .3gp / .3g2, .ogg / .opus by name; MP4
    otherwise) - the codecs must be ones that container can hold.
    SnapToKeyframes = false: frame-accurate decode + re-encode from From to To
    to H.264 + AAC at the engine default quality, source size / rate /
    rotation / audio format. A file WITHOUT video is always trimmed by copying.

ConcatenateFiles(request, progress, ct)
    COPY when every input has the same kinds of stream and, stream by stream:
    video - same MIME type, width, height and byte-identical codec
    configuration (csd-0 / csd-1: the H.264 SPS / PPS, the HEVC VPS / SPS /
    PPS); audio - same MIME type, sample rate, channel count and identical csd-0
    (the AAC AudioSpecificConfig). Clips written with the same writer settings
    on the same device normally match. Each input starts where the previous
    one's PICTURES end (last picture + one frame period); audio frames of the
    next input that would overlap the previous input's sound (codec padding,
    under one audio frame) are dropped.
    Otherwise - where the contract's copy-only promise cannot be kept - the
    inputs are DECODED AND RE-ENCODED to H.264 + AAC at the first input's
    size, frame rate (30 if unknown), rotation and audio format (other sizes
    are scaled), instead of failing. Inputs with no video and differing audio
    throw MediaFormatException.

Every MP4 the engine writes (writer, copies, re-encodes) goes through the
post-pass with fast start ON; TranscodeFile follows Output.FastStart.


10-BIT FRAMES (P010)
====================
    if (engine.Supports(VideoProcessorCapabilities.HighBitDepthFrameSource))
    {
        using IVideoFrameSource source = engine.OpenVideo(path,
            new VideoSourceOptions { BitDepth = 10 });     // I420, Nv12 or Gray
        // source.Info.BitDepth == 10; every sample is a ushort, little-endian,
        // 0..1023 in the LOW bits; Y.RowLength == 2 * Width
    }

THE PROBE. Capabilities are computed once, from MediaCodecList: the flag is set
when at least one video decoder lists COLOR_FormatYUVP010 (54) among its color
formats for one of its types. AndroidVideoProcessor.HighBitDepthDecoders shows
which ("c2.qti.hevc.decoder (video/hevc)", ...). Which decoders list it is
up to the device: on a Pixel 4 XL (API 33) the Qualcomm AVC / HEVC / VP8 / VP9
decoders do and Google's software decoders do not.

WHAT IS DELIVERED (BitDepth = 10):
    * A 10-bit stream (HEVC Main 10, VP9 profile 2, AV1 10-bit, ... - the
      depth comes from the stream's PROFILE) or a stream whose depth the
      profile does not fix: the decoder is configured for P010 output - the
      platform's own choice of decoder when it lists P010 for the stream's
      MIME type, else another decoder of that type that lists it and accepts
      the stream. P010 stores each sample in the HIGH 10 bits of a 16-bit
      word (value << 6); the engine shifts it down, so the delivered samples
      follow the CONTRACT's low-bits convention (the libaom / dav1d one), not
      P010's. Info.BitDepth = 10, Info.SourceBitDepth = 10 (0 when the
      profile does not say), IsBitDepthReduced = false.
    * An 8-bit stream: decoded as usual and every sample WIDENED, value << 2
      (16 -> 64, 235 -> 940, 255 -> 1020). Info.SourceBitDepth = 8 says the
      samples were widened and add no detail - the same reporting as the
      desktop engine.
    * Layouts: I420 (Y, U, V at two bytes per sample), Gray (the 10-bit Y
      plane), NV12 (Y, then Cb Cr interleaved SAMPLE BY SAMPLE - two bytes
      each - in the low-bits convention). RGBA is always 8-bit (the contract
      refuses RGBA + 10 with ArgumentOutOfRangeException); I444 is never
      offered.

REFUSALS - never silently cut samples when 10 bits were asked for:
    * No decoder of the stream's MIME type lists P010, the decoder refuses the
      P010 configuration, or it answers with 8-bit pictures:
      NotSupportedException naming the codec and the stream (e.g. "The
      c2.x.hevc.decoder decoder delivered 8-bit pictures ... for the 10-bit
      hevc stream (track 0) of '...'"). OpenVideo decodes the first picture
      while opening, so this normally comes from OpenVideo itself; a decoder
      that switches to 8-bit pictures later fails TryReadFrame the same way.
      Open the file with BitDepth = 8 instead (the samples are then cut to
      8 bits and IsBitDepthReduced says so).
    * BitDepth = 12: NotSupportedException on every device.

8-BIT FRAMES OF A 10-BIT STREAM (the default BitDepth = 8): the samples are
the top 8 bits of the 10-bit ones (value >> 2), Info.SourceBitDepth = 10 and
IsBitDepthReduced = true. Where a decoder lists P010 the engine decodes such a
stream through P010 and cuts it (some hardware decoders - the Pixel 4 XL's
c2.qti.hevc.decoder - fail outright when asked to convert a 10-bit stream to
flexible 8-bit buffers); otherwise through flexible 8-bit output as before.

Snapshot, TranscodeFile, TrimFile (re-encode) and ConcatenateFiles (re-encode)
work on 8-bit frames, and the writer takes 8-bit frames only.


QUALITY -> BITRATE
==================
Platform encoders take a bitrate, not a CRF. The engine maps a VideoQuality to
a target bitrate DETERMINISTICALLY:

    TargetBitrate(k)       k kbit/s as given.
    ConstantQuality(L)     bits per pixel per frame
                               bpp(L) = 0.1 x 2^((23 - L) / 6)
                           rounded to a whole ten-thousandth; level 23 is
                           0.1000 and every 6 levels halves / doubles it.
                           kbit/s = floor(width x height x fps x bpp / 1000)
                           (exact for rational rates), clamped to 64..200000.
                           HEVC: 65% of the H.264 figure.
    EngineDefault          level 23.
Then clamped to the encoder's supported range on the device.

    level  bpp      640x480@30  1280x720@30  1920x1080@30  3840x2160@30
      0   1.4254      13136        39409        88671        200000
      6   0.7127       6568        19704        44335        177342
     12   0.3564       3284         9853        22170         88683
     16   0.2245       2068         6206        13965         55862
     18   0.1782       1642         4926        11085         44341
     20   0.1414       1303         3909         8796         35184
     23   0.1000        921         2764         6220         24883
     26   0.0707        651         1954         4398         17592
     28   0.0561        517         1551         3489         13959
     30   0.0445        410         1230         2768         11073
     35   0.0250        230          691         1555          6220
     40   0.0140        129          387          870          3483
     45   0.0079         72          218          491          1965
     51   0.0039         64          107          242           970
    (H.264 kbit/s; HEVC is 65% of each figure.)

The scale is a request, not a guarantee: a hardware encoder hits its bitrate
target, not a perceptual quality, so the same level can look different from
the desktop engine's x264 CRF.


FAST START
==========
StandardVideoRequest.FastStart = true (the default) puts the MP4 index
("moov") before the media data, so playback can begin before the whole file
has arrived. MediaMuxer often reserves room at the front and writes moov there
itself; when it does not (moov at the end), the engine's managed post-pass
moves it: it lists the top-level boxes, moves moov before the first mdat
(dropping top-level free / skip padding), and patches every chunk offset
(stco / co64) by how far its data moved - upgrading a 32-bit stco to a 64-bit
co64 if an offset would pass 4 GiB. The new file is written beside the old one
and moved over it, so a failure leaves the original intact. Fragmented MP4s are
left alone. FastStart = false leaves moov wherever MediaMuxer put it.


THREADING
=========
The engine object holds no per-call state and may be used from several threads
at once. Every source, writer and whole-file operation drives its codecs in
MediaCodec's SYNCHRONOUS mode ON THE CALLING THREAD - no background threads are
started, no callbacks arrive later. Consequences:
    * Call long operations (TranscodeFile, TrimFile, ConcatenateFiles, a
      writer loop, a frame-source loop) from a worker thread, never the UI
      thread.
    * IProgress<VideoProgress>.Report is called on that same thread. A
      Progress<T> created on the UI thread posts to it; a custom IProgress runs
      inline.
    * Sources and writers are single-threaded objects, as the contract says.


CANCELLATION AND FAILURES
=========================
    * TranscodeFile / TrimFile / ConcatenateFiles check the CancellationToken
      between samples / frames and while waiting on the decoders; cancellation
      throws OperationCanceledException within about one frame's work and the
      partial output (and any ".rewrite.tmp" beside it) is DELETED. Nothing
      else is left behind.
    * Snapshot checks its token while decoding; Analyse checks it between its
      extractor and retriever steps (each of which is quick).
    * A codec that stops making progress for 15 seconds (a hung or crashed
      codec process) fails the operation with VideoProcessingException rather
      than waiting forever.
    * Platform exceptions (MediaCodec.CodecException, IllegalStateException,
      IOException) never escape raw: they are wrapped in
      VideoProcessingException (the original is the InnerException; the
      message carries the codec error code and diagnostic text). An input
      MediaExtractor cannot read throws MediaFormatException; a missing file
      FileNotFoundException.
    * The writer deletes its output when it fails or is disposed unfinished.


DEVICES AND CODECS
==================
The engine uses whatever codecs MediaCodecList offers (the platform's first
choice for each format - usually a hardware codec, else Google's software
codecs). H.264 and AAC are present on every Android device; HEVC encode varies
(HevcOutput). If a device's video codecs cannot run at all - some Android-x86
images abort their software-codec process ("gralloc-mapper is missing") - the
video operations fail with VideoProcessingException while Analyse, OpenAudio
and the sample-copy paths (TrimFile with SnapToKeyframes, matching
ConcatenateFiles) still work, because they need no video codec.


COMMON PITFALLS
===============
    * Forgetting CodeBrixVideoProcessingAndroid.Register() - VideoProcessors.
      Current then throws VideoProcessorNotRegisteredException.
    * Referencing this package from a shared LIBRARY - reference
      CodeBrix.VideoProcessing.Core there; only the Android app references the
      engine.
    * Running a transcode on the UI thread - everything runs on the calling
      thread.
    * Asking for Mp4HevcAac without checking Supports(HevcOutput).
    * Asking OpenVideo for alpha or I444 frames - not on Android
      (NotSupportedException); use an encoder package for alpha authoring.
    * Asking for BitDepth = 10 without checking
      Supports(HighBitDepthFrameSource) - it is device-dependent - and not
      being ready for NotSupportedException from OpenVideo even when it is
      set: the flag says SOME decoder outputs P010, not that the one for this
      stream will.
    * Reading 10-bit samples as P010 (high bits) - the engine has already
      shifted them to the LOW bits; 1023 is the maximum.
    * Asking for 12-bit frames - never on Android.
    * Expecting ConstantQuality(L) to look identical to the desktop engine -
      it is a bitrate here (see QUALITY -> BITRATE).
    * Expecting a snapped trim to end exactly at To - it may include up to two
      reference pictures after it; use SnapToKeyframes = false for an exact cut.
    * Passing a content:// URI - the engine takes file paths.
    * Keeping a frame from TryReadFrame without Copy().

================================================================================
END OF AGENT-README
================================================================================
