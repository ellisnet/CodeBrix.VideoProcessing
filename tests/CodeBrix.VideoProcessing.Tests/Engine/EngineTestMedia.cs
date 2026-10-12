using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;

namespace CodeBrix.VideoProcessing.Tests.Engine;

/// <summary>
/// Synthetic media and inspection helpers for the engine tests. Fixtures are generated with the host ffmpeg
/// directly (not through the library under test), and outputs are inspected with ffprobe directly.
/// </summary>
internal static class EngineTestMedia
{
    /// <summary>
    /// A 64x48, 25 fps, 10-frame lossless FFV1 clip in yuva420p whose alpha is a horizontal ramp: alpha = min(255, 4 * x).
    /// </summary>
    public static void CreateAlphaClip(string path) =>
        RunFFmpeg($"-f lavfi -i \"testsrc=size=64x48:rate=25:duration=0.4,format=yuva420p,geq=lum='lum(X,Y)':cb='cb(X,Y)':cr='cr(X,Y)':a='min(255,X*4)'\" -c:v ffv1 \"{path}\"");

    /// <summary>A short H.264 + AAC MP4 clip from ffmpeg's test sources, for concatenation.</summary>
    public static void CreateTestClip(string path, double seconds, int frequency) =>
        RunFFmpeg($"-f lavfi -i testsrc=size=160x120:rate=25:duration={seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                  $"-f lavfi -i sine=frequency={frequency}:sample_rate=48000:duration={seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                  $"-c:v libx264 -pix_fmt yuv420p -c:a aac -ac 2 -shortest \"{path}\"");

    /// <summary>A one-second 64x48 clip at exactly 30000/1001 fps (30 frames), H.264 in MP4 or FFV1 in Matroska by extension.</summary>
    // A solid-colour RGB source (ffv1 stores it as bgr0, which ffprobe reports with the gbr colour space).
    public static void CreateRgbClip(string path, string colour) =>
        RunFFmpeg($"-f lavfi -i color=c={colour}:size=64x48:rate=25:duration=0.2 -c:v ffv1 -pix_fmt bgr0 \"{path}\"");

    public static void CreateNtscClip(string path) =>
        RunFFmpeg($"-f lavfi -i testsrc=size=64x48:rate=30000/1001 -frames:v 30 " +
                  (path.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) ? "-c:v ffv1" : "-c:v libx264 -pix_fmt yuv420p") +
                  $" \"{path}\"");

    /// <summary>
    /// A two-second H.264 + AAC Matroska clip with ONE SubRip subtitle stream (language "eng", title "English") and two
    /// chapters ("Opening" 0-1 s, "Closing" 1-2 s). The cue and chapter text files are written next to it and deleted.
    /// </summary>
    public static void CreateClipWithSubtitlesAndChapters(string path)
    {
        string cues = path + ".srt";
        string chapters = path + ".ffmeta";
        File.WriteAllText(cues, "1\n00:00:00,000 --> 00:00:01,000\nHello\n\n2\n00:00:01,000 --> 00:00:02,000\nWorld\n");
        File.WriteAllText(chapters,
            ";FFMETADATA1\n[CHAPTER]\nTIMEBASE=1/1000\nSTART=0\nEND=1000\ntitle=Opening\n" +
            "[CHAPTER]\nTIMEBASE=1/1000\nSTART=1000\nEND=2000\ntitle=Closing\n");
        try
        {
            RunFFmpeg("-f lavfi -i testsrc=size=160x120:rate=25:duration=2 -f lavfi -i sine=frequency=440:sample_rate=48000:duration=2 " +
                      $"-i \"{cues}\" -i \"{chapters}\" -map 0:v -map 1:a -map 2:s -map_metadata 3 -map_chapters 3 " +
                      "-c:v libx264 -pix_fmt yuv420p -c:a aac -c:s srt " +
                      $"-metadata:s:s:0 language=eng -metadata:s:s:0 title=English \"{path}\"");
        }
        finally
        {
            File.Delete(cues);
            File.Delete(chapters);
        }
    }

    /// <summary>One synthetic I420 frame: a diagonal luma ramp shifted by the frame index, neutral chroma.</summary>
    public static VideoFrameData I420Frame(int width, int height, long index)
    {
        byte[] y = new byte[width * height];
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                y[row * width + column] = (byte)(16 + ((row + column + index * 4) % 220));
            }
        }

        int chromaWidth = (width + 1) / 2;
        int chromaHeight = (height + 1) / 2;
        byte[] u = new byte[chromaWidth * chromaHeight];
        byte[] v = new byte[chromaWidth * chromaHeight];
        Array.Fill(u, (byte)128);
        Array.Fill(v, (byte)128);

        return VideoFrameData.CreateI420(
            width,
            height,
            VideoPlane.FromArray(y, width, height),
            VideoPlane.FromArray(u, chromaWidth, chromaHeight),
            VideoPlane.FromArray(v, chromaWidth, chromaHeight),
            new FrameRate(25, 1).TimestampOf(index),
            index);
    }

    /// <summary>Interleaved stereo float samples of a sine tone.</summary>
    public static float[] Tone(int sampleRate, int channels, int frames, long startFrame, double frequency)
    {
        float[] samples = new float[frames * channels];
        for (int i = 0; i < frames; i++)
        {
            float value = (float)(0.25 * Math.Sin(2 * Math.PI * frequency * (startFrame + i) / sampleRate));
            for (int channel = 0; channel < channels; channel++) samples[i * channels + channel] = value;
        }

        return samples;
    }

    /// <summary>ffprobe's value of one stream entry, for example ("v:0", "codec_name").</summary>
    public static string ProbeStream(string path, string selector, string entry) =>
        RunFFprobe($"-v error -select_streams {selector} -show_entries stream={entry} -of default=nw=1:nk=1 \"{path}\"").Trim();

    /// <summary>The number of decoded video frames, counted by ffprobe.</summary>
    public static int CountFrames(string path) =>
        int.Parse(RunFFprobe($"-v error -select_streams v:0 -count_frames -show_entries stream=nb_read_frames -of default=nw=1:nk=1 \"{path}\"").Trim(),
            System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The container duration in seconds, from ffprobe.</summary>
    public static double Duration(string path) =>
        double.Parse(RunFFprobe($"-v error -show_entries format=duration -of default=nw=1:nk=1 \"{path}\"").Trim(),
            System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The frame indexes of the keyframes of the first video stream.</summary>
    public static List<int> KeyframeIndexes(string path)
    {
        string flags = RunFFprobe($"-v error -select_streams v:0 -show_entries packet=flags -of csv=p=0 \"{path}\"");
        var keyframes = new List<int>();
        int index = 0;
        foreach (string line in flags.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("K", StringComparison.Ordinal)) keyframes.Add(index);
            index++;
        }

        return keyframes;
    }

    /// <summary>The four-character types of an MP4's top-level boxes, in file order.</summary>
    public static List<string> TopLevelBoxes(string path)
    {
        var boxes = new List<string>();
        using FileStream stream = File.OpenRead(path);
        byte[] header = new byte[16];
        long position = 0;
        while (position + 8 <= stream.Length)
        {
            stream.Position = position;
            stream.ReadExactly(header, 0, 8);
            long size = BinaryPrimitives.ReadUInt32BigEndian(header);
            string type = Encoding.ASCII.GetString(header, 4, 4);
            if (size == 1)
            {
                stream.ReadExactly(header, 8, 8);
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8));
            }
            else if (size == 0)
            {
                size = stream.Length - position;
            }

            boxes.Add(type);
            if (size < 8) break;
            position += size;
        }

        return boxes;
    }

    private static void RunFFmpeg(string arguments) => Run("ffmpeg", "-hide_banner -v error -y " + arguments);

    private static string RunFFprobe(string arguments) => Run("ffprobe", arguments);

    private static string Run(string tool, string arguments)
    {
        var startInfo = new ProcessStartInfo(GlobalFFOptions.GetFFMpegBinaryPath().Replace("ffmpeg", tool), arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(startInfo);
        var error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"{tool} {arguments} failed: {error.GetAwaiter().GetResult()}");
        return output;
    }
}
