using System;

namespace CodeBrix.VideoProcessing.Core.Encoding;

/// <summary>The sample bit depths an encoder accepts (<see cref="VideoEncoderCapabilities.BitDepths" />).</summary>
[Flags]
public enum VideoEncoderBitDepths
{
    /// <summary>No depth.</summary>
    None = 0,

    /// <summary>8 bits per sample.</summary>
    Depth8 = 1 << 0,

    /// <summary>10 bits per sample.</summary>
    Depth10 = 1 << 1,

    /// <summary>12 bits per sample.</summary>
    Depth12 = 1 << 2
}
