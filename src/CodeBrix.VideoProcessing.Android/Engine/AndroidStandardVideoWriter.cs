using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
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
/// Writes a standard MP4 with a MediaCodec video encoder ("video/avc", or "video/hevc"), a MediaCodec AAC-LC encoder
/// and MediaMuxer, followed by the managed MP4 post-pass (fast start, title, creation time).
/// </summary>
/// <remarks>
/// <para>
/// THREADING: synchronous MediaCodec mode, entirely on the caller's thread - every WriteFrame / WriteAudio queues
/// its input and then drains whatever output the encoders have ready into the muxer. There are no background threads.
/// </para>
/// <para>
/// VIDEO: each frame is converted (NV12 and RGBA managed-side) into one reused tightly packed I420 buffer and written
/// into the encoder's input Image (COLOR_FormatYUV420Flexible), stamped
/// <c>FrameRate.TimestampOf(FramesWritten)</c>. The bitrate comes from <see cref="QualityBitrateTable" /> (clamped to
/// the encoder's range), the keyframe interval from <see cref="KeyframeInterval" />, and the colour range / standard /
/// transfer keys from the request.
/// </para>
/// <para>
/// AUDIO: float samples become 16-bit PCM and are fed in 1024-frame chunks, stamped from the running sample count.
/// The muxer cannot start before BOTH encoders have announced their formats, so coded samples that arrive earlier
/// are held in memory until then. A request with sound whose caller never writes any gets 1024 frames of silence at
/// Finish, so the audio track exists as declared.
/// </para>
/// </remarks>
internal sealed class AndroidStandardVideoWriter : IStandardVideoWriter
{
    private const int AudioChunkFrames = 1024;

    private readonly IProgress<VideoProgress> progress;
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private readonly MediaCodec.BufferInfo bufferInfo = new MediaCodec.BufferInfo();
    private readonly List<PendingSample> pending = new List<PendingSample>();
    private readonly byte[] picture;
    private readonly int audioChunkBytes;
    private readonly byte[] audioStaging;

    private MediaCodec video;
    private MediaCodec audio;
    private MediaMuxer muxer;
    private int videoTrack = -1;
    private int audioTrack = -1;
    private bool muxerStarted;
    private bool videoEnded;
    private bool audioEnded;
    private int audioStagingBytes;
    private long audioFramesQueued;
    private bool finished;
    private bool completed;
    private bool disposed;

    internal AndroidStandardVideoWriter(StandardVideoRequest request, IProgress<VideoProgress> progress)
    {
        Request = request;
        this.progress = progress;
        picture = new byte[YuvConversion.I420Size(request.Width, request.Height)];
        audioChunkBytes = AudioChunkFrames * Math.Max(1, request.AudioChannels) * 2;
        audioStaging = new byte[audioChunkBytes];

        try
        {
            AndroidMedia.DeleteQuietly(request.OutputPath);
            muxer = new MediaMuxer(request.OutputPath, MuxerOutputType.Mpeg4);
            if (request.RotationDegrees != 0) muxer.SetOrientationHint(request.RotationDegrees);
            video = CreateVideoEncoder(request, out string videoEncoderName);
            VideoEncoderName = videoEncoderName;
            if (request.HasAudio) audio = CreateAudioEncoder(request);
        }
        catch (JavaThrowable exception)
        {
            Abandon();
            throw new VideoProcessingException($"The MP4 writer for '{request.OutputPath}' could not be started: {AndroidMedia.Describe(exception)}", exception);
        }
        catch
        {
            Abandon();
            throw;
        }
    }

    public StandardVideoRequest Request { get; }

    public long FramesWritten { get; private set; }

    public long AudioFramesWritten { get; private set; }

    /// <summary>The name of the platform video encoder in use (for diagnostics).</summary>
    internal string VideoEncoderName { get; }

    /// <summary>The bitrate the video encoder was configured with, in bits per second.</summary>
    internal int VideoBitrate { get; private set; }

    public void WriteFrame(in VideoFrameData frame)
    {
        ThrowIfUnusable();
        EngineRequestGate.CheckWriterFrame(Request, frame);

        switch (frame.Layout)
        {
            case VideoPixelLayout.Rgba:
                YuvConversion.RgbaToI420(frame.Rgba, frame.Width, frame.Height, Request.ColorMatrix, Request.ColorRange, picture);
                break;
            case VideoPixelLayout.Nv12:
                YuvConversion.Nv12ToI420(frame.Y, frame.UV, frame.Width, frame.Height, picture);
                break;
            default:
                YuvConversion.PackI420(frame, picture);
                break;
        }

        long presentation = MediaFormatMapping.ToMicroseconds(Request.FrameRate.TimestampOf(FramesWritten));
        Guard(() => QueueVideo(presentation, endOfStream: false));
        FramesWritten++;
        progress?.Report(new VideoProgress(null, FramesWritten, Request.FrameRate.TimestampOf(FramesWritten), stopwatch.Elapsed));
    }

    public void WriteAudio(ReadOnlySpan<float> interleaved)
    {
        ThrowIfUnusable();
        if (audio == null) throw new InvalidOperationException("This writer was created without audio (StandardVideoRequest.HasAudio is false).");
        if (interleaved.Length % Request.AudioChannels != 0)
            throw new ArgumentException($"The sample count must be a multiple of the channel count ({Request.AudioChannels}).", nameof(interleaved));
        if (interleaved.Length == 0) return;

        byte[] pcm = ArrayPool<byte>.Shared.Rent(interleaved.Length * 2);
        try
        {
            PcmConversion.ToPcm16(interleaved, pcm);
            int total = interleaved.Length * 2;
            int position = 0;
            while (position < total)
            {
                int take = Math.Min(audioChunkBytes - audioStagingBytes, total - position);
                Buffer.BlockCopy(pcm, position, audioStaging, audioStagingBytes, take);
                audioStagingBytes += take;
                position += take;
                if (audioStagingBytes == audioChunkBytes)
                {
                    Guard(() => QueueAudioStaging(endOfStream: false));
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pcm);
        }

        AudioFramesWritten += interleaved.Length / Request.AudioChannels;
    }

    public void Finish()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (finished) return;
        if (FramesWritten == 0) throw new InvalidOperationException("No video frame has been written; an MP4 needs at least one.");
        finished = true;

        try
        {
            Guard(() =>
            {
                if (audio != null)
                {
                    if (AudioFramesWritten == 0 && audioStagingBytes == 0)
                    {
                        Array.Clear(audioStaging);
                        audioStagingBytes = audioChunkBytes;
                    }

                    QueueAudioStaging(endOfStream: true);
                }

                long end = MediaFormatMapping.ToMicroseconds(Request.FrameRate.TimestampOf(FramesWritten));
                QueueVideo(end, endOfStream: true);
                DrainUntilEnd();
                muxer.Stop();
            });

            AndroidMedia.ReleaseQuietly(video);
            AndroidMedia.ReleaseQuietly(audio);
            AndroidMedia.ReleaseQuietly(muxer, stop: false);
            video = null;
            audio = null;
            muxer = null;

            AndroidMedia.FinishMp4(Request.OutputPath, MuxerOutputType.Mpeg4, Request.FastStart, Request.Title, Request.CreationTime);
            completed = true;
        }
        catch
        {
            Abandon();
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (!completed) Abandon();
    }

    private void Abandon()
    {
        AndroidMedia.ReleaseQuietly(video);
        AndroidMedia.ReleaseQuietly(audio);
        AndroidMedia.ReleaseQuietly(muxer, stop: muxerStarted);
        video = null;
        audio = null;
        muxer = null;
        AndroidMedia.DeleteQuietly(Request.OutputPath);
        AndroidMedia.DeleteQuietly(Request.OutputPath + ".rewrite.tmp");
    }

    private void ThrowIfUnusable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (finished) throw new InvalidOperationException("The writer has been finished.");
        if (video == null) throw new VideoProcessingException("The writer failed earlier and cannot be used.");
    }

    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (JavaThrowable exception)
        {
            Abandon();
            throw new VideoProcessingException($"Writing '{Request.OutputPath}' failed: {AndroidMedia.Describe(exception)}", exception);
        }
    }

    private MediaCodec CreateVideoEncoder(StandardVideoRequest request, out string name)
    {
        bool hevc = request.Profile == StandardVideoProfile.Mp4HevcAac;
        string mime = hevc ? MediaFormatKeys.MimeHevc : MediaFormatKeys.MimeAvc;
        MediaFormat format = MediaFormat.CreateVideoFormat(mime, request.Width, request.Height);
        format.SetInteger(MediaFormatKeys.ColorFormat, MediaFormatKeys.ColorFormatYuv420Flexible);
        FrameRate rate = request.FrameRate;
        if (rate.Denominator == 1) format.SetInteger(MediaFormatKeys.FrameRate, rate.Numerator);
        else format.SetFloat(MediaFormatKeys.FrameRate, (float)rate.FramesPerSecond);
        format.SetFloat(MediaFormatKeys.IFrameInterval, KeyframeInterval.ToSeconds(request.KeyframeIntervalFrames, rate));
        format.SetInteger(MediaFormatKeys.ColorRange, MediaFormatMapping.ToMediaFormatColorRange(request.ColorRange));
        int standard = MediaFormatMapping.ToMediaFormatColorStandard(request.ColorMatrix);
        if (standard > 0)
        {
            format.SetInteger(MediaFormatKeys.ColorStandard, standard);
            format.SetInteger(MediaFormatKeys.ColorTransfer, MediaFormatMapping.ToMediaFormatColorTransfer(request.ColorMatrix));
        }

        int kilobits = QualityBitrateTable.ToKilobitsPerSecond(request.Quality, request.Width, request.Height, rate, hevc);
        format.SetInteger(MediaFormatKeys.BitRate, (int)Math.Min(int.MaxValue, (long)kilobits * 1000));

        MediaCodecInfo info = AndroidMedia.FindEncoder(format, mime)
            ?? throw new VideoProcessingException($"This device has no {mime} encoder.");
        MediaCodecInfo.CodecCapabilities capabilities = info.GetCapabilitiesForType(mime);
        MediaCodecInfo.VideoCapabilities videoCapabilities = capabilities?.VideoCapabilities;
        if (videoCapabilities != null)
        {
            if (!videoCapabilities.IsSizeSupported(request.Width, request.Height))
                throw new NotSupportedException($"The {info.Name} encoder does not support {request.Width}x{request.Height}.");
            int bitrate = (int)Math.Min(int.MaxValue, (long)kilobits * 1000);
            var range = videoCapabilities.BitrateRange;
            if (range != null)
            {
                int lower = ((global::Java.Lang.Integer)range.Lower).IntValue();
                int upper = ((global::Java.Lang.Integer)range.Upper).IntValue();
                bitrate = Math.Clamp(bitrate, lower, upper);
            }

            format.SetInteger(MediaFormatKeys.BitRate, bitrate);
        }

        MediaCodecInfo.EncoderCapabilities encoderCapabilities = capabilities?.EncoderCapabilities;
        if (encoderCapabilities != null && encoderCapabilities.IsBitrateModeSupported(BitrateMode.Vbr))
        {
            format.SetInteger(MediaFormatKeys.BitrateMode, MediaFormatKeys.BitrateModeVbr);
        }

        VideoBitrate = AndroidMedia.GetInt(format, MediaFormatKeys.BitRate);
        name = info.Name;
        MediaCodec codec = MediaCodec.CreateByCodecName(info.Name);
        try
        {
            codec.Configure(format, null, null, MediaCodecConfigFlags.Encode);
            codec.Start();
        }
        catch
        {
            AndroidMedia.ReleaseQuietly(codec);
            throw;
        }

        return codec;
    }

    private static MediaCodec CreateAudioEncoder(StandardVideoRequest request)
    {
        MediaFormat format = MediaFormat.CreateAudioFormat(MediaFormatKeys.MimeAac, request.AudioSampleRate, request.AudioChannels);
        format.SetInteger(MediaFormatKeys.AacProfile, MediaFormatKeys.AacObjectLc);
        format.SetInteger(MediaFormatKeys.BitRate, request.AudioBitrateKilobitsPerSecond * 1000);
        format.SetInteger(MediaFormatKeys.MaxInputSize, AudioChunkFrames * request.AudioChannels * 2 * 4);
        format.SetInteger(MediaFormatKeys.PcmEncoding, MediaFormatKeys.PcmEncoding16Bit);

        MediaCodecInfo info = AndroidMedia.FindEncoder(format, MediaFormatKeys.MimeAac)
            ?? throw new VideoProcessingException("This device has no AAC encoder.");
        MediaCodec codec = MediaCodec.CreateByCodecName(info.Name);
        try
        {
            codec.Configure(format, null, null, MediaCodecConfigFlags.Encode);
            codec.Start();
        }
        catch
        {
            AndroidMedia.ReleaseQuietly(codec);
            throw;
        }

        return codec;
    }

    // Queues the picture buffer (or the end-of-stream marker) into the video encoder, draining while it is full.
    private void QueueVideo(long presentationMicroseconds, bool endOfStream)
    {
        int index = WaitForInput(video);
        if (endOfStream)
        {
            video.QueueInputBuffer(index, 0, 0, presentationMicroseconds, MediaCodecBufferFlags.EndOfStream);
            return;
        }

        int capacity = video.GetInputBuffer(index).Capacity();
        using (global::Android.Media.Image image = video.GetInputImage(index))
        {
            if (image != null)
            {
                AndroidImageCopy.WriteI420(picture, Request.Width, Request.Height, image);
                video.QueueInputBuffer(index, 0, capacity, presentationMicroseconds, MediaCodecBufferFlags.None);
                DrainAll(0);
                return;
            }
        }

        // No Image: write the classic planar layout the encoder's input format describes.
        ByteBuffer buffer = video.GetInputBuffer(index);
        int size = WritePlanarFallback(buffer);
        video.QueueInputBuffer(index, 0, size, presentationMicroseconds, MediaCodecBufferFlags.None);
        DrainAll(0);
    }

    private int WritePlanarFallback(ByteBuffer buffer)
    {
        MediaFormat format = video.InputFormat;
        int colorFormat = AndroidMedia.GetInt(format, MediaFormatKeys.ColorFormat);
        int width = Request.Width;
        int height = Request.Height;
        int luma = width * height;
        int chroma = YuvConversion.ChromaSize(width, height);
        if (colorFormat == MediaFormatKeys.ColorFormatYuv420Planar)
        {
            AndroidMedia.CopyTo(picture, 0, picture.Length, buffer, 0);
            return picture.Length;
        }

        if (colorFormat == MediaFormatKeys.ColorFormatYuv420SemiPlanar)
        {
            byte[] nv12 = ArrayPool<byte>.Shared.Rent(luma + 2 * chroma);
            try
            {
                YuvConversion.I420ToNv12(picture, width, height, nv12);
                AndroidMedia.CopyTo(nv12, 0, luma + 2 * chroma, buffer, 0);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(nv12);
            }

            return luma + 2 * chroma;
        }

        throw new VideoProcessingException($"The video encoder offers no input Image and its color format 0x{colorFormat:X} is not one the engine can write.");
    }

    // Queues the staged PCM chunk (and/or the end-of-stream marker) into the audio encoder.
    private void QueueAudioStaging(bool endOfStream)
    {
        int frameBytes = Request.AudioChannels * 2;
        if (audioStagingBytes > 0)
        {
            int index = WaitForInput(audio);
            ByteBuffer buffer = audio.GetInputBuffer(index);
            int size = Math.Min(audioStagingBytes, buffer.Capacity() / frameBytes * frameBytes);
            AndroidMedia.CopyTo(audioStaging, 0, size, buffer, 0);
            long presentation = audioFramesQueued * 1_000_000L / Request.AudioSampleRate;
            audioFramesQueued += size / frameBytes;
            bool last = endOfStream && size == audioStagingBytes;
            audio.QueueInputBuffer(index, 0, size, presentation, last ? MediaCodecBufferFlags.EndOfStream : MediaCodecBufferFlags.None);
            if (size < audioStagingBytes) Buffer.BlockCopy(audioStaging, size, audioStaging, 0, audioStagingBytes - size);
            audioStagingBytes -= size;
            DrainAll(0);
            if (audioStagingBytes > 0 || (endOfStream && !last)) QueueAudioStaging(endOfStream);
            return;
        }

        if (endOfStream)
        {
            int index = WaitForInput(audio);
            long presentation = audioFramesQueued * 1_000_000L / Request.AudioSampleRate;
            audio.QueueInputBuffer(index, 0, 0, presentation, MediaCodecBufferFlags.EndOfStream);
        }
    }

    private int WaitForInput(MediaCodec codec)
    {
        Stopwatch waiting = Stopwatch.StartNew();
        while (true)
        {
            int index = codec.DequeueInputBuffer(AndroidMedia.DequeueTimeoutMicroseconds);
            if (index >= 0) return index;
            DrainAll(0);
            if (waiting.Elapsed > AndroidMedia.StallTimeout)
                throw new VideoProcessingException($"The encoder stopped accepting input (no free buffer for {AndroidMedia.StallTimeout.TotalSeconds:0} s).");
        }
    }

    private void DrainAll(long timeoutMicroseconds)
    {
        Drain(video, isVideo: true, timeoutMicroseconds);
        if (audio != null) Drain(audio, isVideo: false, timeoutMicroseconds);
    }

    private void DrainUntilEnd()
    {
        Stopwatch sinceProgress = Stopwatch.StartNew();
        while (!videoEnded || (audio != null && !audioEnded))
        {
            int before = pending.Count;
            bool progressed = false;
            if (!videoEnded) progressed |= Drain(video, isVideo: true, AndroidMedia.DequeueTimeoutMicroseconds);
            if (audio != null && !audioEnded) progressed |= Drain(audio, isVideo: false, AndroidMedia.DequeueTimeoutMicroseconds);
            if (progressed || pending.Count != before) sinceProgress.Restart();
            if (sinceProgress.Elapsed > AndroidMedia.StallTimeout)
                throw new VideoProcessingException($"The encoders did not finish (no progress for {AndroidMedia.StallTimeout.TotalSeconds:0} s).");
        }

        if (!muxerStarted) throw new VideoProcessingException("The encoders finished without announcing their output formats.");
    }

    // Moves every ready output buffer of one encoder into the muxer (or the pending list before the muxer starts).
    private bool Drain(MediaCodec codec, bool isVideo, long timeoutMicroseconds)
    {
        bool progressed = false;
        while (true)
        {
            int index = codec.DequeueOutputBuffer(bufferInfo, timeoutMicroseconds);
            if (index == (int)MediaCodecInfoState.TryAgainLater) return progressed;

            if (index == (int)MediaCodecInfoState.OutputFormatChanged)
            {
                if (muxerStarted) throw new VideoProcessingException("An encoder changed its output format after the muxer started.");
                int track = muxer.AddTrack(codec.OutputFormat);
                if (isVideo) videoTrack = track;
                else audioTrack = track;
                StartMuxerWhenReady();
                progressed = true;
                continue;
            }

            if (index < 0) continue;

            progressed = true;
            bool endOfStream = (bufferInfo.Flags & MediaCodecBufferFlags.EndOfStream) != 0;
            bool config = (bufferInfo.Flags & MediaCodecBufferFlags.CodecConfig) != 0;
            if (!config && bufferInfo.Size > 0)
            {
                ByteBuffer data = codec.GetOutputBuffer(index);
                MediaCodecBufferFlags flags = bufferInfo.Flags & ~MediaCodecBufferFlags.EndOfStream;
                if (muxerStarted)
                {
                    WriteSample(isVideo ? videoTrack : audioTrack, data, bufferInfo.Offset, bufferInfo.Size, bufferInfo.PresentationTimeUs, flags);
                }
                else
                {
                    byte[] copy = new byte[bufferInfo.Size];
                    AndroidMedia.CopyFrom(data, bufferInfo.Offset, copy, 0, copy.Length);
                    pending.Add(new PendingSample(isVideo, copy, bufferInfo.PresentationTimeUs, flags));
                }
            }

            codec.ReleaseOutputBuffer(index, false);
            if (endOfStream)
            {
                if (isVideo) videoEnded = true;
                else audioEnded = true;
                return true;
            }
        }
    }

    private void StartMuxerWhenReady()
    {
        if (videoTrack < 0 || (audio != null && audioTrack < 0)) return;
        muxer.Start();
        muxerStarted = true;
        foreach (PendingSample sample in pending)
        {
            ByteBuffer buffer = ByteBuffer.AllocateDirect(sample.Data.Length);
            AndroidMedia.CopyTo(sample.Data, 0, sample.Data.Length, buffer, 0);
            WriteSample(sample.IsVideo ? videoTrack : audioTrack, buffer, 0, sample.Data.Length, sample.PresentationMicroseconds, sample.Flags);
        }

        pending.Clear();
    }

    private void WriteSample(int track, ByteBuffer data, int offset, int size, long presentationMicroseconds, MediaCodecBufferFlags flags)
    {
        var info = new MediaCodec.BufferInfo();
        info.Set(offset, size, presentationMicroseconds, flags);
        data.Position(offset);
        data.Limit(offset + size);
        muxer.WriteSampleData(track, data, info);
    }

    private sealed class PendingSample
    {
        internal PendingSample(bool isVideo, byte[] data, long presentationMicroseconds, MediaCodecBufferFlags flags)
        {
            IsVideo = isVideo;
            Data = data;
            PresentationMicroseconds = presentationMicroseconds;
            Flags = flags;
        }

        internal bool IsVideo { get; }

        internal byte[] Data { get; }

        internal long PresentationMicroseconds { get; }

        internal MediaCodecBufferFlags Flags { get; }
    }
}
