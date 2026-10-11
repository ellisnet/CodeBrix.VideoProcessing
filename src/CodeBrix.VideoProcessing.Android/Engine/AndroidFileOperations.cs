using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;
using global::Android.Media;
using ByteBuffer = global::Java.Nio.ByteBuffer;
using JavaThrowable = global::Java.Lang.Throwable;

namespace CodeBrix.VideoProcessing.Android.Engine;

/// <summary>
/// The whole-file operations: TranscodeFile (decode + re-encode), TrimFile (sample copy from a keyframe, or decode +
/// re-encode) and ConcatenateFiles (sample copy when the inputs match, else decode + re-encode). Every operation runs
/// on the caller's thread, reports progress, checks the cancellation token between samples / frames, and on failure
/// or cancellation deletes the partial output.
/// </summary>
internal static class AndroidFileOperations
{
    private const int DefaultSampleBufferBytes = 1 << 20;
    private const int MaxHeldSamples = 8;

    // ------------------------------------------------------------------ transcode

    internal static void Transcode(AndroidVideoProcessor engine, TranscodeRequest request, IProgress<VideoProgress> progress, CancellationToken cancellationToken)
    {
        AnalysedMedia source = AndroidMediaAnalyser.Analyse(request.InputPath, cancellationToken);
        VideoStreamInfo video = source.Info.PrimaryVideo ?? throw new MediaFormatException($"'{request.InputPath}' has no video stream.");
        AudioStreamInfo audio = source.Info.PrimaryAudio;

        StandardVideoRequest output = request.Output.Clone();
        bool keepSize = output.Width == 0 && output.Height == 0;
        output.Width = keepSize ? video.Width & ~1 : output.Width;
        output.Height = keepSize ? video.Height & ~1 : output.Height;
        if (!output.FrameRate.IsSpecified) output.FrameRate = video.FrameRate;
        if (!output.FrameRate.IsSpecified) throw new MediaFormatException($"The video stream of '{request.InputPath}' has no known frame rate; set Output.FrameRate.");
        output.InputPixelLayout = VideoPixelLayout.I420;
        if (output.RotationDegrees == 0) output.RotationDegrees = video.RotationDegrees;
        output.HasAudio = output.HasAudio && audio != null;
        if (output.HasAudio)
        {
            if (output.AudioSampleRate == 0) output.AudioSampleRate = Math.Clamp(audio.SampleRate > 0 ? audio.SampleRate : 48000, 8000, 96000);
            if (output.AudioChannels == 0) output.AudioChannels = Math.Clamp(audio.Channels > 0 ? audio.Channels : 2, 1, 8);
        }

        output.Validate();
        long estimatedFrames = output.FrameRate.FrameCountFor(video.Duration);
        Encode(engine, output, new[] { new Segment(request.InputPath, video, audio, TimeSpan.Zero, TimeSpan.MaxValue) }, estimatedFrames, progress, cancellationToken);
    }

    // ------------------------------------------------------------------ trim

    internal static void Trim(AndroidVideoProcessor engine, TrimRequest request, IProgress<VideoProgress> progress, CancellationToken cancellationToken)
    {
        AnalysedMedia source = AndroidMediaAnalyser.Analyse(request.InputPath, cancellationToken);
        if (!source.Info.HasVideo && !source.Info.HasAudio) throw new MediaFormatException($"'{request.InputPath}' has neither video nor audio.");

        if (request.SnapToKeyframes || !source.Info.HasVideo)
        {
            CopySamples(new[] { request.InputPath }, request.OutputPath, request.From, request.To, progress, cancellationToken);
            return;
        }

        VideoStreamInfo video = source.Info.PrimaryVideo;
        AudioStreamInfo audio = source.Info.PrimaryAudio;
        var output = new StandardVideoRequest
        {
            OutputPath = request.OutputPath,
            Width = video.Width & ~1,
            Height = video.Height & ~1,
            FrameRate = video.FrameRate,
            RotationDegrees = video.RotationDegrees,
            HasAudio = audio != null,
            AudioSampleRate = audio != null ? Math.Clamp(audio.SampleRate > 0 ? audio.SampleRate : 48000, 8000, 96000) : 48000,
            AudioChannels = audio != null ? Math.Clamp(audio.Channels > 0 ? audio.Channels : 2, 1, 8) : 2
        };
        if (!output.FrameRate.IsSpecified) throw new MediaFormatException($"The video stream of '{request.InputPath}' has no known frame rate.");
        output.Validate();

        long first = FirstSlotAtOrAfter(video.FrameRate, request.From);
        long end = FirstSlotAtOrAfter(video.FrameRate, request.To);
        Encode(engine, output, new[] { new Segment(request.InputPath, video, audio, request.From, request.To) }, Math.Max(1, end - first), progress, cancellationToken);
    }

    // ------------------------------------------------------------------ concatenate

    internal static void Concatenate(AndroidVideoProcessor engine, ConcatenateRequest request, IProgress<VideoProgress> progress, CancellationToken cancellationToken)
    {
        var analysed = new List<AnalysedMedia>();
        var signatures = new List<(TrackDescription Video, TrackDescription Audio)>();
        foreach (string input in request.InputPaths)
        {
            AnalysedMedia media = AndroidMediaAnalyser.Analyse(input, cancellationToken);
            analysed.Add(media);
            signatures.Add((media.FirstVideo, media.FirstAudio));
        }

        if (ConcatenationRule.CanCopy(signatures, out string reason))
        {
            CopySamples(request.InputPaths, request.OutputPath, TimeSpan.Zero, TimeSpan.MaxValue, progress, cancellationToken);
            return;
        }

        VideoStreamInfo firstVideo = analysed[0].Info.PrimaryVideo;
        if (firstVideo == null)
        {
            throw new MediaFormatException($"The inputs cannot be joined by copying ({reason}) and the first input has no video to re-encode around.");
        }

        AudioStreamInfo firstAudio = analysed[0].Info.PrimaryAudio;
        FrameRate rate = firstVideo.FrameRate.IsSpecified ? firstVideo.FrameRate : new FrameRate(30, 1);
        bool anyAudio = analysed.Exists(media => media.Info.HasAudio);
        var output = new StandardVideoRequest
        {
            OutputPath = request.OutputPath,
            Width = firstVideo.Width & ~1,
            Height = firstVideo.Height & ~1,
            FrameRate = rate,
            RotationDegrees = firstVideo.RotationDegrees,
            HasAudio = anyAudio,
            AudioSampleRate = firstAudio != null ? Math.Clamp(firstAudio.SampleRate > 0 ? firstAudio.SampleRate : 48000, 8000, 96000) : 48000,
            AudioChannels = firstAudio != null ? Math.Clamp(firstAudio.Channels > 0 ? firstAudio.Channels : 2, 1, 8) : 2
        };
        output.Validate();

        var segments = new List<Segment>();
        long estimated = 0;
        for (int i = 0; i < analysed.Count; i++)
        {
            VideoStreamInfo video = analysed[i].Info.PrimaryVideo ?? throw new MediaFormatException($"Input {i} ('{request.InputPaths[i]}') has no video stream to re-encode.");
            segments.Add(new Segment(request.InputPaths[i], video, analysed[i].Info.PrimaryAudio, TimeSpan.Zero, TimeSpan.MaxValue));
            estimated += rate.FrameCountFor(video.Duration);
        }

        Encode(engine, output, segments, estimated, progress, cancellationToken);
    }

    // ------------------------------------------------------------------ decode + encode

    private sealed class Segment
    {
        internal Segment(string path, VideoStreamInfo video, AudioStreamInfo audio, TimeSpan from, TimeSpan to)
        {
            Path = path;
            Video = video;
            Audio = audio;
            From = from;
            To = to;
        }

        internal string Path { get; }

        internal VideoStreamInfo Video { get; }

        internal AudioStreamInfo Audio { get; }

        internal TimeSpan From { get; }

        internal TimeSpan To { get; }
    }

    // Decodes each segment's pictures (resampled in time to the output rate, scaled to the output size) and sound
    // (at the output rate / channels, padded or cut to the segment's picture length) into ONE writer.
    private static void Encode(AndroidVideoProcessor engine, StandardVideoRequest output, IReadOnlyList<Segment> segments, long estimatedFrames, IProgress<VideoProgress> progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stopwatch stopwatch = Stopwatch.StartNew();
        FrameRate outRate = output.FrameRate;
        int outWidth = output.Width;
        int outHeight = output.Height;
        byte[] scaled = new byte[YuvConversion.I420Size(outWidth, outHeight)];
        int chromaWidth = (outWidth + 1) / 2;
        int chromaHeight = (outHeight + 1) / 2;
        int luma = outWidth * outHeight;
        int chroma = chromaWidth * chromaHeight;
        float[] audioBuffer = output.HasAudio ? new float[4096 * output.AudioChannels] : null;

        long outIndex = 0;
        long audioWritten = 0;
        bool success = false;
        var writer = (AndroidStandardVideoWriter)engine.CreateWriter(output);
        try
        {
            foreach (Segment segment in segments)
            {
                FrameRate inRate = segment.Video.FrameRate;
                long segmentStart = outIndex;
                using var frames = new AndroidVideoFrameSource(segment.Path, segment.Video, new VideoSourceOptions { StartTime = segment.From }, 0, cancellationToken);
                AndroidAudioSampleSource sound = null;
                if (output.HasAudio && segment.Audio != null)
                {
                    sound = new AndroidAudioSampleSource(segment.Path, segment.Audio,
                        new AudioSourceOptions { SampleRate = output.AudioSampleRate, Channels = output.AudioChannels, StartTime = segment.From },
                        cancellationToken);
                }

                try
                {
                    // Two owned copies: the source frame on screen now, and the one after it.
                    var shownSlot = new OwnedFrame();
                    var nextSlot = new OwnedFrame();
                    bool haveFrame = shownSlot.ReadFrom(frames);
                    bool haveNext = haveFrame && nextSlot.ReadFrom(frames);

                    while (haveFrame)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        TimeSpan segmentTime = outRate.TimestampOf(outIndex - segmentStart);
                        TimeSpan sourceTime = segment.From + segmentTime;
                        if (sourceTime >= segment.To) break;

                        // Advance while the next source frame starts at or before this output frame's time.
                        while (haveNext && nextSlot.Frame.Timestamp <= sourceTime)
                        {
                            (shownSlot, nextSlot) = (nextSlot, shownSlot);
                            haveNext = nextSlot.ReadFrom(frames);
                        }

                        VideoFrameData shown = shownSlot.Frame;
                        // Past the last source frame's own slot: the segment is over (compared as slots, not
                        // tick-rounded times, so a frame is never repeated at the end).
                        if (!haveNext && inRate.FrameIndexAt(sourceTime) > shown.FrameIndex) break;

                        VideoFrameData frame = shown;
                        if (shown.Width != outWidth || shown.Height != outHeight)
                        {
                            YuvConversion.ScaleI420(shownSlot.Buffer, shown.Width, shown.Height, scaled, outWidth, outHeight);
                            frame = VideoFrameData.CreateI420(outWidth, outHeight,
                                new VideoPlane(new ReadOnlyMemory<byte>(scaled, 0, luma), outWidth, outWidth, outHeight),
                                new VideoPlane(new ReadOnlyMemory<byte>(scaled, luma, chroma), chromaWidth, chromaWidth, chromaHeight),
                                new VideoPlane(new ReadOnlyMemory<byte>(scaled, luma + chroma, chroma), chromaWidth, chromaWidth, chromaHeight),
                                TimeSpan.Zero, 0);
                        }

                        writer.WriteFrame(frame);
                        outIndex++;

                        if (output.HasAudio)
                        {
                            long target = AudioFramesAt(outRate.TimestampOf(outIndex), output.AudioSampleRate);
                            audioWritten = FeedAudio(writer, sound, audioBuffer, output.AudioChannels, audioWritten, target, cancellationToken);
                        }

                        double? fraction = estimatedFrames > 0 ? Math.Min(1.0, (double)outIndex / estimatedFrames) : null;
                        progress?.Report(new VideoProgress(fraction, outIndex, outRate.TimestampOf(outIndex), stopwatch.Elapsed));
                    }
                }
                finally
                {
                    sound?.Dispose();
                }
            }

            if (outIndex == 0) throw new MediaFormatException("There is no video in the requested range.");
            writer.Finish();
            success = true;
        }
        catch (JavaThrowable exception)
        {
            throw new VideoProcessingException($"Writing '{output.OutputPath}' failed: {AndroidMedia.Describe(exception)}", exception);
        }
        finally
        {
            writer.Dispose();
            if (!success) AndroidMedia.DeleteQuietly(output.OutputPath);
        }

        progress?.Report(new VideoProgress(1, outIndex, outRate.TimestampOf(outIndex), stopwatch.Elapsed));
    }

    // A reusable owned copy of one decoded I420 frame (tightly packed), so a frame outlives the next read.
    private sealed class OwnedFrame
    {
        internal byte[] Buffer { get; private set; } = Array.Empty<byte>();

        internal VideoFrameData Frame { get; private set; }

        internal bool ReadFrom(IVideoFrameSource source)
        {
            if (!source.TryReadFrame(out VideoFrameData decoded))
            {
                Frame = default;
                return false;
            }

            int width = decoded.Width;
            int height = decoded.Height;
            int size = YuvConversion.I420Size(width, height);
            if (Buffer.Length != size) Buffer = new byte[size];
            YuvConversion.PackI420(decoded, Buffer);

            int chromaWidth = (width + 1) / 2;
            int chromaHeight = (height + 1) / 2;
            int luma = width * height;
            int chroma = chromaWidth * chromaHeight;
            Frame = VideoFrameData.CreateI420(width, height,
                new VideoPlane(new ReadOnlyMemory<byte>(Buffer, 0, luma), width, width, height),
                new VideoPlane(new ReadOnlyMemory<byte>(Buffer, luma, chroma), chromaWidth, chromaWidth, chromaHeight),
                new VideoPlane(new ReadOnlyMemory<byte>(Buffer, luma + chroma, chroma), chromaWidth, chromaWidth, chromaHeight),
                decoded.Timestamp, decoded.FrameIndex);
            return true;
        }
    }

    // Writes sound up to an absolute frame count: from the source while it lasts, then silence.
    private static long FeedAudio(AndroidStandardVideoWriter writer, AndroidAudioSampleSource sound, float[] buffer, int channels, long written, long target, CancellationToken cancellationToken)
    {
        while (written < target)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int want = (int)Math.Min(buffer.Length / channels, target - written);
            int got = sound != null ? sound.Read(buffer.AsSpan(0, want * channels)) : 0;
            if (got < want) Array.Clear(buffer, got * channels, (want - got) * channels);
            writer.WriteAudio(buffer.AsSpan(0, want * channels));
            written += want;
        }

        return written;
    }

    private static long AudioFramesAt(TimeSpan time, int sampleRate) => (long)((Int128)time.Ticks * sampleRate / TimeSpan.TicksPerSecond);

    private static long FirstSlotAtOrAfter(FrameRate rate, TimeSpan time)
    {
        long slot = rate.FrameIndexAt(time);
        if (rate.TimestampOf(slot) < time) slot++;
        return slot;
    }

    // ------------------------------------------------------------------ sample copy

    // One frame period of a video track in microseconds (rounded), from its exact rate; 0 when the rate is unknown.
    private static long FramePeriodMicroseconds(TrackDescription track)
    {
        FrameRate rate = MediaFormatMapping.ChooseFrameRate(track);
        return rate.IsSpecified ? (long)Math.Round(1_000_000.0 * rate.Denominator / rate.Numerator) : 0;
    }

    // One coded audio frame in microseconds: 1024 samples (AAC) at the track's rate; 0 when unknown.
    private static long AudioFramePeriodMicroseconds(MediaFormat format)
    {
        int rate = format == null ? -1 : AndroidMedia.GetInt(format, MediaFormatKeys.SampleRate);
        return rate > 0 ? 1024L * 1_000_000 / rate : 0;
    }

    // Copies the first video and first audio track of each input, in order, into one muxer: samples in [from, to)
    // of each input (from snapped back to the video sync sample at or before it), shifted so each input starts where
    // the previous one ended. Used by snapped trims (one input) and matching concatenations (from = 0, to = end).
    private static void CopySamples(IList<string> inputs, string outputPath, TimeSpan from, TimeSpan to, IProgress<VideoProgress> progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stopwatch stopwatch = Stopwatch.StartNew();
        MuxerOutputType type = AndroidMedia.MuxerTypeFor(outputPath);
        AndroidMedia.DeleteQuietly(outputPath);
        MediaMuxer muxer = null;
        bool started = false;
        bool success = false;
        long frames = 0;
        try
        {
            muxer = new MediaMuxer(outputPath, type);
            int videoTrack = -1;
            int audioTrack = -1;
            long offset = 0;
            long lastAudio = -1;
            var info = new MediaCodec.BufferInfo();
            ByteBuffer buffer = null;

            for (int n = 0; n < inputs.Count; n++)
            {
                string input = inputs[n];
                MediaExtractor extractor = AndroidMedia.OpenExtractor(input);
                try
                {
                    List<TrackDescription> tracks = AndroidMedia.ReadTracks(extractor, input);
                    int videoIndex = -1;
                    int audioIndex = -1;
                    MediaFormat videoFormat = null;
                    MediaFormat audioFormat = null;
                    for (int i = 0; i < extractor.TrackCount; i++)
                    {
                        MediaFormat format = extractor.GetTrackFormat(i);
                        string mime = AndroidMedia.GetString(format, MediaFormatKeys.Mime) ?? string.Empty;
                        if (videoIndex < 0 && mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) { videoIndex = i; videoFormat = format; }
                        else if (audioIndex < 0 && mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) { audioIndex = i; audioFormat = format; }
                    }

                    if (n == 0)
                    {
                        if (videoFormat != null)
                        {
                            int rotation = MediaFormatMapping.NormaliseRotation(AndroidMedia.GetInt(videoFormat, MediaFormatKeys.Rotation));
                            if (rotation != 0) muxer.SetOrientationHint(rotation);
                            videoTrack = muxer.AddTrack(videoFormat);
                        }

                        if (audioFormat != null) audioTrack = muxer.AddTrack(audioFormat);
                        muxer.Start();
                        started = true;
                    }

                    int capacity = DefaultSampleBufferBytes;
                    if (videoFormat != null) capacity = Math.Max(capacity, AndroidMedia.GetInt(videoFormat, MediaFormatKeys.MaxInputSize, 0));
                    if (audioFormat != null) capacity = Math.Max(capacity, AndroidMedia.GetInt(audioFormat, MediaFormatKeys.MaxInputSize, 0));
                    if (buffer == null || buffer.Capacity() < capacity) buffer = ByteBuffer.AllocateDirect(capacity);

                    // The start: the video sync sample at or before "from" (or "from" itself without video).
                    long origin = 0;
                    if (videoIndex >= 0)
                    {
                        extractor.SelectTrack(videoIndex);
                        extractor.SeekTo(0, MediaExtractorSeekTo.PreviousSync);
                        origin = Math.Max(0, extractor.SampleTime);
                    }
                    else if (audioIndex >= 0)
                    {
                        extractor.SelectTrack(audioIndex);
                        extractor.SeekTo(0, MediaExtractorSeekTo.PreviousSync);
                        origin = Math.Max(0, extractor.SampleTime);
                    }

                    long fromMicroseconds = origin + MediaFormatMapping.ToMicroseconds(from);
                    long toMicroseconds = to == TimeSpan.MaxValue ? long.MaxValue : origin + MediaFormatMapping.ToMicroseconds(to);
                    extractor.SeekTo(fromMicroseconds, MediaExtractorSeekTo.PreviousSync);
                    long start = extractor.SampleTime >= 0 ? extractor.SampleTime : fromMicroseconds;
                    if (videoIndex >= 0 && audioIndex >= 0) extractor.SelectTrack(audioIndex);
                    extractor.SeekTo(start, MediaExtractorSeekTo.PreviousSync);

                    bool videoDone = videoIndex < 0;
                    bool audioDone = audioIndex < 0;
                    long lastVideoRelative = -1;
                    long lastAudioRelative = -1;

                    // Video samples at or after "to" are HELD, not dropped at once: in decode order a picture shown
                    // after "to" can come before B-frames shown before it that reference it, and then it must be
                    // written too. Held samples are written when such a B-frame arrives; a sync sample (or a run of
                    // MaxHeldSamples) at or after "to" ends the video.
                    var held = new List<(byte[] Data, long Time, MediaCodecBufferFlags Flags)>();

                    void Write(bool isVideo, int size, long time, MediaCodecBufferFlags flags)
                    {
                        long stamped = time - start + offset;
                        info.Set(0, size, stamped, flags);
                        buffer.Position(0);
                        buffer.Limit(size);
                        muxer.WriteSampleData(isVideo ? videoTrack : audioTrack, buffer, info);
                        buffer.Clear();
                        if (isVideo)
                        {
                            lastVideoRelative = Math.Max(lastVideoRelative, time - start);
                            frames++;
                        }
                        else
                        {
                            lastAudio = Math.Max(lastAudio, stamped);
                            lastAudioRelative = Math.Max(lastAudioRelative, time - start);
                        }
                    }

                    while (!(videoDone && audioDone))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int track = extractor.SampleTrackIndex;
                        if (track < 0) break;
                        long time = extractor.SampleTime;
                        bool isVideo = track == videoIndex;
                        MediaCodecBufferFlags flags = (extractor.SampleFlags & MediaExtractorSampleFlags.Sync) != 0
                            ? MediaCodecBufferFlags.KeyFrame
                            : MediaCodecBufferFlags.None;

                        if (isVideo && !videoDone)
                        {
                            if (time >= toMicroseconds)
                            {
                                if (flags == MediaCodecBufferFlags.KeyFrame || held.Count >= MaxHeldSamples)
                                {
                                    videoDone = true;
                                    held.Clear();
                                }
                                else
                                {
                                    int size = extractor.ReadSampleData(buffer, 0);
                                    if (size > 0)
                                    {
                                        byte[] copy = new byte[size];
                                        AndroidMedia.CopyFrom(buffer, 0, copy, 0, size);
                                        held.Add((copy, time, flags));
                                    }

                                    buffer.Clear();
                                }
                            }
                            else if (time >= start)
                            {
                                foreach ((byte[] data, long heldTime, MediaCodecBufferFlags heldFlags) in held)
                                {
                                    buffer.Clear();
                                    AndroidMedia.CopyTo(data, 0, data.Length, buffer, 0);
                                    Write(true, data.Length, heldTime, heldFlags);
                                }

                                held.Clear();
                                int size = extractor.ReadSampleData(buffer, 0);
                                if (size > 0) Write(true, size, time, flags);
                            }
                        }
                        else if (!isVideo && !audioDone)
                        {
                            if (time >= toMicroseconds)
                            {
                                audioDone = true;
                            }
                            else if (time >= start && time - start + offset > lastAudio)
                            {
                                // (An input's sound can run a little past its pictures - codec frame padding - so
                                // the next input's first audio frames that would overlap it are dropped and sound
                                // stays in step with the pictures.)
                                int size = extractor.ReadSampleData(buffer, 0);
                                if (size > 0) Write(false, size, time, flags);
                            }
                        }

                        extractor.Advance();
                    }

                    // The next input starts where this one's PICTURES end - the last copied picture plus one frame
                    // period at the track's exact rate - so joined video is seamless (a track duration would include
                    // B-frame reordering delay or the sound's codec padding). A file without video ends with its
                    // last audio frame (1024 samples for AAC).
                    long period = videoIndex >= 0
                        ? FramePeriodMicroseconds(tracks[videoIndex])
                        : AudioFramePeriodMicroseconds(audioFormat);
                    long lastRelative = videoIndex >= 0 ? lastVideoRelative : lastAudioRelative;
                    offset += Math.Max(1, lastRelative + Math.Max(1, period));

                    progress?.Report(new VideoProgress((double)(n + 1) / inputs.Count, frames, TimeSpan.FromTicks(offset * 10), stopwatch.Elapsed));
                }
                finally
                {
                    AndroidMedia.ReleaseQuietly(extractor);
                }
            }

            muxer.Stop();
            AndroidMedia.ReleaseQuietly(muxer, stop: false);
            muxer = null;
            AndroidMedia.FinishMp4(outputPath, type, fastStart: true, title: null, creationTime: null);
            success = true;
        }
        catch (JavaThrowable exception)
        {
            throw new VideoProcessingException($"Copying samples into '{outputPath}' failed: {AndroidMedia.Describe(exception)}", exception);
        }
        finally
        {
            if (muxer != null) AndroidMedia.ReleaseQuietly(muxer, stop: started);
            if (!success)
            {
                AndroidMedia.DeleteQuietly(outputPath);
                AndroidMedia.DeleteQuietly(outputPath + ".rewrite.tmp");
            }
        }

        progress?.Report(new VideoProgress(1, frames, TimeSpan.Zero, stopwatch.Elapsed));
    }
}
