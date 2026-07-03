using System;
using System.IO;
using CodeBrix.Imaging;
using CodeBrix.VideoProcessing;

namespace CodeBrix.VideoProcessing.Extensions.Imaging;

/// <summary>
///     Extension helpers that bridge CodeBrix.Imaging images and
///     CodeBrix.VideoProcessing operations.
/// </summary>
public static class ImageExtensions
{
    /// <summary>
    ///     Combines a poster image with an audio track, producing a video whose
    ///     single visual frame is the supplied image (album-art-over-audio). The
    ///     image is written to a temporary PNG file and muxed with the audio via
    ///     <see cref="FFMpeg.PosterWithAudio(string, string, string)" />.
    /// </summary>
    /// <param name="poster">The poster image to use as the video's visual frame.</param>
    /// <param name="audio">Path to the audio file to mux.</param>
    /// <param name="output">Path of the resulting video file.</param>
    /// <returns><c>true</c> if the operation succeeded; otherwise <c>false</c>.</returns>
    public static bool AddAudio(this Image poster, string audio, string output)
    {
        var destination = $"{Environment.TickCount}.png";
        poster.SaveAsPng(destination);

        try
        {
            return FFMpeg.PosterWithAudio(destination, audio, output);
        }
        finally
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
        }
    }
}
