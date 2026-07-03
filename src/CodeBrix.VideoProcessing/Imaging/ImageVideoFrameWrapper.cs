using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Imaging;
using CodeBrix.Imaging.PixelFormats;
using CodeBrix.VideoProcessing.Pipes;

namespace CodeBrix.VideoProcessing.Imaging;

/// <summary>
///     Wraps a CodeBrix.Imaging <see cref="Image{TPixel}">RGBA image</see> as an
///     <see cref="IVideoFrame" /> so it can be piped into FFmpeg through a
///     <see cref="RawVideoPipeSource" />. The frame is emitted as raw
///     <c>rgba</c> pixel data.
/// </summary>
public class ImageVideoFrameWrapper : IVideoFrame, IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageVideoFrameWrapper" />
    ///     class wrapping the supplied RGBA image.
    /// </summary>
    /// <param name="image">The RGBA image to wrap as a video frame.</param>
    /// <exception cref="ArgumentNullException"><paramref name="image" /> is <c>null</c>.</exception>
    public ImageVideoFrameWrapper(Image<Rgba32> image)
    {
        Source = image ?? throw new ArgumentNullException(nameof(image));
    }

    /// <summary>
    ///     Gets the wrapped RGBA image.
    /// </summary>
    public Image<Rgba32> Source { get; }

    /// <summary>
    ///     Gets the width of the frame, in pixels.
    /// </summary>
    public int Width => Source.Width;

    /// <summary>
    ///     Gets the height of the frame, in pixels.
    /// </summary>
    public int Height => Source.Height;

    /// <summary>
    ///     Gets the FFmpeg pixel format of the serialized frame data (always <c>rgba</c>).
    /// </summary>
    public string Format => "rgba";

    /// <summary>
    ///     Writes the frame's raw RGBA pixel data to the supplied stream.
    /// </summary>
    /// <param name="pipe">The destination stream.</param>
    public void Serialize(Stream pipe)
    {
        var buffer = new byte[Width * Height * 4];
        Source.CopyPixelDataTo(buffer);
        pipe.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    ///     Asynchronously writes the frame's raw RGBA pixel data to the supplied stream.
    /// </summary>
    /// <param name="pipe">The destination stream.</param>
    /// <param name="token">Token used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous write.</returns>
    public async Task SerializeAsync(Stream pipe, CancellationToken token)
    {
        var buffer = new byte[Width * Height * 4];
        Source.CopyPixelDataTo(buffer);
        await pipe.WriteAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
    }

    /// <summary>
    ///     Disposes the wrapped image.
    /// </summary>
    public void Dispose()
    {
        Source.Dispose();
    }
}
