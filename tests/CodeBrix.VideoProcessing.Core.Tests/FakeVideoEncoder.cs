using System;
using System.Collections.Generic;
using CodeBrix.VideoProcessing.Core.Encoding;
using CodeBrix.VideoProcessing.Core.Frames;

namespace CodeBrix.VideoProcessing.Core.Tests;

/// <summary>
/// A fake encoder: each packet is two bytes - the low byte of the frame number and the first byte of the frame's Y
/// plane - so a test can see which picture went into which packet. The configuration record appears with the first
/// packet, as it does for encoders that only know their sequence header once they have coded a frame.
/// </summary>
internal sealed class FakeVideoEncoder : IVideoEncoder
{
    private readonly FakeVideoEncoderFactory factory;
    private readonly Queue<(long Index, byte FirstByte, bool Forced)> pending = new Queue<(long, byte, bool)>();
    private readonly Queue<EncodedVideoPacket> ready = new Queue<EncodedVideoPacket>();
    private bool flushed;

    public FakeVideoEncoder(FakeVideoEncoderFactory factory, VideoEncoderRequest request)
    {
        this.factory = factory;
        Request = request;
    }

    public VideoEncoderRequest Request { get; }

    public ReadOnlyMemory<byte> CodecConfigurationRecord { get; private set; }

    public long FramesSubmitted { get; private set; }

    public long PacketsProduced { get; private set; }

    public bool Disposed { get; private set; }

    public void Encode(in VideoFrameData frame, bool forceKeyFrame)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        if (flushed) throw new InvalidOperationException("The encoder has been flushed.");
        if (frame.IsEmpty) throw new ArgumentException("The frame is empty.", nameof(frame));
        if (frame.Width != Request.Width || frame.Height != Request.Height || frame.Layout != Request.PixelLayout || frame.BitDepth != Request.BitDepth)
            throw new ArgumentException($"The frame ({frame}) does not match the request.", nameof(frame));
        if (forceKeyFrame && !factory.Capabilities.SupportsForcedKeyFrames)
            throw new NotSupportedException("This encoder cannot force keyframes.");

        // Copy what is kept: the caller may reuse the frame's memory as soon as this returns.
        pending.Enqueue((FramesSubmitted, frame.Y.GetRow(0)[0], forceKeyFrame));
        FramesSubmitted++;
        while (pending.Count > factory.LookAhead) Release();
    }

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        if (flushed) return;
        flushed = true;
        while (pending.Count > 0) Release();
    }

    public bool TryReadPacket(out EncodedVideoPacket packet)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        if (ready.Count == 0)
        {
            packet = default;
            return false;
        }

        packet = ready.Dequeue();
        PacketsProduced++;
        return true;
    }

    public void Dispose() => Disposed = true;

    private void Release()
    {
        (long index, byte firstByte, bool forced) = pending.Dequeue();
        int interval = Request.MaxKeyFrameInterval;
        bool key = index == 0 || forced || (interval > 0 && index % interval == 0);
        ready.Enqueue(new EncodedVideoPacket(new byte[] { (byte)index, firstByte }, Request.FrameRate.TimestampOf(index), index, key));
        if (CodecConfigurationRecord.IsEmpty) CodecConfigurationRecord = new byte[] { 0x81, 0x00, 0x0C, 0x00 };
    }
}
