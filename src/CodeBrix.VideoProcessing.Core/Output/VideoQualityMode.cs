namespace CodeBrix.VideoProcessing.Core.Output;

/// <summary>How a <see cref="VideoQuality" /> asks the encoder to spend bits.</summary>
public enum VideoQualityMode
{
    /// <summary>Let the engine choose (the default value of <see cref="VideoQuality" />).</summary>
    EngineDefault = 0,

    /// <summary>Hold a constant perceived quality (x264/x265 "CRF" style), letting the bitrate vary.</summary>
    ConstantQuality = 1,

    /// <summary>Aim for an average bitrate.</summary>
    TargetBitrate = 2
}
