namespace CodeBrix.VideoProcessing.Core.Output;

/// <summary>The kind of standard video file an engine writes.</summary>
public enum StandardVideoProfile
{
    /// <summary>MP4 with H.264 (AVC) video, 8-bit 4:2:0, and AAC-LC audio - plays everywhere. The default, and mandatory for every engine.</summary>
    Mp4H264Aac = 0,

    /// <summary>MP4 with H.265 (HEVC) video, 8-bit 4:2:0, and AAC-LC audio. Needs <see cref="VideoProcessorCapabilities.HevcOutput" />.</summary>
    Mp4HevcAac = 1
}
