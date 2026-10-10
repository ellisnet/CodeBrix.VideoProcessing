using System;

namespace CodeBrix.VideoProcessing.Core.Encoding;

/// <summary>
/// One compressed packet from an <see cref="IVideoEncoder" />: the bytes of one coded picture (for AV1, one temporal
/// unit) in the codec's elementary-stream format, with its position.
/// </summary>
/// <remarks>
/// <see cref="Data" /> is a FRESH buffer the consumer owns - the encoder never touches it again. The default value
/// is the empty packet (<see cref="IsEmpty" />), returned by a failed <see cref="IVideoEncoder.TryReadPacket" />.
/// </remarks>
public readonly struct EncodedVideoPacket
{
    /// <summary>Creates a packet.</summary>
    /// <param name="data">The compressed bytes; not empty.</param>
    /// <param name="timestamp">The presentation time; not negative.</param>
    /// <param name="frameIndex">The zero-based index of the frame the packet presents; not negative.</param>
    /// <param name="isKeyFrame">True when decoding can start at this packet.</param>
    /// <exception cref="ArgumentException"><paramref name="data" /> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timestamp" /> or <paramref name="frameIndex" /> is negative.</exception>
    public EncodedVideoPacket(ReadOnlyMemory<byte> data, TimeSpan timestamp, long frameIndex, bool isKeyFrame)
    {
        if (data.IsEmpty) throw new ArgumentException("A packet needs at least one byte.", nameof(data));
        if (timestamp < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timestamp), timestamp, "The timestamp cannot be negative.");
        if (frameIndex < 0) throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, "The frame index cannot be negative.");

        Data = data;
        Timestamp = timestamp;
        FrameIndex = frameIndex;
        IsKeyFrame = isKeyFrame;
    }

    /// <summary>The compressed bytes: a fresh buffer the consumer owns.</summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>
    /// The presentation time: <c>Request.FrameRate.TimestampOf(FrameIndex)</c>, derived from the frame's submission
    /// index, never from the frame's own timestamp.
    /// </summary>
    public TimeSpan Timestamp { get; }

    /// <summary>
    /// The zero-based index of the frame this packet presents: the Nth frame given to
    /// <see cref="IVideoEncoder.Encode" /> is frame N.
    /// </summary>
    public long FrameIndex { get; }

    /// <summary>True when the packet is a keyframe (a random-access point: decoding can start here).</summary>
    public bool IsKeyFrame { get; }

    /// <summary>True for <c>default(EncodedVideoPacket)</c>, which holds no data.</summary>
    public bool IsEmpty => Data.IsEmpty;

    /// <inheritdoc />
    public override string ToString() =>
        IsEmpty ? "(empty packet)" : $"#{FrameIndex} @ {Timestamp}, {Data.Length} bytes{(IsKeyFrame ? ", key" : string.Empty)}";
}
