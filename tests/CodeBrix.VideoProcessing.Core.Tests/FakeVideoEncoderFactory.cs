using System;
using System.Collections.Generic;
using CodeBrix.VideoProcessing.Core.Encoding;

namespace CodeBrix.VideoProcessing.Core.Tests;

/// <summary>
/// An in-memory encoder package for the contract tests: it makes <see cref="FakeVideoEncoder" />s that hold
/// <see cref="LookAhead" /> frames back before releasing a packet, like a real encoder with look-ahead.
/// </summary>
internal sealed class FakeVideoEncoderFactory : IVideoEncoderFactory
{
    public FakeVideoEncoderFactory(string codecId = VideoCodecIds.Av1, string displayName = "fake", VideoEncoderCapabilities capabilities = null, int lookAhead = 2)
    {
        CodecId = codecId;
        DisplayName = displayName;
        Capabilities = capabilities ?? new VideoEncoderCapabilities
        {
            PixelLayouts = VideoEncoderPixelLayouts.I420 | VideoEncoderPixelLayouts.I444 | VideoEncoderPixelLayouts.Gray,
            BitDepths = VideoEncoderBitDepths.Depth8 | VideoEncoderBitDepths.Depth10,
            SupportsLossless = true,
            SupportsForcedKeyFrames = true
        };
        LookAhead = lookAhead;
    }

    public string CodecId { get; }

    public string DisplayName { get; }

    public VideoEncoderCapabilities Capabilities { get; }

    public int LookAhead { get; }

    public List<FakeVideoEncoder> Created { get; } = new List<FakeVideoEncoder>();

    public IVideoEncoder Create(VideoEncoderRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.Validate();
        Capabilities.ThrowIfUnsupported(request);

        var encoder = new FakeVideoEncoder(this, request.Clone());
        Created.Add(encoder);
        return encoder;
    }
}
