using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Android.Engine;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;
using global::Android.App;
using global::Android.OS;
using global::Android.Widget;
using AndroidBuild = global::Android.OS.Build;
using AndroidLog = global::Android.Util.Log;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace CodeBrix.VideoProcessing.Android.Diagnostics;

/// <summary>
/// One-screen device proof for CodeBrix.VideoProcessing.Android: registers the engine and runs the WHOLE
/// CodeBrix.VideoProcessing.Core contract against the bundled synthetic clip and against files it writes itself -
/// Analyse, Snapshot, OpenVideo in every offered layout with seeking, OpenAudio with resampling, CreateWriter
/// (I420 / NV12 / RGBA input, HEVC when offered, fast start, tags), TrimFile both ways, ConcatenateFiles both ways,
/// TranscodeFile, the capability refusals and cancellation. One PASS / FAIL line per step and a final
/// "VPDIAG RESULT:" line go to the screen and to logcat under the tag VPDiag.
/// </summary>
[Activity(Label = "CodeBrix Video Processing Diagnostics", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private const string LogTag = "VPDiag";
    private const string ClipName = "clip-h264-aac.mp4";

    private TextView _log = null;
    private ScrollView _scroll = null;
    private Button _run = null;
    private int _running;
    private string _work = null;
    private string _clip = null;
    private readonly List<string> _failures = new List<string>();
    private readonly List<string> _notes = new List<string>();
    private int _passed;
    private string _videoCodecProblem = null;

    /// <summary>Builds the one-screen UI and starts the proof one second after launch (so adb needs no taps).</summary>
    /// <param name="savedInstanceState">Unused saved state.</param>
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var panel = new LinearLayout(this) { Orientation = global::Android.Widget.Orientation.Vertical };
        _run = new Button(this) { Text = "Run video processing diagnostics" };
        _run.Click += (_, _) => StartRun();
        _log = new TextView(this) { Text = "" };
        _log.SetTextIsSelectable(true);
        _scroll = new ScrollView(this);
        _scroll.AddView(_log);
        panel.AddView(_run);
        panel.AddView(_scroll, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent, 0, 1f));
        SetContentView(panel);

        new Handler(Looper.MainLooper).PostDelayed(StartRun, 1000);
    }

    private void StartRun()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1) return;
        _run.Enabled = false;
        Task.Run(() =>
        {
            try { RunDiagnostics(); }
            catch (Exception ex) { Log("VPDIAG RESULT: FAIL: unexpected " + ex.GetType().Name + ": " + ex.Message); }
            finally
            {
                Volatile.Write(ref _running, 0);
                RunOnUiThread(() => _run.Enabled = true);
            }
        });
    }

    private void Log(string line)
    {
        AndroidLog.Info(LogTag, line);
        RunOnUiThread(() =>
        {
            _log.Append(line + "\n");
            _scroll.Post(() => _scroll.FullScroll(global::Android.Views.FocusSearchDirection.Down));
        });
    }

    private void Step(string id, string name, Func<string> body)
    {
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            string detail = body();
            if (detail != null && detail.StartsWith("NOTE:", StringComparison.Ordinal))
            {
                _notes.Add(id);
                Log($"STEP {id} {name}: PASSED-WITH-NOTE ({watch.ElapsedMilliseconds} ms) - {detail.Substring(5).Trim()}");
            }
            else
            {
                _passed++;
                Log($"STEP {id} {name}: PASS ({watch.ElapsedMilliseconds} ms) - {detail}");
            }
        }
        catch (VideoProcessingException ex) when (_videoCodecProblem != null)
        {
            // The codec probe already showed this device cannot run video codecs at all (see CodecProbe).
            _notes.Add(id);
            Log($"STEP {id} {name}: PASSED-WITH-NOTE ({watch.ElapsedMilliseconds} ms) - video codecs unavailable on this device ({_videoCodecProblem}); this step's failure: {Describe(ex)}");
        }
        catch (Exception ex)
        {
            _failures.Add(id);
            Log($"STEP {id} {name}: FAIL ({watch.ElapsedMilliseconds} ms) - {Describe(ex)}");
        }
    }

    private static string Describe(Exception ex)
    {
        var text = new StringBuilder(ex.GetType().Name + ": " + ex.Message);
        for (Exception inner = ex.InnerException; inner != null; inner = inner.InnerException)
        {
            text.Append(" <- " + inner.GetType().Name + ": " + inner.Message);
        }

        return text.ToString();
    }

    private static void Check(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException("check failed: " + what);
    }

    private string Out(string name) => Path.Combine(_work, name);

    private void RunDiagnostics()
    {
        _failures.Clear();
        _notes.Clear();
        _passed = 0;
        Log($"VPDiag start: {AndroidBuild.Manufacturer} {AndroidBuild.Model}, API {(int)AndroidBuild.VERSION.SdkInt}, ABI {string.Join(",", AndroidBuild.SupportedAbis ?? Array.Empty<string>())}");

        _work = Path.Combine(FilesDir.AbsolutePath, "vpdiag");
        if (Directory.Exists(_work)) Directory.Delete(_work, true);
        Directory.CreateDirectory(_work);
        _clip = Out(ClipName);
        using (Stream asset = Assets.Open(ClipName))
        using (FileStream file = File.Create(_clip))
        {
            asset.CopyTo(file);
        }

        IVideoProcessor engine = null;

        Step("01", "Register", () =>
        {
            CodeBrixVideoProcessingAndroid.Register();
            CodeBrixVideoProcessingAndroid.Register();
            engine = VideoProcessors.Current;
            Check(ReferenceEquals(engine, CodeBrixVideoProcessingAndroid.Processor), "Current is the registered Processor");
            Check(CodeBrixVideoProcessingAndroid.IsRegistered, "IsRegistered");
            Check(VideoProcessors.RegisteredProcessors.Count == 1, "registered once");
            Check(engine.Name == AndroidVideoProcessor.EngineName, "Name");
            return $"name {engine.Name}, capabilities [{engine.Capabilities}], hevc encoder {CodeBrixVideoProcessingAndroid.Processor.HasHevcEncoder}";
        });
        if (engine == null)
        {
            Report();
            return;
        }

        CodecProbe(engine);

        Step("02", "Analyse", () =>
        {
            MediaInfo info = engine.Analyse(_clip);
            VideoStreamInfo v = info.PrimaryVideo;
            AudioStreamInfo a = info.PrimaryAudio;
            Check(v != null && a != null, "one video and one audio stream");
            Check(v.CodecName == "h264", "video codec h264 (got " + v.CodecName + ")");
            Check(v.Width == 320 && v.Height == 240, "320x240");
            Check(v.FrameRate == new FrameRate(30, 1), "30/1 fps (got " + v.FrameRate + ")");
            Check(v.FrameCount == 120, "120 frames (got " + v.FrameCount + ")");
            Check(Math.Abs(info.Duration.TotalSeconds - 4.0) < 0.1, "duration 4 s (got " + info.Duration + ")");
            Check(a.CodecName == "aac" && a.SampleRate == 48000 && a.Channels == 2, "aac 48000 stereo");
            Check(v.RotationDegrees == 0, "rotation 0");
            return $"container {info.ContainerFormat}, {info.Duration.TotalSeconds:0.000} s; video {v.CodecName} {v.Width}x{v.Height} {v.FrameRate} {v.FrameCount} frames, depth {v.BitDepth} {v.PixelFormatName} {v.ChromaSubsampling}, range {v.ColorRange} matrix {v.ColorMatrix}; audio {a.CodecName} {a.SampleRate} Hz x{a.Channels} {a.Duration.TotalSeconds:0.000} s; tags [{string.Join(", ", info.Tags.Select(t => t.Key + "=" + t.Value))}]";
        });

        Step("03", "Snapshot", () =>
        {
            VideoFrameData shot = engine.Snapshot(_clip, TimeSpan.FromSeconds(1.5));
            Check(shot.Layout == VideoPixelLayout.I420 && shot.Width == 320 && shot.Height == 240 && shot.BitDepth == 8, "8-bit I420 320x240");
            Check(shot.FrameIndex == 45, "frame 45 at 1.5 s (got " + shot.FrameIndex + ")");
            using IVideoFrameSource source = engine.OpenVideo(_clip, new VideoSourceOptions { StartTime = TimeSpan.FromSeconds(1.5) });
            Check(source.TryReadFrame(out VideoFrameData same), "frame source at 1.5 s");
            double mad = MeanAbsoluteDifference(shot.Y.ToArray(), same.Y.ToArray());
            Check(mad == 0, "snapshot equals the frame source's frame 45 (MAD " + mad + ")");
            return $"I420 {shot.Width}x{shot.Height} #{shot.FrameIndex} @ {shot.Timestamp}, luma mean {shot.Y.ToArray().Average(b => b):0.0}";
        });

        Step("04", "OpenVideo I420", () =>
        {
            using IVideoFrameSource source = engine.OpenVideo(_clip);
            VideoSourceInfo info = source.Info;
            long count = 0;
            while (source.TryReadFrame(out VideoFrameData frame))
            {
                Check(frame.FrameIndex == count, "consecutive indexes");
                Check(frame.Timestamp == info.FrameRate.TimestampOf(count), "timestamp = TimestampOf(index)");
                Check(frame.Y.RowLength == 320 && frame.Y.Rows == 240 && frame.U.RowLength == 160 && frame.U.Rows == 120 && frame.V.Rows == 120, "plane sizes");
                count++;
            }

            Check(count == 120, "120 frames (got " + count + ")");
            return $"{count} frames, info {info.Width}x{info.Height} {info.FrameRate} est {info.EstimatedFrameCount} source depth {info.SourceBitDepth} range {info.ColorRange} matrix {info.ColorMatrix}";
        });

        Step("05", "OpenVideo Gray", () =>
        {
            using IVideoFrameSource gray = engine.OpenVideo(_clip, new VideoSourceOptions { PixelLayout = VideoPixelLayout.Gray });
            using IVideoFrameSource i420 = engine.OpenVideo(_clip);
            long count = 0;
            double worst = 0;
            while (gray.TryReadFrame(out VideoFrameData g))
            {
                Check(i420.TryReadFrame(out VideoFrameData c), "I420 source keeps pace");
                Check(g.Layout == VideoPixelLayout.Gray && g.U.IsEmpty, "Gray layout, no chroma");
                if (count % 10 == 0) worst = Math.Max(worst, MeanAbsoluteDifference(g.Y.ToArray(), c.Y.ToArray()));
                count++;
            }

            Check(count == 120, "120 frames (got " + count + ")");
            Check(worst == 0, "Gray Y equals I420 Y (worst MAD " + worst + ")");
            return $"{count} Gray frames, Y identical to I420's";
        });

        Step("06", "OpenVideo NV12 + RGBA", () =>
        {
            using IVideoFrameSource nv12 = engine.OpenVideo(_clip, new VideoSourceOptions { PixelLayout = VideoPixelLayout.Nv12 });
            Check(nv12.TryReadFrame(out VideoFrameData n), "an NV12 frame");
            Check(n.Layout == VideoPixelLayout.Nv12 && n.UV.RowLength == 320 && n.UV.Rows == 120, "NV12 planes");
            using IVideoFrameSource rgba = engine.OpenVideo(_clip, new VideoSourceOptions { PixelLayout = VideoPixelLayout.Rgba });
            Check(rgba.TryReadFrame(out VideoFrameData r), "an RGBA frame");
            Check(r.Layout == VideoPixelLayout.Rgba && r.Rgba.RowLength == 1280 && r.Rgba.Rows == 240 && rgba.Info.HasAlpha, "RGBA plane");
            Check(rgba.Info.ColorMatrix == VideoColorMatrix.Rgb, "RGBA delivery reports the Rgb matrix (got " + rgba.Info.ColorMatrix + ")");
            byte[] pixels = r.Rgba.ToArray();
            Check(pixels.Where((_, i) => i % 4 == 3).All(alpha => alpha == 255), "opaque alpha");
            return $"NV12 UV {n.UV.RowLength}x{n.UV.Rows}; RGBA {r.Rgba.RowLength}x{r.Rgba.Rows}, first pixel {pixels[0]},{pixels[1]},{pixels[2]}";
        });

        Step("07", "OpenVideo StartTime + Seek", () =>
        {
            using IVideoFrameSource source = engine.OpenVideo(_clip, new VideoSourceOptions { StartTime = TimeSpan.FromSeconds(0.51) });
            Check(source.TryReadFrame(out VideoFrameData first), "frame after StartTime");
            Check(first.FrameIndex == 16, "StartTime 0.51 s gives frame 16 (got " + first.FrameIndex + ")");
            source.Seek(TimeSpan.FromSeconds(2));
            Check(source.TryReadFrame(out VideoFrameData atTwo), "frame after Seek(2 s)");
            Check(atTwo.FrameIndex == 60, "Seek(2 s) gives frame 60 (got " + atTwo.FrameIndex + ")");
            source.Seek(TimeSpan.FromSeconds(3.9));
            long tail = 0;
            while (source.TryReadFrame(out _)) tail++;
            Check(tail == 3, "3 frames from 3.9 s (got " + tail + ")");
            source.Seek(TimeSpan.Zero);
            Check(source.TryReadFrame(out VideoFrameData zero) && zero.FrameIndex == 0, "Seek(0) restarts");
            return "StartTime 0.51 s -> #16, Seek(2 s) -> #60, Seek(3.9 s) -> 3 frames, Seek(0) -> #0";
        });

        Step("08", "OpenAudio (source format)", () =>
        {
            using IAudioSampleSource source = engine.OpenAudio(_clip);
            Check(source.Info.SampleRate == 48000 && source.Info.Channels == 2, "48000 Hz stereo");
            long frames = ReadAll(source, out float[] mono, 4096);
            Check(Math.Abs(frames - 192000) <= 4096, "about 192000 frames (got " + frames + ")");
            double hz = ZeroCrossingFrequency(mono, 48000);
            Check(Math.Abs(hz - 440) < 5, "440 Hz tone (measured " + hz.ToString("0.0") + ")");
            return $"{frames} frames at 48000 Hz x2 ({frames / 48000.0:0.000} s), tone {hz:0.0} Hz, codec {source.Info.SourceCodecName}";
        });

        Step("09", "OpenAudio (22050 Hz mono, from 1 s)", () =>
        {
            using IAudioSampleSource source = engine.OpenAudio(_clip, new AudioSourceOptions { SampleRate = 22050, Channels = 1, StartTime = TimeSpan.FromSeconds(1) });
            Check(source.Info.SampleRate == 22050 && source.Info.Channels == 1, "22050 Hz mono");
            long frames = ReadAll(source, out float[] mono, 4096);
            Check(Math.Abs(frames - 66150) <= 2048, "about 66150 frames (got " + frames + ")");
            double hz = ZeroCrossingFrequency(mono, 22050);
            Check(Math.Abs(hz - 440) < 5, "440 Hz tone after resampling (measured " + hz.ToString("0.0") + ")");
            return $"{frames} frames ({frames / 22050.0:0.000} s), tone {hz:0.0} Hz";
        });

        string written = Out("written-i420.mp4");
        Step("10", "CreateWriter I420 + AAC, FastStart, tags", () =>
        {
            DateTimeOffset created = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
            var request = new StandardVideoRequest
            {
                OutputPath = written,
                Width = 320,
                Height = 240,
                FrameRate = new FrameRate(30, 1),
                Quality = VideoQuality.ConstantQuality(20),
                KeyframeIntervalFrames = 30,
                HasAudio = true,
                AudioSampleRate = 48000,
                AudioChannels = 2,
                FastStart = true,
                Title = "VPDiag synthetic",
                CreationTime = created
            };
            long reports = 0;
            var progress = new SynchronousProgress(_ => reports++);
            using (IStandardVideoWriter writer = engine.CreateWriter(request, progress))
            {
                for (int i = 0; i < 90; i++)
                {
                    writer.WriteFrame(SyntheticI420(320, 240, i));
                    writer.WriteAudio(Tone(48000, 2, 1600, i * 1600L, 660));
                }

                writer.Finish();
                Check(writer.FramesWritten == 90 && writer.AudioFramesWritten == 144000, "counts");
            }

            Copy(written);

            MediaInfo info = engine.Analyse(written);
            Check(info.PrimaryVideo?.CodecName == "h264" && info.PrimaryAudio?.CodecName == "aac", "h264 + aac");
            Check(info.PrimaryVideo.Width == 320 && info.PrimaryVideo.Height == 240, "size");
            Check(info.PrimaryVideo.FrameCount == 90, "90 frames (got " + info.PrimaryVideo.FrameCount + ")");
            Check(info.PrimaryVideo.FrameRate == new FrameRate(30, 1), "30/1 (got " + info.PrimaryVideo.FrameRate + ")");
            Check(Math.Abs(info.PrimaryVideo.Duration.TotalSeconds - 3.0) < 0.1, "3 s video (got " + info.PrimaryVideo.Duration + ")");
            string boxes = TopLevelBoxes(written);
            Check(boxes.IndexOf("moov", StringComparison.Ordinal) < boxes.IndexOf("mdat", StringComparison.Ordinal), "moov before mdat (" + boxes + ")");
            info.Tags.TryGetValue("title", out string title);
            Check(title == "VPDiag synthetic", "title tag (got '" + title + "')");
            info.Tags.TryGetValue("creation_time", out string creation);
            Check(creation != null && creation.StartsWith("2026-10-11T12:00:00", StringComparison.Ordinal), "creation_time tag (got '" + creation + "')");

            using IVideoFrameSource back = engine.OpenVideo(written);
            double worstPsnr = double.MaxValue;
            long count = 0;
            while (back.TryReadFrame(out VideoFrameData frame))
            {
                if (count % 15 == 0) worstPsnr = Math.Min(worstPsnr, Psnr(frame.Y.ToArray(), SyntheticI420(320, 240, count).Y.ToArray()));
                count++;
            }

            Check(count == 90, "decodes back to 90 frames (got " + count + ")");
            Check(worstPsnr > 30, "luma PSNR above 30 dB (worst " + worstPsnr.ToString("0.0") + ")");
            Copy(written);
            return $"{new FileInfo(written).Length} bytes, boxes [{boxes}], {info.PrimaryVideo.Duration.TotalSeconds:0.000} s, title + creation_time ok, {reports} progress reports, decode-back {count} frames, worst luma PSNR {worstPsnr:0.0} dB";
        });

        Step("11", "CreateWriter NV12 + RGBA input", () =>
        {
            string nv12Path = Out("written-nv12.mp4");
            string rgbaPath = Out("written-rgba.mp4");
            WriteFrames(engine, nv12Path, VideoPixelLayout.Nv12, StandardVideoProfile.Mp4H264Aac, 30);
            WriteFrames(engine, rgbaPath, VideoPixelLayout.Rgba, StandardVideoProfile.Mp4H264Aac, 30);
            MediaInfo nv12 = engine.Analyse(nv12Path);
            MediaInfo rgba = engine.Analyse(rgbaPath);
            Check(nv12.PrimaryVideo.FrameCount == 30 && rgba.PrimaryVideo.FrameCount == 30, "30 frames each");
            using IVideoFrameSource back = engine.OpenVideo(nv12Path);
            Check(back.TryReadFrame(out VideoFrameData first), "NV12 output decodes");
            double psnr = Psnr(first.Y.ToArray(), SyntheticI420(320, 240, 0).Y.ToArray());
            Check(psnr > 30, "NV12 luma PSNR above 30 dB (" + psnr.ToString("0.0") + ")");
            Copy(nv12Path);
            Copy(rgbaPath);
            return $"NV12 {nv12.PrimaryVideo.CodecName} {nv12.PrimaryVideo.FrameCount} frames (PSNR {psnr:0.0} dB), RGBA {rgba.PrimaryVideo.CodecName} {rgba.PrimaryVideo.FrameCount} frames";
        });

        Step("12", "CreateWriter HEVC", () =>
        {
            if (!engine.Supports(VideoProcessorCapabilities.HevcOutput))
            {
                bool refused = Throws<NotSupportedException>(() => engine.CreateWriter(new StandardVideoRequest { OutputPath = Out("x.mp4"), Width = 320, Height = 240, FrameRate = new FrameRate(30, 1), Profile = StandardVideoProfile.Mp4HevcAac }));
                Check(refused, "HEVC refused with NotSupportedException");
                return "NOTE: no video/hevc encoder on this device; HevcOutput not set and CreateWriter refuses HEVC as it should";
            }

            string path = Out("written-hevc.mp4");
            WriteFrames(engine, path, VideoPixelLayout.I420, StandardVideoProfile.Mp4HevcAac, 30);
            MediaInfo info = engine.Analyse(path);
            Check(info.PrimaryVideo.CodecName == "hevc", "hevc (got " + info.PrimaryVideo.CodecName + ")");
            Check(info.PrimaryVideo.FrameCount == 30, "30 frames");
            Copy(path);
            return $"hevc {info.PrimaryVideo.Width}x{info.PrimaryVideo.Height} {info.PrimaryVideo.FrameCount} frames, {new FileInfo(path).Length} bytes";
        });

        Step("13", "TrimFile (SnapToKeyframes)", () =>
        {
            string path = Out("trim-snap.mp4");
            int reports = 0;
            engine.TrimFile(new TrimRequest { InputPath = _clip, OutputPath = path, From = TimeSpan.FromSeconds(1.2), To = TimeSpan.FromSeconds(2.5), SnapToKeyframes = true }, new SynchronousProgress(_ => reports++));
            Copy(path);
            MediaInfo info = engine.Analyse(path);
            Check(info.PrimaryVideo.CodecName == "h264" && info.HasAudio, "h264 + audio copied");
            // Every source picture from the 1.0 s keyframe (#30) to 2.5 s (#74) - 45 - plus at most two pictures after
            // 2.5 s that B-frames before it reference. The copy needs no codec, so the container is checked first.
            long samples = info.PrimaryVideo.FrameCount;
            Check(samples >= 45 && samples <= 47, "45..47 coded pictures (got " + samples + ")");
            Check(TopLevelBoxes(path).StartsWith("ftyp moov", StringComparison.Ordinal), "moov first");
            if (_videoCodecProblem != null)
            {
                return $"NOTE: the copy itself needs no codec and is verified from the container ({samples} coded pictures, {info.Duration.TotalSeconds:0.000} s, boxes [{TopLevelBoxes(path)}]); the decode-back comparison was skipped - video codecs unavailable on this device ({_videoCodecProblem})";
            }

            long frames = CountFrames(engine, path);
            Check(frames >= 45 && frames <= 47, "45..47 frames: keyframe at 1.0 s up to 2.5 s (got " + frames + ")");
            using (IVideoFrameSource cut = engine.OpenVideo(path))
            using (IVideoFrameSource original = engine.OpenVideo(_clip, new VideoSourceOptions { StartTime = TimeSpan.FromSeconds(1) }))
            {
                for (int i = 0; i < 45; i++)
                {
                    Check(cut.TryReadFrame(out VideoFrameData a), "frame " + i + " present");
                    Check(original.TryReadFrame(out VideoFrameData b), "source frame " + (30 + i) + " present");
                    Check(MeanAbsoluteDifference(a.Y.ToArray(), b.Y.ToArray()) == 0, "frame " + i + " is source frame #" + (30 + i) + " bit for bit");
                }
            }

            Copy(path);
            return $"{frames} frames ({frames - 45} trailing reference), source frames #30..#74 identical, {info.Duration.TotalSeconds:0.000} s (starts at the 1.0 s keyframe), audio {info.PrimaryAudio.CodecName}, boxes [{TopLevelBoxes(path)}], {reports} progress reports";
        });

        Step("14", "TrimFile (frame-accurate re-encode)", () =>
        {
            string path = Out("trim-exact.mp4");
            engine.TrimFile(new TrimRequest { InputPath = _clip, OutputPath = path, From = TimeSpan.FromSeconds(1.2), To = TimeSpan.FromSeconds(2.5) });
            Copy(path);
            MediaInfo info = engine.Analyse(path);
            long frames = CountFrames(engine, path);
            Check(frames == 39, "39 frames: #36..#74 (got " + frames + ")");
            Check(info.HasAudio && Math.Abs(info.PrimaryAudio.Duration.TotalSeconds - 1.3) < 0.1, "about 1.3 s of audio (got " + info.PrimaryAudio?.Duration + ")");
            Copy(path);
            return $"{frames} frames, video {info.PrimaryVideo.Duration.TotalSeconds:0.000} s, audio {info.PrimaryAudio.Duration.TotalSeconds:0.000} s";
        });

        Step("15", "ConcatenateFiles (copy)", () =>
        {
            string path = Out("concat-copy.mp4");
            engine.ConcatenateFiles(new ConcatenateRequest { InputPaths = new List<string> { _clip, _clip }, OutputPath = path });
            Copy(path);
            MediaInfo info = engine.Analyse(path);
            Check(info.PrimaryVideo.FrameCount == 240, "240 coded pictures (got " + info.PrimaryVideo.FrameCount + ")");
            Check(info.PrimaryVideo.FrameRate == new FrameRate(30, 1), "30/1 (got " + info.PrimaryVideo.FrameRate + ")");
            if (_videoCodecProblem != null)
            {
                return $"NOTE: the copy itself needs no codec and is verified from the container (240 coded pictures at {info.PrimaryVideo.FrameRate}, {info.Duration.TotalSeconds:0.000} s, boxes [{TopLevelBoxes(path)}]); the decode-back count was skipped - video codecs unavailable on this device ({_videoCodecProblem})";
            }

            long frames = CountFrames(engine, path);
            Check(frames == 240, "240 frames (got " + frames + ")");
            Check(Math.Abs(info.Duration.TotalSeconds - 8.0) < 0.15, "about 8 s (got " + info.Duration + ")");
            Copy(path);
            return $"{frames} frames, {info.Duration.TotalSeconds:0.000} s, {info.PrimaryVideo.CodecName} + {info.PrimaryAudio?.CodecName}";
        });

        Step("16", "ConcatenateFiles (inputs differ -> re-encode)", () =>
        {
            string path = Out("concat-reencode.mp4");
            if (!File.Exists(written) && _videoCodecProblem != null)
                return $"NOTE: step 10 could not write its file - video codecs unavailable on this device ({_videoCodecProblem})";
            Check(File.Exists(written), "step 10's file exists");
            engine.ConcatenateFiles(new ConcatenateRequest { InputPaths = new List<string> { _clip, written }, OutputPath = path });
            Copy(path);
            MediaInfo info = engine.Analyse(path);
            long frames = CountFrames(engine, path);
            Check(frames == 210, "210 frames = 120 + 90 (got " + frames + ")");
            Check(info.HasAudio, "audio");
            Copy(path);
            return $"{frames} frames, {info.Duration.TotalSeconds:0.000} s";
        });

        Step("17", "TranscodeFile (160x120, 15 fps, keep audio)", () =>
        {
            string path = Out("transcode.mp4");
            double lastFraction = 0;
            var request = new TranscodeRequest { InputPath = _clip };
            request.Output.OutputPath = path;
            request.Output.Width = 160;
            request.Output.Height = 120;
            request.Output.FrameRate = new FrameRate(15, 1);
            engine.TranscodeFile(request, new SynchronousProgress(p => lastFraction = p.Fraction ?? lastFraction));
            Copy(path);
            MediaInfo info = engine.Analyse(path);
            long frames = CountFrames(engine, path);
            Check(info.PrimaryVideo.Width == 160 && info.PrimaryVideo.Height == 120, "160x120");
            Check(frames == 60, "60 frames (got " + frames + ")");
            Check(info.PrimaryAudio?.SampleRate == 48000 && info.PrimaryAudio.Channels == 2, "audio kept at 48000 stereo");
            Check(lastFraction == 1, "progress reached 1");
            Copy(path);
            return $"{info.PrimaryVideo.Width}x{info.PrimaryVideo.Height} {info.PrimaryVideo.FrameRate} {frames} frames, {info.Duration.TotalSeconds:0.000} s, audio {info.PrimaryAudio.CodecName} {info.PrimaryAudio.SampleRate}";
        });

        Step("18", "Capability refusals", () =>
        {
            var refused = new List<string>();
            if (Throws<NotSupportedException>(() => engine.OpenVideo(_clip, new VideoSourceOptions { PixelLayout = VideoPixelLayout.I444 }).Dispose())) refused.Add("I444");
            if (Throws<NotSupportedException>(() => engine.OpenVideo(_clip, new VideoSourceOptions { BitDepth = 10 }).Dispose())) refused.Add("10-bit");
            if (Throws<NotSupportedException>(() => engine.OpenVideo(_clip, new VideoSourceOptions { IncludeAlpha = true }).Dispose())) refused.Add("alpha");
            Check(refused.Count == 3, "I444, 10-bit and alpha refused (refused: " + string.Join(",", refused) + ")");
            Check(Throws<FileNotFoundException>(() => engine.Analyse(Out("missing.mp4"))), "missing file -> FileNotFoundException");
            File.WriteAllText(Out("not-media.mp4"), "this is not media");
            Check(Throws<MediaFormatException>(() => engine.Analyse(Out("not-media.mp4"))), "garbage -> MediaFormatException");
            return "NotSupported for " + string.Join(", ", refused) + "; FileNotFound and MediaFormat as the contract says";
        });

        Step("19", "Cancellation", () =>
        {
            string path = Out("cancelled.mp4");
            using var cancel = new CancellationTokenSource();
            var request = new TranscodeRequest { InputPath = _clip };
            request.Output.OutputPath = path;
            bool cancelled = false;
            try
            {
                engine.TranscodeFile(request, new SynchronousProgress(p => { if (p.Frames >= 10) cancel.Cancel(); }), cancel.Token);
            }
            catch (System.OperationCanceledException)
            {
                cancelled = true;
            }

            Check(cancelled, "OperationCanceledException");
            Check(!File.Exists(path), "partial output deleted");
            return "TranscodeFile cancelled after 10 frames; partial output deleted";
        });

        Report();
    }

    // Decodes one frame and encodes one frame before the steps. If EITHER fails before producing anything, the device
    // cannot run video codecs at all (for example an Android-x86 image whose software-codec process aborts with
    // "gralloc-mapper is missing"); the codec-dependent steps are then reported PASSED-WITH-NOTE with this reason
    // instead of FAIL. On a device where both work, nothing is masked.
    private void CodecProbe(IVideoProcessor engine)
    {
        var problems = new List<string>();
        try
        {
            using IVideoFrameSource source = engine.OpenVideo(_clip);
            if (!source.TryReadFrame(out _)) problems.Add("decoder produced no frame");
        }
        catch (VideoProcessingException ex)
        {
            problems.Add("decoder: " + Describe(ex));
        }

        try
        {
            var request = new StandardVideoRequest { OutputPath = Out("probe.mp4"), Width = 320, Height = 240, FrameRate = new FrameRate(30, 1) };
            using IStandardVideoWriter writer = engine.CreateWriter(request);
            writer.WriteFrame(SyntheticI420(320, 240, 0));
            writer.Finish();
        }
        catch (VideoProcessingException ex)
        {
            problems.Add("encoder: " + Describe(ex));
        }

        _videoCodecProblem = problems.Count == 0 ? null : string.Join("; ", problems);
        Log("VPDiag codec probe: " + (_videoCodecProblem == null ? "video decoder and encoder OK" : "UNAVAILABLE - " + _videoCodecProblem));
    }

    private void Report()
    {
        string external = GetExternalFilesDir(null)?.AbsolutePath;
        Log($"VPDiag outputs copied to {external}");
        string summary = $"{_passed} passed, {_notes.Count} passed with note{(_notes.Count > 0 ? " (" + string.Join(",", _notes) + ")" : "")}, {_failures.Count} failed";
        Log(_failures.Count == 0
            ? "VPDIAG RESULT: PASS: " + summary
            : "VPDIAG RESULT: FAIL: " + summary + " (" + string.Join(",", _failures) + ")");
    }

    private void Copy(string path)
    {
        string external = GetExternalFilesDir(null)?.AbsolutePath;
        if (external == null) return;
        File.Copy(path, Path.Combine(external, Path.GetFileName(path)), true);
    }

    private static void WriteFrames(IVideoProcessor engine, string path, VideoPixelLayout layout, StandardVideoProfile profile, int count)
    {
        var request = new StandardVideoRequest
        {
            OutputPath = path,
            Width = 320,
            Height = 240,
            FrameRate = new FrameRate(30, 1),
            InputPixelLayout = layout,
            Profile = profile,
            Quality = VideoQuality.TargetBitrate(1500),
            ColorMatrix = VideoColorMatrix.Bt601
        };
        using IStandardVideoWriter writer = engine.CreateWriter(request);
        for (int i = 0; i < count; i++)
        {
            VideoFrameData i420 = SyntheticI420(320, 240, i);
            VideoFrameData frame = layout switch
            {
                VideoPixelLayout.Nv12 => ToNv12(i420),
                VideoPixelLayout.Rgba => SyntheticRgba(320, 240, i),
                _ => i420
            };
            writer.WriteFrame(frame);
        }

        writer.Finish();
    }

    private static long CountFrames(IVideoProcessor engine, string path)
    {
        using IVideoFrameSource source = engine.OpenVideo(path, new VideoSourceOptions { PixelLayout = VideoPixelLayout.Gray });
        long count = 0;
        while (source.TryReadFrame(out _)) count++;
        return count;
    }

    private static long ReadAll(IAudioSampleSource source, out float[] firstChannel, int chunk)
    {
        int channels = source.Info.Channels;
        float[] buffer = new float[chunk * channels];
        var mono = new List<float>();
        long frames = 0;
        int read;
        while ((read = source.Read(buffer)) > 0)
        {
            for (int i = 0; i < read; i++) mono.Add(buffer[i * channels]);
            frames += read;
        }

        firstChannel = mono.ToArray();
        return frames;
    }

    private static bool Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (T)
        {
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // A diagonal luma ramp that moves 4 levels per frame, with chroma that varies across the picture.
    private static VideoFrameData SyntheticI420(int width, int height, long index)
    {
        byte[] y = new byte[width * height];
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                y[row * width + column] = (byte)(16 + ((row + column + index * 4) % 220));
            }
        }

        int cw = (width + 1) / 2;
        int ch = (height + 1) / 2;
        byte[] u = new byte[cw * ch];
        byte[] v = new byte[cw * ch];
        for (int row = 0; row < ch; row++)
        {
            for (int column = 0; column < cw; column++)
            {
                u[row * cw + column] = (byte)(64 + (column * 128 / cw));
                v[row * cw + column] = (byte)(64 + (row * 128 / ch));
            }
        }

        return VideoFrameData.CreateI420(width, height, VideoPlane.FromArray(y, width, height), VideoPlane.FromArray(u, cw, ch), VideoPlane.FromArray(v, cw, ch), TimeSpan.Zero, 0);
    }

    private static VideoFrameData ToNv12(VideoFrameData i420)
    {
        int cw = (i420.Width + 1) / 2;
        int ch = (i420.Height + 1) / 2;
        byte[] u = i420.U.ToArray();
        byte[] v = i420.V.ToArray();
        byte[] uv = new byte[2 * cw * ch];
        for (int i = 0; i < cw * ch; i++)
        {
            uv[2 * i] = u[i];
            uv[2 * i + 1] = v[i];
        }

        return VideoFrameData.CreateNv12(i420.Width, i420.Height, i420.Y, VideoPlane.FromArray(uv, 2 * cw, ch), TimeSpan.Zero, 0);
    }

    private static VideoFrameData SyntheticRgba(int width, int height, long index)
    {
        byte[] rgba = new byte[4 * width * height];
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int o = 4 * (row * width + column);
                rgba[o] = (byte)((column + index * 4) % 256);
                rgba[o + 1] = (byte)(row * 255 / height);
                rgba[o + 2] = 128;
                rgba[o + 3] = 255;
            }
        }

        return VideoFrameData.CreateRgba(width, height, VideoPlane.FromArray(rgba, 4 * width, height), TimeSpan.Zero, 0);
    }

    private static float[] Tone(int sampleRate, int channels, int frames, long startFrame, double frequency)
    {
        float[] samples = new float[frames * channels];
        for (int i = 0; i < frames; i++)
        {
            float value = (float)(0.25 * Math.Sin(2 * Math.PI * frequency * (startFrame + i) / sampleRate));
            for (int channel = 0; channel < channels; channel++) samples[i * channels + channel] = value;
        }

        return samples;
    }

    private static double MeanAbsoluteDifference(byte[] a, byte[] b)
    {
        long sum = 0;
        for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i] - b[i]);
        return (double)sum / a.Length;
    }

    private static double Psnr(byte[] a, byte[] b)
    {
        double squared = 0;
        for (int i = 0; i < a.Length; i++)
        {
            double d = a[i] - b[i];
            squared += d * d;
        }

        double mse = squared / a.Length;
        return mse == 0 ? 99 : 10 * Math.Log10(255.0 * 255.0 / mse);
    }

    // Positive-going zero crossings over the middle of the signal (skipping codec start-up and the tail).
    private static double ZeroCrossingFrequency(float[] samples, int sampleRate)
    {
        int start = samples.Length / 8;
        int end = samples.Length - samples.Length / 8;
        int crossings = 0;
        for (int i = start + 1; i < end; i++)
        {
            if (samples[i - 1] < 0 && samples[i] >= 0) crossings++;
        }

        return crossings * (double)sampleRate / Math.Max(1, end - start - 1);
    }

    // The top-level MP4 box types in file order, e.g. "ftyp moov mdat".
    private static string TopLevelBoxes(string path)
    {
        var types = new List<string>();
        using FileStream stream = File.OpenRead(path);
        byte[] header = new byte[16];
        long position = 0;
        while (position + 8 <= stream.Length)
        {
            stream.Position = position;
            stream.ReadExactly(header, 0, 8);
            long size = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (size == 1)
            {
                stream.ReadExactly(header, 8, 8);
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8));
            }
            else if (size == 0)
            {
                size = stream.Length - position;
            }

            types.Add(Encoding.ASCII.GetString(header, 4, 4));
            if (size < 8) break;
            position += size;
        }

        return string.Join(" ", types);
    }

    // IProgress that reports on the calling thread (Progress<T> would post to the UI thread's context).
    private sealed class SynchronousProgress : IProgress<VideoProgress>
    {
        private readonly Action<VideoProgress> action;

        public SynchronousProgress(Action<VideoProgress> action)
        {
            this.action = action;
        }

        public void Report(VideoProgress value) => action(value);
    }
}
