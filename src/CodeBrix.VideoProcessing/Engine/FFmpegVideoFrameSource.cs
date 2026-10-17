using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;

namespace CodeBrix.VideoProcessing.Engine;

/// <summary>
/// Decodes one video stream with ffmpeg into raw 8-, 10- or 12-bit frames read from its standard output.
/// </summary>
/// <remarks>
/// <para>
/// ffmpeg runs with <c>-noautorotate</c> (frames stay in stored orientation), an <c>fps</c> filter at the stream's
/// rate (so delivery is constant-rate even from a variable-rate source), a <c>scale</c> that keeps the source's
/// sample range, and a <c>format</c> chosen by <see cref="ChoosePixelFormat" />: yuv420p or yuv444p (with the 10le /
/// 12le variants for deeper samples), the yuva* variants when alpha is requested and present (the A plane is the
/// fourth plane of the raw frame), or rgba. Gray decodes through the same yuv / yuva formats and keeps only the Y
/// (and A) plane: ffmpeg's own gray formats would stretch limited-range luma to full range, while this way Gray's luma
/// is exactly I420's, in <see cref="VideoSourceInfo.ColorRange" />. 12-bit I420 with alpha is refused: ffmpeg has no
/// yuva420p12le. The "le" formats are little-endian with the value in the low bits - exactly the contract's layout.
/// </para>
/// <para>
/// <see cref="VideoSourceInfo.ColorMatrix" /> describes the DELIVERED samples, as the contract says. A YUV source keeps
/// its own matrix. An RGB source (an RGB pixel format, or a stream tagged with the RGB identity matrix) delivered as a
/// YUV layout is converted with an EXPLICIT matrix - <c>scale=out_color_matrix=bt709</c> - and reports
/// <see cref="VideoColorMatrix.Bt709" />, so the tag never names a matrix the samples do not have (swscale's own
/// default for that conversion is BT.601, and it is not reported anywhere). RGBA delivery reports
/// <see cref="VideoColorMatrix.Rgb" /> whatever the source was: those samples are RGB.
/// </para>
/// <para>
/// Seeking restarts ffmpeg with an input <c>-ss</c> a quarter of a frame before the target frame's grid time, so the
/// target is the first frame that survives ffmpeg's accurate seek. One buffer is reused for every frame.
/// </para>
/// </remarks>
internal sealed class FFmpegVideoFrameSource : IVideoFrameSource
{
    private readonly string path;
    private readonly VideoStreamInfo stream;
    private readonly FFOptions options;
    private readonly long maximumFrames;
    private readonly bool deliverAlpha;
    private readonly string outputPixelFormat;
    private readonly bool convertsFromRgb;
    private readonly int bytesPerSample;
    private readonly int lumaSize;
    private readonly int chromaWidth;
    private readonly int chromaHeight;
    private readonly int chromaSize;
    private readonly byte[] buffer;

    private FFmpegToolProcess tool;
    private long startIndex;
    private long nextIndex;
    private long framesFromThisProcess;
    private bool ended;
    private bool disposed;

    internal FFmpegVideoFrameSource(string path, VideoStreamInfo stream, VideoSourceOptions sourceOptions, FFOptions options, bool isMirrored = false, long maximumFrames = 0)
    {
        this.path = path;
        this.stream = stream;
        this.options = options;
        this.maximumFrames = maximumFrames;

        if (stream.Width <= 0 || stream.Height <= 0)
            throw new MediaFormatException($"The video stream of '{path}' has no known size.");
        if (!stream.FrameRate.IsSpecified)
            throw new MediaFormatException($"The video stream of '{path}' has no known frame rate.");

        VideoPixelLayout layout = sourceOptions.PixelLayout;
        int bitDepth = layout == VideoPixelLayout.Rgba ? 8 : sourceOptions.BitDepth;
        deliverAlpha = layout != VideoPixelLayout.Rgba && sourceOptions.IncludeAlpha && stream.HasAlpha;
        outputPixelFormat = ChoosePixelFormat(layout, bitDepth, deliverAlpha);
        bytesPerSample = bitDepth > 8 ? 2 : 1;

        // The raw frame's chroma planes: those of the ffmpeg format (for Gray, the yuv / yuva format it decodes through).
        bool fullChroma = outputPixelFormat.StartsWith("yuv444", StringComparison.Ordinal) || outputPixelFormat.StartsWith("yuva444", StringComparison.Ordinal);
        bool noChroma = layout == VideoPixelLayout.Rgba;
        lumaSize = stream.Width * stream.Height * bytesPerSample;
        chromaWidth = noChroma ? 0 : fullChroma ? stream.Width : (stream.Width + 1) / 2;
        chromaHeight = noChroma ? 0 : fullChroma ? stream.Height : (stream.Height + 1) / 2;
        chromaSize = chromaWidth * chromaHeight * bytesPerSample;
        int frameSize = layout == VideoPixelLayout.Rgba
            ? 4 * stream.Width * stream.Height
            : lumaSize + 2 * chromaSize + (deliverAlpha ? lumaSize : 0);
        buffer = new byte[frameSize];

        VideoColorRange deliveredRange = stream.ColorRange == VideoColorRange.Full ? VideoColorRange.Full : VideoColorRange.Limited;

        // The matrix of the samples that come out of the pipe, not of the stream in the file (see the class remarks).
        bool sourceIsRgb = stream.ColorMatrix == VideoColorMatrix.Rgb || stream.ChromaSubsampling == VideoChromaSubsampling.Rgb;
        convertsFromRgb = sourceIsRgb && layout != VideoPixelLayout.Rgba;
        VideoColorMatrix deliveredMatrix = layout == VideoPixelLayout.Rgba ? VideoColorMatrix.Rgb
            : convertsFromRgb ? VideoColorMatrix.Bt709
            : stream.ColorMatrix;

        Info = new VideoSourceInfo
        {
            Width = stream.Width,
            Height = stream.Height,
            FrameRate = stream.FrameRate,
            Duration = stream.Duration,
            EstimatedFrameCount = stream.FrameCount > 0 ? stream.FrameCount : stream.FrameRate.FrameCountFor(stream.Duration),
            PixelLayout = layout,
            BitDepth = bitDepth,
            SourceBitDepth = stream.BitDepth,
            SourceHasAlpha = stream.HasAlpha,
            HasAlpha = deliverAlpha || layout == VideoPixelLayout.Rgba,
            RotationDegrees = stream.RotationDegrees,
            IsMirrored = isMirrored,
            ColorRange = deliveredRange,
            ColorMatrix = deliveredMatrix
        };

        Seek(sourceOptions.StartTime);
    }

    public VideoSourceInfo Info { get; }

    public bool CanSeek => true;

    public bool TryReadFrame(out VideoFrameData frame)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        frame = default;
        if (ended) return false;
        if (maximumFrames > 0 && framesFromThisProcess >= maximumFrames)
        {
            End();
            return false;
        }

        tool ??= FFmpegToolProcess.Start(GlobalFFOptions.GetFFMpegBinaryPath(options), BuildArguments(), options.WorkingDirectory);

        int read = tool.ReadFully(buffer);
        if (read < buffer.Length)
        {
            tool.WaitForExit();
            int exitCode = tool.ExitCode;
            string error = tool.ErrorText;
            End();
            if (exitCode != 0)
            {
                throw new VideoProcessingException($"ffmpeg failed while decoding '{path}' (exit code {exitCode}): {error}");
            }

            return false;
        }

        long index = nextIndex++;
        framesFromThisProcess++;
        frame = BuildFrame(Info.FrameRate.TimestampOf(index), index);
        return true;
    }

    public void Seek(TimeSpan position)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (position < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(position), position, "The position cannot be negative.");

        FrameRate rate = stream.FrameRate;
        long index = rate.FrameIndexAt(position);
        if (rate.TimestampOf(index) < position) index++;

        End();
        startIndex = index;
        nextIndex = index;
        framesFromThisProcess = 0;
        ended = false;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        End();
    }

    private void End()
    {
        ended = true;
        tool?.Dispose();
        tool = null;
    }

    private VideoFrameData BuildFrame(TimeSpan timestamp, long index)
    {
        int width = stream.Width;
        int height = stream.Height;
        VideoPixelLayout layout = Info.PixelLayout;
        int bitDepth = Info.BitDepth;

        if (layout == VideoPixelLayout.Rgba)
        {
            return VideoFrameData.CreateRgba(width, height, new VideoPlane(buffer, 4 * width, 4 * width, height), timestamp, index);
        }

        var memory = new ReadOnlyMemory<byte>(buffer);
        int lumaRow = width * bytesPerSample;
        int chromaRow = chromaWidth * bytesPerSample;
        VideoPlane y = new VideoPlane(memory.Slice(0, lumaSize), lumaRow, lumaRow, height);
        VideoPlane a = deliverAlpha
            ? new VideoPlane(memory.Slice(lumaSize + 2 * chromaSize, lumaSize), lumaRow, lumaRow, height)
            : default;

        if (layout == VideoPixelLayout.Gray)
        {
            // Gray decodes through a yuv / yuva format; its chroma planes are skipped.
            return VideoFrameData.CreateGray(width, height, y, timestamp, index, a, bitDepth);
        }

        VideoPlane u = new VideoPlane(memory.Slice(lumaSize, chromaSize), chromaRow, chromaRow, chromaHeight);
        VideoPlane v = new VideoPlane(memory.Slice(lumaSize + chromaSize, chromaSize), chromaRow, chromaRow, chromaHeight);

        return layout == VideoPixelLayout.I444
            ? VideoFrameData.CreateI444(width, height, y, u, v, timestamp, index, a, bitDepth)
            : VideoFrameData.CreateI420(width, height, y, u, v, timestamp, index, a, bitDepth);
    }

    // The ffmpeg pixel format whose raw planes are exactly the contract's planes for a layout, depth and alpha.
    // Combinations ffmpeg has no exact format for are refused rather than approximated.
    internal static string ChoosePixelFormat(VideoPixelLayout layout, int bitDepth, bool alpha)
    {
        string depthSuffix = bitDepth switch
        {
            8 => string.Empty,
            10 => "10le",
            12 => "12le",
            _ => throw new ArgumentOutOfRangeException(nameof(bitDepth), bitDepth, "The bit depth must be 8, 10 or 12.")
        };

        switch (layout)
        {
            case VideoPixelLayout.Rgba:
                if (bitDepth != 8) throw new ArgumentOutOfRangeException(nameof(bitDepth), bitDepth, "RGBA frames are always 8 bits.");
                return "rgba";
            case VideoPixelLayout.I420:
                if (alpha && bitDepth == 12)
                {
                    throw new NotSupportedException(
                        "The ffmpeg engine cannot deliver 12-bit I420 frames with an alpha plane (ffmpeg has no yuva420p12le). " +
                        "Ask for 10 bits, the I444 layout, or IncludeAlpha = false.");
                }

                return (alpha ? "yuva420p" : "yuv420p") + depthSuffix;
            case VideoPixelLayout.I444:
                return (alpha ? "yuva444p" : "yuv444p") + depthSuffix;
            case VideoPixelLayout.Gray:
                // Through yuv / yuva, keeping only Y (and A): ffmpeg's gray formats are full range, and there is no
                // planar gray + alpha format. 12-bit with alpha has only the 4:4:4 yuva format.
                if (!alpha) return "yuv420p" + depthSuffix;
                return bitDepth == 12 ? "yuva444p12le" : "yuva420p" + depthSuffix;
            default:
                throw new NotSupportedException($"The ffmpeg engine cannot deliver {layout} frames.");
        }
    }

    private List<string> BuildArguments()
    {
        var arguments = new List<string> { "-hide_banner", "-nostdin", "-v", "error", "-noautorotate" };

        if (startIndex > 0)
        {
            // A quarter of a frame early: the target frame is then the first one ffmpeg's accurate seek keeps.
            TimeSpan seek = stream.FrameRate.TimestampOf(startIndex) - TimeSpan.FromTicks(stream.FrameRate.FrameDuration.Ticks / 4);
            arguments.Add("-ss");
            arguments.Add(seek.TotalSeconds.ToString("0.000000", CultureInfo.InvariantCulture));
        }

        // libvpx, not ffmpeg's native VP8/VP9 decoders, reads the alpha side channel of a WebM with alpha_mode=1.
        if (deliverAlpha && stream.Tags.TryGetValue("alpha_mode", out string alphaMode) && alphaMode == "1")
        {
            if (stream.CodecName == "vp9") arguments.AddRange(new[] { "-c:v", "libvpx-vp9" });
            else if (stream.CodecName == "vp8") arguments.AddRange(new[] { "-c:v", "libvpx" });
        }

        arguments.AddRange(new[] { "-i", path, "-map", "0:" + stream.Index.ToString(CultureInfo.InvariantCulture), "-an", "-sn", "-dn" });

        string filter = "fps=fps=" + stream.FrameRate;
        filter += Info.PixelLayout == VideoPixelLayout.Rgba
            ? ",format=rgba"
            : ",scale=out_range=" + FFmpegNames.ToFFmpegRange(Info.ColorRange)
              + (convertsFromRgb ? ":out_color_matrix=" + FFmpegNames.ToFFmpegColorSpace(Info.ColorMatrix) : string.Empty)
              + ",format=" + outputPixelFormat;
        arguments.AddRange(new[] { "-vf", filter });

        if (maximumFrames > 0)
        {
            arguments.AddRange(new[] { "-frames:v", maximumFrames.ToString(CultureInfo.InvariantCulture) });
        }

        arguments.AddRange(new[] { "-f", "rawvideo", "pipe:1" });
        return arguments;
    }
}
