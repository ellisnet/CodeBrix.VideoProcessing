using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Enums;

namespace CodeBrix.VideoProcessing; //was previously: FFMpegCore;

public class VideoStream : MediaStream
{
    public double AvgFrameRate { get; set; }
    public int BitsPerRawSample { get; set; }
    public (int Width, int Height) DisplayAspectRatio { get; set; }
    public (int Width, int Height) SampleAspectRatio { get; set; }
    public string Profile { get; set; } = null;
    public int Width { get; set; }
    public int Height { get; set; }
    public double FrameRate { get; set; }
    public string PixelFormat { get; set; } = null;
    public int Level { get; set; }
    public int Rotation { get; set; }
    public double AverageFrameRate { get; set; }
    public string ColorRange { get; set; } = null;
    public string ColorSpace { get; set; } = null;
    public string ColorTransfer { get; set; } = null;
    public string ColorPrimaries { get; set; } = null;

    /// <summary>
    /// The exact frame rate as a reduced rational (for example 30000/1001), taken from ffprobe's <c>avg_frame_rate</c>,
    /// or from <c>r_frame_rate</c> when <c>avg_frame_rate</c> is missing or not a positive rational (ffprobe reports
    /// "0/0" for streams it cannot average); <see cref="CodeBrix.VideoProcessing.Core.Media.FrameRate.Unspecified" />
    /// when neither is known. The double <see cref="FrameRate" /> property is unchanged.
    /// </summary>
    public FrameRate ExactFrameRate { get; set; }

    /// <summary>
    /// ffprobe's <c>r_frame_rate</c> (the stream's base rate) as an exact reduced rational, with no fallback;
    /// <see cref="CodeBrix.VideoProcessing.Core.Media.FrameRate.Unspecified" /> when missing or not a positive rational.
    /// </summary>
    public FrameRate RealFrameRate { get; set; }

    /// <summary>
    /// The number of frames in the stream, from ffprobe's <c>nb_frames</c>; null when ffprobe does not report one
    /// (Matroska / WebM and other containers without a frame count).
    /// </summary>
    public long? FrameCount { get; set; }

    /// <summary>
    /// The raw rotation of the stream's display matrix side data in degrees, COUNTER-clockwise as ffprobe reports it
    /// (-90 means the picture is shown turned 90 degrees clockwise); null when the stream has no display matrix.
    /// Unlike <see cref="Rotation" />, it is not mixed with the legacy (clockwise) <c>rotate</c> tag.
    /// </summary>
    public double? DisplayMatrixRotation { get; set; }

    public PixelFormat GetPixelFormatInfo()
    {
        return FFMpeg.GetPixelFormat(PixelFormat);
    }
}
