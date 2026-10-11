using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using global::Android.Media;
using ByteBuffer = global::Java.Nio.ByteBuffer;
using JavaThrowable = global::Java.Lang.Throwable;

namespace CodeBrix.VideoProcessing.Android.Engine;

/// <summary>
/// Decodes the first audio stream of a file with MediaExtractor + a MediaCodec decoder (synchronous mode, on the
/// caller's thread) into interleaved 32-bit float at the requested rate and channel count.
/// </summary>
/// <remarks>
/// The decoder's PCM (16-bit, float, 8-, 24- or 32-bit, whichever its output format says) is converted to float,
/// re-mixed to the delivered channel count (<see cref="AudioChannelMixer" />) and resampled by LINEAR INTERPOLATION
/// (<see cref="AudioResampler" />) when the delivered rate differs from the decoder's. A start time or seek position
/// is honoured to the sample: the extractor seeks to the sync sample at or before it and the decoded samples before
/// it are dropped.
/// </remarks>
internal sealed class AndroidAudioSampleSource : IAudioSampleSource
{
    private readonly string path;
    private readonly int trackIndex;
    private readonly CancellationToken cancellationToken;
    private readonly MediaCodec.BufferInfo bufferInfo = new MediaCodec.BufferInfo();
    private readonly List<float> ready = new List<float>();

    private MediaExtractor extractor;
    private MediaCodec decoder;
    private AudioResampler resampler;
    private int sourceRate;
    private int sourceChannels;
    private int pcmEncoding = MediaFormatKeys.PcmEncoding16Bit;
    private int readyPosition;
    private long startMicroseconds;
    private bool inputDone;
    private bool ended;
    private bool disposed;

    internal AndroidAudioSampleSource(string path, AudioStreamInfo stream, AudioSourceOptions options, CancellationToken cancellationToken = default)
    {
        this.path = path;
        this.cancellationToken = cancellationToken;
        trackIndex = stream.Index;

        int sampleRate = options.SampleRate > 0 ? options.SampleRate : stream.SampleRate;
        int channels = options.Channels > 0 ? options.Channels : stream.Channels;
        if (sampleRate <= 0 || channels <= 0)
            throw new MediaFormatException($"The audio stream of '{path}' has no known sample rate or channel count; set them in AudioSourceOptions.");

        Info = new AudioSourceInfo
        {
            SampleRate = sampleRate,
            Channels = channels,
            Duration = stream.Duration,
            SourceCodecName = stream.CodecName
        };

        sourceRate = stream.SampleRate > 0 ? stream.SampleRate : sampleRate;
        sourceChannels = stream.Channels > 0 ? stream.Channels : channels;

        try
        {
            extractor = AndroidMedia.OpenExtractor(path);
            extractor.SelectTrack(trackIndex);
            MediaFormat format = extractor.GetTrackFormat(trackIndex);
            decoder = AndroidMedia.CreateDecoder(format, path, AndroidMedia.GetString(format, MediaFormatKeys.Mime) ?? "audio");
        }
        catch (JavaThrowable exception)
        {
            Release();
            throw new VideoProcessingException($"The audio of '{path}' could not be opened for decoding: {AndroidMedia.Describe(exception)}", exception);
        }
        catch
        {
            Release();
            throw;
        }

        Seek(options.StartTime);
    }

    public AudioSourceInfo Info { get; }

    public bool CanSeek => true;

    public int Read(Span<float> interleaved)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (interleaved.Length % Info.Channels != 0)
            throw new ArgumentException($"The buffer length must be a multiple of the channel count ({Info.Channels}).", nameof(interleaved));

        int written = 0;
        try
        {
            while (written < interleaved.Length)
            {
                int available = ready.Count - readyPosition;
                if (available > 0)
                {
                    int take = Math.Min(available, interleaved.Length - written);
                    CollectionsMarshal.AsSpan(ready).Slice(readyPosition, take).CopyTo(interleaved.Slice(written));
                    readyPosition += take;
                    written += take;
                    continue;
                }

                ready.Clear();
                readyPosition = 0;
                if (ended) break;
                cancellationToken.ThrowIfCancellationRequested();
                DecodeChunk();
            }
        }
        catch (JavaThrowable exception)
        {
            throw new VideoProcessingException($"Decoding the audio of '{path}' failed: {AndroidMedia.Describe(exception)}", exception);
        }

        return written / Info.Channels;
    }

    public void Seek(TimeSpan position)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (position < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(position), position, "The position cannot be negative.");

        startMicroseconds = MediaFormatMapping.ToMicroseconds(position);
        try
        {
            extractor.SeekTo(startMicroseconds, MediaExtractorSeekTo.PreviousSync);
            decoder.Flush();
        }
        catch (JavaThrowable exception)
        {
            throw new VideoProcessingException($"Seeking in the audio of '{path}' failed: {AndroidMedia.Describe(exception)}", exception);
        }

        ready.Clear();
        readyPosition = 0;
        inputDone = false;
        ended = false;
        resampler = new AudioResampler(sourceRate, Info.SampleRate, Info.Channels);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Release();
    }

    private void Release()
    {
        AndroidMedia.ReleaseQuietly(decoder);
        AndroidMedia.ReleaseQuietly(extractor);
        decoder = null;
        extractor = null;
    }

    // Feeds the decoder and converts ONE output buffer into delivered samples, or marks the end.
    private void DecodeChunk()
    {
        Stopwatch sinceProgress = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!inputDone && FeedInput()) sinceProgress.Restart();

            int index = decoder.DequeueOutputBuffer(bufferInfo, AndroidMedia.DequeueTimeoutMicroseconds);
            if (index >= 0)
            {
                bool endOfStream = (bufferInfo.Flags & MediaCodecBufferFlags.EndOfStream) != 0;
                if (bufferInfo.Size > 0) Convert(decoder.GetOutputBuffer(index), bufferInfo.Offset, bufferInfo.Size, bufferInfo.PresentationTimeUs);
                decoder.ReleaseOutputBuffer(index, false);
                if (endOfStream)
                {
                    resampler.Flush(ready);
                    ended = true;
                }

                return;
            }

            if (index == (int)MediaCodecInfoState.OutputFormatChanged)
            {
                MediaFormat format = decoder.OutputFormat;
                int rate = AndroidMedia.GetInt(format, MediaFormatKeys.SampleRate);
                int channels = AndroidMedia.GetInt(format, MediaFormatKeys.ChannelCount);
                pcmEncoding = AndroidMedia.GetInt(format, MediaFormatKeys.PcmEncoding, MediaFormatKeys.PcmEncoding16Bit);
                if (channels > 0) sourceChannels = channels;
                if (rate > 0 && rate != sourceRate)
                {
                    sourceRate = rate;
                    resampler = new AudioResampler(sourceRate, Info.SampleRate, Info.Channels);
                }

                sinceProgress.Restart();
            }

            if (sinceProgress.Elapsed > AndroidMedia.StallTimeout)
            {
                throw new VideoProcessingException(
                    $"The audio decoder for '{path}' stopped producing samples (no progress for {AndroidMedia.StallTimeout.TotalSeconds:0} s).");
            }
        }
    }

    private bool FeedInput()
    {
        int index = decoder.DequeueInputBuffer(0);
        if (index < 0) return false;

        ByteBuffer input = decoder.GetInputBuffer(index);
        int size = extractor.ReadSampleData(input, 0);
        if (size < 0)
        {
            decoder.QueueInputBuffer(index, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
            inputDone = true;
            return true;
        }

        decoder.QueueInputBuffer(index, 0, size, extractor.SampleTime, MediaCodecBufferFlags.None);
        extractor.Advance();
        return true;
    }

    // PCM bytes -> float (source channels) -> drop samples before the start -> mix -> resample -> ready.
    private void Convert(ByteBuffer buffer, int offset, int size, long presentationMicroseconds)
    {
        int bytesPerSample = PcmConversion.BytesPerSample(pcmEncoding);
        int frameBytes = bytesPerSample * sourceChannels;
        int frames = size / frameBytes;
        if (frames <= 0) return;

        byte[] bytes = ArrayPool<byte>.Shared.Rent(frames * frameBytes);
        float[] source = ArrayPool<float>.Shared.Rent(frames * sourceChannels);
        float[] mixed = ArrayPool<float>.Shared.Rent(frames * Info.Channels);
        try
        {
            AndroidMedia.CopyFrom(buffer, offset, bytes, 0, frames * frameBytes);
            PcmConversion.ToFloat(bytes.AsSpan(0, frames * frameBytes), pcmEncoding, source);

            int skip = 0;
            if (presentationMicroseconds < startMicroseconds)
            {
                long early = (startMicroseconds - presentationMicroseconds) * sourceRate / 1_000_000;
                skip = (int)Math.Min(frames, early);
            }

            int kept = frames - skip;
            if (kept <= 0) return;
            AudioChannelMixer.Mix(source.AsSpan(skip * sourceChannels, kept * sourceChannels), sourceChannels, mixed, Info.Channels, kept);
            resampler.Process(mixed.AsSpan(0, kept * Info.Channels), ready);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
            ArrayPool<float>.Shared.Return(source);
            ArrayPool<float>.Shared.Return(mixed);
        }
    }
}
