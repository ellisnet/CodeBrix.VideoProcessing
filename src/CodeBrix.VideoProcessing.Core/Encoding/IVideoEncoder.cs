using System;
using CodeBrix.VideoProcessing.Core.Frames;

namespace CodeBrix.VideoProcessing.Core.Encoding;

/// <summary>
/// Encodes ONE video elementary stream: frames in, compressed packets out, through a PULL model.
/// </summary>
/// <remarks>
/// <para>
/// THE LOOP. Call <see cref="Encode" /> once per frame, in presentation order, and after each call drain
/// <see cref="TryReadPacket" /> until it returns false - an encoder may hold frames back (look-ahead), so a call may
/// produce no packet, one, or several. After the last frame call <see cref="Flush" />, then drain
/// <see cref="TryReadPacket" /> again: only then has every frame come out.
/// </para>
/// <code>
/// using IVideoEncoder encoder = VideoEncoders.Get(VideoCodecIds.Av1).Create(request);
/// foreach (VideoFrameData frame in frames)
/// {
///     encoder.Encode(frame, forceKeyFrame: false);
///     while (encoder.TryReadPacket(out EncodedVideoPacket packet)) Store(packet);
/// }
/// encoder.Flush();
/// while (encoder.TryReadPacket(out EncodedVideoPacket packet)) Store(packet);
/// </code>
/// <para>
/// PACKETS come out in decoding order, each stamped with the frame it presents. The encoder numbers frames itself:
/// the Nth frame given to <see cref="Encode" /> is frame N, at <c>Request.FrameRate.TimestampOf(N)</c>; the frame's
/// own <see cref="VideoFrameData.Timestamp" /> and <see cref="VideoFrameData.FrameIndex" /> are ignored. For AV1 every
/// packet is one temporal unit and decoding order equals presentation order.
/// </para>
/// <para>
/// SCOPE. An encoder knows nothing about containers, alpha streams, RGBA conversion or any file format: it does not
/// write files, it does not split alpha off a frame (an <see cref="VideoFrameData.A" /> plane is ignored - encode
/// alpha as its own stream with a second encoder), and it accepts only the layout and depth its request names.
/// </para>
/// <para>
/// THREADING: an encoder is a single-threaded object - drive it from one thread at a time (it may use worker
/// threads inside).
/// </para>
/// </remarks>
public interface IVideoEncoder : IDisposable
{
    /// <summary>The encoder's own (validated) copy of the request it was created from.</summary>
    VideoEncoderRequest Request { get; }

    /// <summary>
    /// The codec's decoder configuration record, as a container stores it: the AV1CodecConfigurationRecord ("av1C",
    /// including the sequence header OBU) for AV1, the AVCDecoderConfigurationRecord ("avcC") for H.264, the
    /// HEVCDecoderConfigurationRecord ("hvcC") for HEVC. EMPTY until the encoder has produced its sequence header:
    /// some encoders know it as soon as they are created, others only once they have produced the first packet - it
    /// is guaranteed to be available once <see cref="TryReadPacket" /> has returned the first packet. The memory
    /// belongs to the consumer and does not change afterwards.
    /// </summary>
    ReadOnlyMemory<byte> CodecConfigurationRecord { get; }

    /// <summary>The number of frames accepted by <see cref="Encode" /> so far.</summary>
    long FramesSubmitted { get; }

    /// <summary>The number of packets handed out by <see cref="TryReadPacket" /> so far.</summary>
    long PacketsProduced { get; }

    /// <summary>Submits the next frame.</summary>
    /// <param name="frame">
    /// The picture: the request's width, height, layout and bit depth. Its memory may be reused by the caller as soon
    /// as this returns - the encoder copies what it keeps.
    /// </param>
    /// <param name="forceKeyFrame">
    /// True to make this frame a keyframe. Needs <see cref="VideoEncoderCapabilities.SupportsForcedKeyFrames" />.
    /// </param>
    /// <exception cref="ArgumentException">The frame is empty, or its size, layout or bit depth does not match the request.</exception>
    /// <exception cref="NotSupportedException"><paramref name="forceKeyFrame" /> is true and the encoder cannot force keyframes.</exception>
    /// <exception cref="InvalidOperationException"><see cref="Flush" /> has been called.</exception>
    /// <exception cref="ObjectDisposedException">The encoder has been disposed.</exception>
    /// <exception cref="VideoEncodingException">The underlying encoder failed.</exception>
    void Encode(in VideoFrameData frame, bool forceKeyFrame);

    /// <summary>
    /// Tells the encoder no more frames are coming, so it can finish the frames it holds back. Drain
    /// <see cref="TryReadPacket" /> afterwards. A second call does nothing; <see cref="Encode" /> afterwards throws.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The encoder has been disposed.</exception>
    /// <exception cref="VideoEncodingException">The underlying encoder failed.</exception>
    void Flush();

    /// <summary>Takes the next finished packet, if there is one.</summary>
    /// <param name="packet">The packet, or the empty packet when none is ready.</param>
    /// <returns>
    /// True when a packet was returned. False means "nothing more for now" before <see cref="Flush" />, and "the stream
    /// is complete" after it.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The encoder has been disposed.</exception>
    /// <exception cref="VideoEncodingException">The underlying encoder failed.</exception>
    bool TryReadPacket(out EncodedVideoPacket packet);
}
