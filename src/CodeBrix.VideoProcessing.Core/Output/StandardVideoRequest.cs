using System;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;

namespace CodeBrix.VideoProcessing.Core.Output;

/// <summary>
/// The settings of a standard video file to write: <see cref="IVideoProcessor.CreateWriter" /> uses it as-is, and
/// <see cref="TranscodeRequest.Output" /> uses it with "keep the source's value" allowed for size, rate and audio
/// format.
/// </summary>
/// <remarks>
/// The output is always 8-bit 4:2:0 video at a CONSTANT frame rate - variable frame rate is out of scope - and the
/// input frames are always 8-bit I420, NV12 or RGBA (an encoder for other layouts and depths plugs in through
/// <see cref="Encoding.VideoEncoders" /> instead). The
/// colour range and matrix are TAGS describing the samples you supply; the engine does not convert YUV input
/// between them (it does use them when it converts RGBA input to YUV).
/// </remarks>
public sealed class StandardVideoRequest
{
    /// <summary>The file to write. An existing file is overwritten.</summary>
    public string OutputPath { get; set; }

    /// <summary>Container and codecs. Default <see cref="StandardVideoProfile.Mp4H264Aac" />.</summary>
    public StandardVideoProfile Profile { get; set; } = StandardVideoProfile.Mp4H264Aac;

    /// <summary>Width in pixels: positive and even. (Transcode: 0 keeps the source's.)</summary>
    public int Width { get; set; }

    /// <summary>Height in pixels: positive and even. (Transcode: 0 keeps the source's.)</summary>
    public int Height { get; set; }

    /// <summary>The CONSTANT frame rate; the engine stamps frame N at <c>FrameRate.TimestampOf(N)</c>. (Transcode: unspecified keeps the source's.)</summary>
    public FrameRate FrameRate { get; set; }

    /// <summary>
    /// The layout of the frames the caller will pass to <see cref="IStandardVideoWriter.WriteFrame" />. Default
    /// <see cref="VideoPixelLayout.I420" /> (mandatory for every engine); NV12 and RGBA need the matching capability.
    /// The writer takes 8-bit frames in these three layouts ONLY: <see cref="VideoPixelLayout.I444" /> and
    /// <see cref="VideoPixelLayout.Gray" /> fail <see cref="Validate" />, and frames deeper than 8 bits are refused by
    /// <see cref="IStandardVideoWriter.WriteFrame" />. Not used by a transcode.
    /// </summary>
    public VideoPixelLayout InputPixelLayout { get; set; } = VideoPixelLayout.I420;

    /// <summary>The quality / bitrate request. Default: the engine's choice.</summary>
    public VideoQuality Quality { get; set; }

    /// <summary>The maximum distance between keyframes, in frames; 0 (the default) lets the engine choose.</summary>
    public int KeyframeIntervalFrames { get; set; }

    /// <summary>The sample range of the video. Default <see cref="VideoColorRange.Limited" />.</summary>
    public VideoColorRange ColorRange { get; set; } = VideoColorRange.Limited;

    /// <summary>The YUV matrix of the video. Default <see cref="VideoColorMatrix.Bt709" />.</summary>
    public VideoColorMatrix ColorMatrix { get; set; } = VideoColorMatrix.Bt709;

    /// <summary>
    /// Display rotation METADATA: the clockwise rotation (0, 90, 180 or 270) a player should apply. The pictures
    /// themselves are not rotated. Default 0.
    /// </summary>
    public int RotationDegrees { get; set; }

    /// <summary>Put the MP4 index ("moov") before the media data, so playback can start before the whole file has arrived. Default true.</summary>
    public bool FastStart { get; set; } = true;

    /// <summary>Whether the file has a sound track. Default false. (Transcode: whether to keep the source's audio.)</summary>
    public bool HasAudio { get; set; }

    /// <summary>Audio samples per second per channel. Default 48000. (Transcode: 0 keeps the source's.)</summary>
    public int AudioSampleRate { get; set; } = 48000;

    /// <summary>Audio channels, 1..8. Default 2. (Transcode: 0 keeps the source's.)</summary>
    public int AudioChannels { get; set; } = 2;

    /// <summary>The AAC bitrate. Default 128.</summary>
    public int AudioBitrateKilobitsPerSecond { get; set; } = 128;

    /// <summary>An optional title tag.</summary>
    public string Title { get; set; }

    /// <summary>An optional creation-time tag.</summary>
    public DateTimeOffset? CreationTime { get; set; }

    /// <summary>Checks the request for <see cref="IVideoProcessor.CreateWriter" />: every value must be given.</summary>
    /// <exception cref="ArgumentException">A value is missing or out of range; the message names it.</exception>
    public void Validate() => ValidateCore(allowKeep: false);

    /// <summary>Makes a member-by-member copy.</summary>
    /// <returns>The copy.</returns>
    public StandardVideoRequest Clone() => (StandardVideoRequest)MemberwiseClone();

    internal void ValidateCore(bool allowKeep)
    {
        if (string.IsNullOrWhiteSpace(OutputPath)) throw new ArgumentException("The output path is required.", nameof(OutputPath));
        if (!Enum.IsDefined(Profile)) throw new ArgumentOutOfRangeException(nameof(Profile), Profile, "Unknown profile.");
        if (!Enum.IsDefined(InputPixelLayout)) throw new ArgumentOutOfRangeException(nameof(InputPixelLayout), InputPixelLayout, "Unknown pixel layout.");
        if (InputPixelLayout != VideoPixelLayout.I420 && InputPixelLayout != VideoPixelLayout.Nv12 && InputPixelLayout != VideoPixelLayout.Rgba)
        {
            throw new ArgumentOutOfRangeException(nameof(InputPixelLayout), InputPixelLayout,
                "The standard video writer takes 8-bit I420, NV12 or RGBA frames only; convert I444 or Gray frames first.");
        }

        bool keepSize = allowKeep && Width == 0 && Height == 0;
        if (!keepSize)
        {
            CheckDimension(Width, nameof(Width), allowKeep);
            CheckDimension(Height, nameof(Height), allowKeep);
        }

        if (!FrameRate.IsSpecified && !allowKeep) throw new ArgumentException("The frame rate is required.", nameof(FrameRate));

        if (Quality.Mode == VideoQualityMode.ConstantQuality &&
            (Quality.QualityLevel < VideoQuality.BestQualityLevel || Quality.QualityLevel > VideoQuality.WorstQualityLevel))
            throw new ArgumentOutOfRangeException(nameof(Quality), Quality, "The quality level must be between 0 and 51.");
        if (!Enum.IsDefined(Quality.Mode)) throw new ArgumentOutOfRangeException(nameof(Quality), Quality, "Unknown quality mode.");

        if (KeyframeIntervalFrames < 0) throw new ArgumentOutOfRangeException(nameof(KeyframeIntervalFrames), KeyframeIntervalFrames, "The keyframe interval cannot be negative.");
        if (!Enum.IsDefined(ColorRange)) throw new ArgumentOutOfRangeException(nameof(ColorRange), ColorRange, "Unknown colour range.");
        if (!Enum.IsDefined(ColorMatrix)) throw new ArgumentOutOfRangeException(nameof(ColorMatrix), ColorMatrix, "Unknown colour matrix.");
        if (RotationDegrees != 0 && RotationDegrees != 90 && RotationDegrees != 180 && RotationDegrees != 270)
            throw new ArgumentOutOfRangeException(nameof(RotationDegrees), RotationDegrees, "The rotation must be 0, 90, 180 or 270.");

        if (!HasAudio) return;

        bool keepRate = allowKeep && AudioSampleRate == 0;
        if (!keepRate && (AudioSampleRate < 8000 || AudioSampleRate > 96000))
            throw new ArgumentOutOfRangeException(nameof(AudioSampleRate), AudioSampleRate, "The audio sample rate must be between 8000 and 96000 for AAC.");
        bool keepChannels = allowKeep && AudioChannels == 0;
        if (!keepChannels && (AudioChannels < 1 || AudioChannels > 8))
            throw new ArgumentOutOfRangeException(nameof(AudioChannels), AudioChannels, "The audio channel count must be between 1 and 8.");
        if (AudioBitrateKilobitsPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(AudioBitrateKilobitsPerSecond), AudioBitrateKilobitsPerSecond, "The audio bitrate must be greater than zero.");
    }

    private static void CheckDimension(int value, string name, bool allowKeep)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, allowKeep
                ? "The size must be positive and even, or BOTH Width and Height 0 to keep the source's size."
                : "The size must be positive and even.");
        }

        if (value % 2 != 0) throw new ArgumentOutOfRangeException(name, value, "4:2:0 video needs an even width and height.");
    }
}
