using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;

namespace CodeBrix.VideoProcessing.Engine;

/// <summary>Decodes one audio stream with ffmpeg into interleaved 32-bit float samples (<c>-f f32le</c>) read from standard output.</summary>
internal sealed class FFmpegAudioSampleSource : IAudioSampleSource
{
    private readonly string path;
    private readonly AudioStreamInfo stream;
    private readonly FFOptions options;
    private readonly int frameBytes;

    private FFmpegToolProcess tool;
    private TimeSpan start;
    private bool ended;
    private bool disposed;

    internal FFmpegAudioSampleSource(string path, AudioStreamInfo stream, AudioSourceOptions sourceOptions, FFOptions options)
    {
        this.path = path;
        this.stream = stream;
        this.options = options;

        int sampleRate = sourceOptions.SampleRate > 0 ? sourceOptions.SampleRate : stream.SampleRate;
        int channels = sourceOptions.Channels > 0 ? sourceOptions.Channels : stream.Channels;
        if (sampleRate <= 0 || channels <= 0)
            throw new MediaFormatException($"The audio stream of '{path}' has no known sample rate or channel count; set them in AudioSourceOptions.");

        Info = new AudioSourceInfo
        {
            SampleRate = sampleRate,
            Channels = channels,
            Duration = stream.Duration,
            SourceCodecName = stream.CodecName
        };
        frameBytes = channels * sizeof(float);

        Seek(sourceOptions.StartTime);
    }

    public AudioSourceInfo Info { get; }

    public bool CanSeek => true;

    public int Read(Span<float> interleaved)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (interleaved.Length % Info.Channels != 0)
            throw new ArgumentException($"The buffer length must be a multiple of the channel count ({Info.Channels}).", nameof(interleaved));
        if (ended || interleaved.Length == 0) return 0;

        tool ??= FFmpegToolProcess.Start(GlobalFFOptions.GetFFMpegBinaryPath(options), BuildArguments(), options.WorkingDirectory);

        Span<byte> bytes = MemoryMarshal.AsBytes(interleaved);
        int read = tool.ReadFully(bytes);
        if (read < bytes.Length)
        {
            tool.WaitForExit();
            int exitCode = tool.ExitCode;
            string error = tool.ErrorText;
            End();
            if (exitCode != 0)
            {
                throw new VideoProcessingException($"ffmpeg failed while decoding the audio of '{path}' (exit code {exitCode}): {error}");
            }
        }

        return read / frameBytes;
    }

    public void Seek(TimeSpan position)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (position < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(position), position, "The position cannot be negative.");

        End();
        start = position;
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

    private List<string> BuildArguments()
    {
        var arguments = new List<string> { "-hide_banner", "-nostdin", "-v", "error" };
        if (start > TimeSpan.Zero)
        {
            arguments.Add("-ss");
            arguments.Add(start.TotalSeconds.ToString("0.000000", CultureInfo.InvariantCulture));
        }

        arguments.AddRange(new[]
        {
            "-i", path, "-map", "0:" + stream.Index.ToString(CultureInfo.InvariantCulture), "-vn", "-sn", "-dn",
            "-ar", Info.SampleRate.ToString(CultureInfo.InvariantCulture),
            "-ac", Info.Channels.ToString(CultureInfo.InvariantCulture),
            "-f", "f32le", "pipe:1"
        });
        return arguments;
    }
}
