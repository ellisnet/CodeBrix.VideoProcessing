using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Imaging;
using CodeBrix.VideoProcessing;
using CodeBrix.VideoProcessing.Pipes;
using Size = System.Drawing.Size;

namespace CodeBrix.VideoProcessing.Extensions.Imaging;

/// <summary>
///     Provides in-memory video snapshot helpers that return
///     <see cref="Image">CodeBrix.Imaging images</see>, adapted from the
///     FFMpegCore SkiaSharp/System.Drawing image extensions.
/// </summary>
public static class FFMpegImage
{
    /// <summary>
    ///     Captures a single frame from a video and returns it as an in-memory
    ///     <see cref="Image" /> (rather than writing a thumbnail file). The frame
    ///     is produced by FFmpeg as a PNG and decoded with CodeBrix.Imaging.
    /// </summary>
    /// <param name="input">Source video file.</param>
    /// <param name="size">Thumbnail size. If width or height equals 0, the other is computed automatically. Pass <c>null</c> for the source size.</param>
    /// <param name="captureTime">Seek position where the snapshot should be taken. Defaults to one third of the duration.</param>
    /// <param name="streamIndex">Selected video stream index.</param>
    /// <param name="inputFileIndex">Input file index.</param>
    /// <returns>A CodeBrix.Imaging <see cref="Image" /> containing the requested snapshot.</returns>
    public static Image Snapshot(string input, Size? size = null, TimeSpan? captureTime = null, int? streamIndex = null, int inputFileIndex = 0)
    {
        var source = FFProbe.Analyse(input);
        var (arguments, outputOptions) = SnapshotArgumentBuilder.BuildSnapshotArguments(input, source, size, captureTime, streamIndex, inputFileIndex);
        using var ms = new MemoryStream();

        arguments
            .OutputToPipe(new StreamPipeSink(ms), options => outputOptions(options
                .ForceFormat("rawvideo")))
            .ProcessSynchronously();

        ms.Position = 0;
        return Image.Load(ms);
    }

    /// <summary>
    ///     Asynchronously captures a single frame from a video and returns it as
    ///     an in-memory <see cref="Image" />. The frame is produced by FFmpeg as a
    ///     PNG and decoded with CodeBrix.Imaging.
    /// </summary>
    /// <param name="input">Source video file.</param>
    /// <param name="size">Thumbnail size. If width or height equals 0, the other is computed automatically. Pass <c>null</c> for the source size.</param>
    /// <param name="captureTime">Seek position where the snapshot should be taken. Defaults to one third of the duration.</param>
    /// <param name="streamIndex">Selected video stream index.</param>
    /// <param name="inputFileIndex">Input file index.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that resolves to a CodeBrix.Imaging <see cref="Image" /> containing the requested snapshot.</returns>
    public static async Task<Image> SnapshotAsync(string input, Size? size = null, TimeSpan? captureTime = null, int? streamIndex = null,
        int inputFileIndex = 0, CancellationToken cancellationToken = default)
    {
        var source = await FFProbe.AnalyseAsync(input, cancellationToken: cancellationToken).ConfigureAwait(false);
        var (arguments, outputOptions) = SnapshotArgumentBuilder.BuildSnapshotArguments(input, source, size, captureTime, streamIndex, inputFileIndex);
        using var ms = new MemoryStream();

        await arguments
            .OutputToPipe(new StreamPipeSink(ms), options => outputOptions(options
                .ForceFormat("rawvideo")))
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously()
            .ConfigureAwait(false);

        ms.Position = 0;
        return Image.Load(ms);
    }
}
