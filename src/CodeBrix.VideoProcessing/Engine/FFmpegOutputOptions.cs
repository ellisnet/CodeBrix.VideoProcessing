using System;
using System.Globalization;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;

namespace CodeBrix.VideoProcessing.Engine;

/// <summary>Turns a <see cref="StandardVideoRequest" /> into ffmpeg output options on the fluent builder.</summary>
internal static class FFmpegOutputOptions
{
    internal const string H264Encoder = "libx264";
    internal const string HevcEncoder = "libx265";

    /// <summary>Adds the codec, quality, colour, audio, metadata and container options for a request.</summary>
    /// <param name="output">The output options being built.</param>
    /// <param name="request">The request.</param>
    /// <param name="includeMaps">Map input 0's video and input 1's audio (the writer's two pipes).</param>
    /// <param name="isTranscode">"Keep" values are allowed and the source is one file.</param>
    internal static void Apply(FFMpegArgumentOptions output, StandardVideoRequest request, bool includeMaps, bool isTranscode)
    {
        if (includeMaps)
        {
            output.WithCustomArgument("-map 0:v:0");
            if (request.HasAudio) output.WithCustomArgument("-map 1:a:0");
        }

        bool hevc = request.Profile == StandardVideoProfile.Mp4HevcAac;
        output.WithVideoCodec(hevc ? HevcEncoder : H264Encoder);
        if (hevc) output.WithCustomArgument("-tag:v hvc1 -x265-params log-level=error");

        string filter = BuildVideoFilter(request, isTranscode);
        if (filter != null) output.WithCustomArgument("-vf " + filter);
        output.ForcePixelFormat("yuv420p");

        switch (request.Quality.Mode)
        {
            case VideoQualityMode.ConstantQuality:
                output.WithConstantRateFactor(request.Quality.QualityLevel);
                break;
            case VideoQualityMode.TargetBitrate:
                output.WithVideoBitrate(request.Quality.BitrateKilobitsPerSecond);
                break;
        }

        if (request.KeyframeIntervalFrames > 0)
            output.WithCustomArgument("-g " + request.KeyframeIntervalFrames.ToString(CultureInfo.InvariantCulture));

        output.WithCustomArgument("-color_range " + FFmpegNames.ToFFmpegRange(request.ColorRange));
        string colorSpace = FFmpegNames.ToFFmpegColorSpace(request.ColorMatrix);
        if (colorSpace != null)
        {
            output.WithCustomArgument("-colorspace " + colorSpace);
            if (request.ColorMatrix == VideoColorMatrix.Bt709) output.WithCustomArgument("-color_primaries bt709 -color_trc bt709");
            else if (request.ColorMatrix == VideoColorMatrix.Bt601) output.WithCustomArgument("-color_primaries smpte170m -color_trc smpte170m");
        }

        if (request.HasAudio)
        {
            output.WithAudioCodec("aac");
            output.WithAudioBitrate(request.AudioBitrateKilobitsPerSecond);
            if (request.AudioSampleRate > 0) output.WithAudioSamplingRate(request.AudioSampleRate);
            if (request.AudioChannels > 0) output.WithCustomArgument("-ac " + request.AudioChannels.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            output.WithCustomArgument("-an");
        }

        if (!string.IsNullOrEmpty(request.Title)) output.WithMetadata("title", request.Title);
        if (request.CreationTime.HasValue)
        {
            output.WithMetadata("creation_time", request.CreationTime.Value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture));
        }

        if (request.FastStart) output.WithFastStart();
        output.ForceFormat("mp4");
    }

    // The scale / fps chain: RGBA input needs an explicit matrix and range for its conversion to YUV; a transcode
    // may change size and rate. Null when nothing is needed.
    private static string BuildVideoFilter(StandardVideoRequest request, bool isTranscode)
    {
        string filter = null;

        if (isTranscode && request.FrameRate.IsSpecified)
        {
            filter = "fps=fps=" + request.FrameRate;
        }

        bool resize = isTranscode && request.Width > 0 && request.Height > 0;
        bool fromRgb = !isTranscode && request.InputPixelLayout == VideoPixelLayout.Rgba;
        if (resize || fromRgb)
        {
            string scale = resize
                ? "scale=" + request.Width.ToString(CultureInfo.InvariantCulture) + ":" + request.Height.ToString(CultureInfo.InvariantCulture)
                : "scale";
            string matrix = FFmpegNames.ToFFmpegColorSpace(request.ColorMatrix) ?? "bt709";
            scale += (resize ? ":" : "=") + "out_color_matrix=" + matrix + ":out_range=" + FFmpegNames.ToFFmpegRange(request.ColorRange);
            filter = filter == null ? scale : filter + "," + scale;
        }

        return filter;
    }
}
