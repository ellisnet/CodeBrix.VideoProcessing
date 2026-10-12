using System;
using System.Collections.Generic;
using System.Diagnostics;
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
/// Decodes the first video stream of a file with MediaExtractor + a MediaCodec decoder (synchronous mode, on the
/// caller's thread) into 8-bit or 10-bit frames, with the same semantics as the desktop engine's frame source.
/// </summary>
/// <remarks>
/// <para>
/// CONSTANT RATE: every decoded picture is copied (through the Image API) into a tightly packed I420 buffer and
/// placed in the frame grid of <see cref="VideoSourceInfo.FrameRate" /> by
/// <see cref="ConstantRateScheduler{T}" /> - nearest slot, repeats for gaps, latest wins - so frame N is always
/// <c>FrameRate.TimestampOf(N)</c> and FrameIndex counts from the start of the stream (slot 0 is the stream's first
/// sample time), also after a seek.
/// </para>
/// <para>
/// LAYOUTS: I420 is the decoded buffer itself; Gray is its Y plane (exactly I420's luma, in the delivered range);
/// NV12 and RGBA are converted from it into one more reused buffer (RGBA with the stream's matrix and range;
/// <see cref="VideoSourceInfo.ColorMatrix" /> then reports <see cref="VideoColorMatrix.Rgb" />, the matrix of the
/// delivered samples, as the contract says). Pictures stay in STORED orientation;
/// <see cref="VideoSourceInfo.RotationDegrees" /> says how to display them.
/// </para>
/// <para>
/// BIT DEPTH (<see cref="DecoderOutputMode" />): at 8 bits the decoder is asked for flexible YUV 4:2:0 - or, for a
/// known 10-bit stream, for P010 where a decoder lists it (falling back to flexible) - and 10-bit (P010) output is cut
/// to its top 8 bits, reported through <see cref="VideoSourceInfo.SourceBitDepth" />. At 10 bits
/// the picture buffers are 10-bit I420 (two bytes per sample, low bits): a 10-bit or unknown-depth source REQUIRES the
/// decoder's P010 output - the decoder is configured for it and the first picture is decoded while opening, so a
/// decoder that refuses P010 or answers with 8-bit pictures fails OpenVideo with NotSupportedException naming the
/// codec and the stream (a later 8-bit picture fails TryReadFrame the same way); a known 8-bit source is decoded as
/// usual and widened (value &lt;&lt; 2). Gray at 10 bits is the Y plane; NV12 at 10 bits interleaves Cb / Cr sample
/// by sample.
/// </para>
/// <para>
/// OWNERSHIP: three picture buffers circulate (the frame on screen, a look-ahead, one being decoded); a frame is a
/// view over one of them and is valid until the next TryReadFrame, Seek or Dispose, as the contract says.
/// </para>
/// <para>
/// SEEK: MediaExtractor seeks to the sync sample at or before the target, the decoder is flushed, and pictures before
/// the target slot are decoded and dropped by the scheduler.
/// </para>
/// </remarks>
internal sealed class AndroidVideoFrameSource : IVideoFrameSource
{
    private readonly string path;
    private readonly int trackIndex;
    private readonly int width;
    private readonly int height;
    private readonly int pictureSize;
    private readonly long maximumFrames;
    private readonly VideoColorMatrix sourceMatrix;
    private readonly DecoderOutputMode mode;
    private readonly int bytesPerSample;
    private readonly string streamLabel;
    private readonly CancellationToken cancellationToken;
    private readonly Stack<byte[]> pool = new Stack<byte[]>();
    private readonly MediaCodec.BufferInfo bufferInfo = new MediaCodec.BufferInfo();

    private MediaExtractor extractor;
    private MediaCodec decoder;
    private MediaFormat outputFormat;
    private ConstantRateScheduler<byte[]> scheduler;
    private byte[] conversion;
    private byte[] narrow;
    private string decoderName;
    private long originMicroseconds;
    private long delivered;
    private bool inputDone;
    private bool disposed;

    internal AndroidVideoFrameSource(string path, VideoStreamInfo stream, VideoSourceOptions options, long maximumFrames = 0, CancellationToken cancellationToken = default)
    {
        this.path = path;
        this.maximumFrames = maximumFrames;
        this.cancellationToken = cancellationToken;
        trackIndex = stream.Index;
        width = stream.Width;
        height = stream.Height;

        if (width <= 0 || height <= 0) throw new MediaFormatException($"The video stream of '{path}' has no known size.");
        if (!stream.FrameRate.IsSpecified) throw new MediaFormatException($"The video stream of '{path}' has no known frame rate.");

        VideoPixelLayout layout = options.PixelLayout;
        int bitDepth = layout == VideoPixelLayout.Rgba ? 8 : options.BitDepth;
        mode = HighBitDepthConversion.ChooseMode(bitDepth, stream.BitDepth);
        bytesPerSample = mode == DecoderOutputMode.EightBit ? 1 : 2;
        pictureSize = YuvConversion.I420Size(width, height) * bytesPerSample;
        streamLabel = $"{(stream.BitDepth > 0 ? stream.BitDepth + "-bit " : string.Empty)}{stream.CodecName ?? "video"} stream (track {stream.Index})";
        sourceMatrix = stream.ColorMatrix;
        Info = new VideoSourceInfo
        {
            Width = width,
            Height = height,
            FrameRate = stream.FrameRate,
            Duration = stream.Duration,
            EstimatedFrameCount = stream.FrameCount > 0 ? stream.FrameCount : stream.FrameRate.FrameCountFor(stream.Duration),
            PixelLayout = layout,
            BitDepth = bitDepth,
            SourceBitDepth = stream.BitDepth,
            SourceHasAlpha = false,
            HasAlpha = layout == VideoPixelLayout.Rgba,
            RotationDegrees = stream.RotationDegrees,
            ColorRange = stream.ColorRange == VideoColorRange.Full ? VideoColorRange.Full : VideoColorRange.Limited,
            // The matrix of the DELIVERED samples: the stream's for YUV / Gray, Rgb for RGBA (those samples are RGB).
            ColorMatrix = layout == VideoPixelLayout.Rgba ? VideoColorMatrix.Rgb : stream.ColorMatrix
        };

        try
        {
            extractor = AndroidMedia.OpenExtractor(path);
            extractor.SelectTrack(trackIndex);
            MediaFormat format = extractor.GetTrackFormat(trackIndex);
            extractor.SeekTo(0, MediaExtractorSeekTo.PreviousSync);
            originMicroseconds = Math.Max(0, extractor.SampleTime);
            string mime = AndroidMedia.GetString(format, MediaFormatKeys.Mime) ?? "video";
            if (mode == DecoderOutputMode.P010)
            {
                decoder = AndroidMedia.CreateP010Decoder(format, path, mime, streamLabel);
            }
            else if (HighBitDepthConversion.AsksForP010(mode, stream.BitDepth))
            {
                // 8-bit frames from a 10-bit stream: P010 output cut to its top 8 bits where a decoder offers it
                // (some hardware decoders cannot convert 10-bit pictures to flexible 8-bit buffers), else flexible.
                try
                {
                    decoder = AndroidMedia.CreateP010Decoder(format, path, mime, streamLabel);
                }
                catch (NotSupportedException)
                {
                    decoder = AndroidMedia.CreateDecoder(format, path, mime);
                }
            }
            else
            {
                decoder = AndroidMedia.CreateDecoder(format, path, mime);
            }

            decoderName = decoder.Name;
            outputFormat = decoder.OutputFormat;
        }
        catch (JavaThrowable exception)
        {
            Release();
            throw new VideoProcessingException($"The video of '{path}' could not be opened for decoding: {AndroidMedia.Describe(exception)}", exception);
        }
        catch
        {
            Release();
            throw;
        }

        scheduler = new ConstantRateScheduler<byte[]>(Info.FrameRate, originMicroseconds, 0, pool.Push);
        Seek(options.StartTime);

        if (mode == DecoderOutputMode.P010)
        {
            // Prove the decoder really outputs P010 for THIS stream before handing the source out: decode the first
            // picture now (the scheduler keeps it, so nothing is lost). 8-bit output throws NotSupportedException.
            try
            {
                DecodeOne();
            }
            catch (JavaThrowable exception)
            {
                Release();
                throw new VideoProcessingException($"The video of '{path}' could not be decoded: {AndroidMedia.Describe(exception)}", exception);
            }
            catch
            {
                Release();
                throw;
            }
        }
    }

    public VideoSourceInfo Info { get; }

    public bool CanSeek => true;

    public bool TryReadFrame(out VideoFrameData frame)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        frame = default;
        if (maximumFrames > 0 && delivered >= maximumFrames) return false;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (scheduler.TryDeliver(out byte[] picture, out long slot))
                {
                    delivered++;
                    frame = BuildFrame(picture, slot);
                    return true;
                }

                if (!scheduler.NeedsInput) return false;
                DecodeOne();
            }
        }
        catch (JavaThrowable exception)
        {
            throw new VideoProcessingException($"Decoding the video of '{path}' failed: {AndroidMedia.Describe(exception)}", exception);
        }
    }

    public void Seek(TimeSpan position)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (position < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(position), position, "The position cannot be negative.");

        FrameRate rate = Info.FrameRate;
        long slot = rate.FrameIndexAt(position);
        if (rate.TimestampOf(slot) < position) slot++;

        try
        {
            long seekMicroseconds = originMicroseconds + MediaFormatMapping.ToMicroseconds(rate.TimestampOf(slot));
            extractor.SeekTo(seekMicroseconds, MediaExtractorSeekTo.PreviousSync);
            decoder.Flush();
        }
        catch (JavaThrowable exception)
        {
            throw new VideoProcessingException($"Seeking in the video of '{path}' failed: {AndroidMedia.Describe(exception)}", exception);
        }

        inputDone = false;
        scheduler.Reset(slot);
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

    // Feeds the decoder and waits for ONE output picture (offered to the scheduler) or the end of the stream.
    private void DecodeOne()
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
                bool hasPicture = bufferInfo.Size > 0;
                if (hasPicture)
                {
                    byte[] picture = pool.Count > 0 ? pool.Pop() : new byte[pictureSize];
                    try
                    {
                        CopyPicture(index, picture);
                    }
                    catch
                    {
                        pool.Push(picture);
                        decoder.ReleaseOutputBuffer(index, false);
                        throw;
                    }

                    long presentation = bufferInfo.PresentationTimeUs;
                    decoder.ReleaseOutputBuffer(index, false);
                    scheduler.Offer(picture, presentation);
                    if (endOfStream) scheduler.EndOfInput();
                    return;
                }

                decoder.ReleaseOutputBuffer(index, false);
                if (endOfStream)
                {
                    scheduler.EndOfInput();
                    return;
                }

                sinceProgress.Restart();
            }
            else if (index == (int)MediaCodecInfoState.OutputFormatChanged)
            {
                outputFormat = decoder.OutputFormat;
                sinceProgress.Restart();
            }

            if (sinceProgress.Elapsed > AndroidMedia.StallTimeout)
            {
                throw new VideoProcessingException(
                    $"The video decoder for '{path}' stopped producing pictures (no progress for {AndroidMedia.StallTimeout.TotalSeconds:0} s).");
            }
        }
    }

    // Queues one compressed sample (or the end-of-stream marker) when the decoder has a free input buffer.
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

    private void CopyPicture(int index, byte[] picture)
    {
        if (mode != DecoderOutputMode.EightBit)
        {
            CopyPictureDeep(index, picture);
            return;
        }

        using (global::Android.Media.Image image = decoder.GetOutputImage(index))
        {
            if (image != null)
            {
                AndroidImageCopy.ReadI420(image, width, height, picture);
                return;
            }
        }

        ByteBuffer buffer = decoder.GetOutputBuffer(index);
        MediaFormat format = decoder.GetOutputFormat(index) ?? outputFormat;
        AndroidImageCopy.ReadI420FromBuffer(buffer, bufferInfo.Offset, format, width, height, picture);
    }

    // 10-bit frames: P010 pictures keep all ten bits; 8-bit pictures are widened - unless P010 is REQUIRED (a 10-bit or
    // unknown-depth source), when an 8-bit picture means the decoder cut the samples and the request is refused.
    private void CopyPictureDeep(int index, byte[] picture)
    {
        using (global::Android.Media.Image image = decoder.GetOutputImage(index))
        {
            if (image != null)
            {
                if (!AndroidImageCopy.IsP010(image) && mode == DecoderOutputMode.P010) throw EightBitOutput($"image format 0x{(int)image.Format:X}");
                AndroidImageCopy.ReadI420Deep(image, width, height, picture);
                return;
            }
        }

        ByteBuffer buffer = decoder.GetOutputBuffer(index);
        MediaFormat format = decoder.GetOutputFormat(index) ?? outputFormat;
        int colorFormat = AndroidMedia.GetInt(format, MediaFormatKeys.ColorFormat);
        if (colorFormat == MediaFormatKeys.ColorFormatYuvP010)
        {
            AndroidImageCopy.ReadP010FromBuffer(buffer, bufferInfo.Offset, format, width, height, picture);
            return;
        }

        if (mode == DecoderOutputMode.P010) throw EightBitOutput($"color format 0x{colorFormat:X}");
        narrow ??= new byte[YuvConversion.I420Size(width, height)];
        AndroidImageCopy.ReadI420FromBuffer(buffer, bufferInfo.Offset, format, width, height, narrow);
        HighBitDepthConversion.Widen(narrow, picture);
    }

    private NotSupportedException EightBitOutput(string what) => new NotSupportedException(
        $"The {decoderName} decoder delivered 8-bit pictures ({what}) instead of P010 for the {streamLabel} of '{path}', so it " +
        "cannot be delivered at 10 bits on this device. Open it with BitDepth = 8 (the samples are then cut to 8 bits and " +
        "VideoSourceInfo.IsBitDepthReduced says so).");

    private VideoFrameData BuildFrame(byte[] picture, long slot)
    {
        if (bytesPerSample == 2) return BuildDeepFrame(picture, slot);

        TimeSpan timestamp = Info.FrameRate.TimestampOf(slot);
        int luma = width * height;
        int chromaWidth = (width + 1) / 2;
        int chromaHeight = (height + 1) / 2;
        int chroma = chromaWidth * chromaHeight;
        var memory = new ReadOnlyMemory<byte>(picture);
        VideoPlane y = new VideoPlane(memory.Slice(0, luma), width, width, height);

        switch (Info.PixelLayout)
        {
            case VideoPixelLayout.Gray:
                return VideoFrameData.CreateGray(width, height, y, timestamp, slot);
            case VideoPixelLayout.Nv12:
                conversion ??= new byte[pictureSize];
                YuvConversion.I420ToNv12(picture, width, height, conversion);
                return VideoFrameData.CreateNv12(width, height,
                    new VideoPlane(new ReadOnlyMemory<byte>(conversion, 0, luma), width, width, height),
                    new VideoPlane(new ReadOnlyMemory<byte>(conversion, luma, 2 * chroma), 2 * chromaWidth, 2 * chromaWidth, chromaHeight),
                    timestamp, slot);
            case VideoPixelLayout.Rgba:
                conversion ??= new byte[4 * luma];
                YuvConversion.I420ToRgba(picture, width, height, sourceMatrix, Info.ColorRange, conversion);
                return VideoFrameData.CreateRgba(width, height, new VideoPlane(conversion, 4 * width, 4 * width, height), timestamp, slot);
            default:
                return VideoFrameData.CreateI420(width, height, y,
                    new VideoPlane(memory.Slice(luma, chroma), chromaWidth, chromaWidth, chromaHeight),
                    new VideoPlane(memory.Slice(luma + chroma, chroma), chromaWidth, chromaWidth, chromaHeight),
                    timestamp, slot);
        }
    }

    // 10-bit frames: every plane is two bytes per sample, so row lengths are twice the widths in samples.
    private VideoFrameData BuildDeepFrame(byte[] picture, long slot)
    {
        TimeSpan timestamp = Info.FrameRate.TimestampOf(slot);
        int bitDepth = Info.BitDepth;
        int lumaRow = 2 * width;
        int luma = lumaRow * height;
        int chromaWidth = (width + 1) / 2;
        int chromaHeight = (height + 1) / 2;
        int chromaRow = 2 * chromaWidth;
        int chroma = chromaRow * chromaHeight;
        var memory = new ReadOnlyMemory<byte>(picture);
        VideoPlane y = new VideoPlane(memory.Slice(0, luma), lumaRow, lumaRow, height);

        switch (Info.PixelLayout)
        {
            case VideoPixelLayout.Gray:
                return VideoFrameData.CreateGray(width, height, y, timestamp, slot, default, bitDepth);
            case VideoPixelLayout.Nv12:
                conversion ??= new byte[pictureSize];
                HighBitDepthConversion.I420ToNv12(picture, width, height, conversion);
                return VideoFrameData.CreateNv12(width, height,
                    new VideoPlane(new ReadOnlyMemory<byte>(conversion, 0, luma), lumaRow, lumaRow, height),
                    new VideoPlane(new ReadOnlyMemory<byte>(conversion, luma, 2 * chroma), 2 * chromaRow, 2 * chromaRow, chromaHeight),
                    timestamp, slot, default, bitDepth);
            default:
                return VideoFrameData.CreateI420(width, height, y,
                    new VideoPlane(memory.Slice(luma, chroma), chromaRow, chromaRow, chromaHeight),
                    new VideoPlane(memory.Slice(luma + chroma, chroma), chromaRow, chromaRow, chromaHeight),
                    timestamp, slot, default, bitDepth);
        }
    }
}
