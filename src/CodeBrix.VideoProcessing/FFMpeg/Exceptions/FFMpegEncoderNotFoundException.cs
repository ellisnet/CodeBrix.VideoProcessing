using System;

namespace CodeBrix.VideoProcessing.Exceptions;

/// <summary>
///     Thrown when ffmpeg exits because it does not have an encoder the command asked for - its standard error
///     says <c>Unknown encoder '...'</c>.
/// </summary>
/// <remarks>
///     <para>
///         It derives from <see cref="FFMpegException" /> with <see cref="FFMpegExceptionType.Process" />, so code that
///         already catches <see cref="FFMpegException" /> keeps working. <see cref="FFMpegException.FFMpegErrorOutput" />
///         carries ffmpeg's standard error as usual, and the message still contains the usual
///         <c>ffmpeg exited with non-zero exit-code (...)</c> text in full.
///     </para>
///     <para>
///         When the missing encoder is SVT-AV1 (<c>libsvtav1</c>) an explanation is put in front of that text: that the
///         SVT-AV1 library is not present or could not be found, and how to set
///         <see cref="FFOptions.AllowAv1EncoderFallback" /> to encode with <c>libaom-av1</c> instead.
///     </para>
/// </remarks>
public class FFMpegEncoderNotFoundException : FFMpegException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="encoderName">The encoder ffmpeg reported as unknown, for example <c>libsvtav1</c>.</param>
    /// <param name="message">The full message.</param>
    /// <param name="innerException">The failure underneath this one, if there was one.</param>
    /// <param name="ffMpegErrorOutput">ffmpeg's standard error.</param>
    public FFMpegEncoderNotFoundException(string encoderName, string message, Exception innerException = null, string ffMpegErrorOutput = "")
        : base(FFMpegExceptionType.Process, message, innerException, ffMpegErrorOutput)
    {
        EncoderName = encoderName;
    }

    /// <summary>The encoder ffmpeg reported as unknown, exactly as it appeared on the command line.</summary>
    public string EncoderName { get; }
}
