using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;

namespace CodeBrix.VideoProcessing.Core.Tests;

/// <summary>
/// An in-memory engine for the contract tests: "files" are synthetic grey-ramp videos, and writers record what
/// they were given. It implements the mandatory core plus whatever capabilities a test hands it.
/// </summary>
internal sealed class FakeVideoProcessor : IVideoProcessor
{
    public FakeVideoProcessor(string name = "fake", VideoProcessorCapabilities capabilities = VideoProcessorCapabilities.None)
    {
        Name = name;
        Capabilities = capabilities;
    }

    public string Name { get; }

    public VideoProcessorCapabilities Capabilities { get; }

    public int Width { get; set; } = 4;

    public int Height { get; set; } = 2;

    public FrameRate Rate { get; set; } = new FrameRate(25, 1);

    public int FrameCount { get; set; } = 5;

    public List<FakeWriter> Writers { get; } = new List<FakeWriter>();

    public MediaInfo Analyse(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("The path is required.", nameof(path));
        cancellationToken.ThrowIfCancellationRequested();

        return new MediaInfo
        {
            ContainerFormat = "fake",
            Duration = Rate.TimestampOf(FrameCount),
            VideoStreams = new[]
            {
                new VideoStreamInfo
                {
                    Index = 0,
                    CodecName = "rawvideo",
                    Width = Width,
                    Height = Height,
                    FrameRate = Rate,
                    FrameCount = FrameCount,
                    BitDepth = 8,
                    ChromaSubsampling = VideoChromaSubsampling.Yuv420
                }
            }
        };
    }

    public Task<MediaInfo> AnalyseAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(Analyse(path, cancellationToken));

    public VideoFrameData Snapshot(string path, TimeSpan at, CancellationToken cancellationToken = default)
    {
        this.RequireCapability(VideoProcessorCapabilities.Snapshot, nameof(Snapshot));
        using IVideoFrameSource source = OpenVideo(path, new VideoSourceOptions { StartTime = at });
        if (!source.TryReadFrame(out VideoFrameData frame)) throw new MediaFormatException("Past the end.");
        return frame.Copy();
    }

    public IVideoFrameSource OpenVideo(string path, VideoSourceOptions options = null)
    {
        options ??= new VideoSourceOptions();
        options.Validate();
        switch (options.PixelLayout)
        {
            case VideoPixelLayout.Rgba: this.RequireCapability(VideoProcessorCapabilities.RgbaInput, "RGBA frames"); break;
            case VideoPixelLayout.I444: this.RequireCapability(VideoProcessorCapabilities.I444FrameSource, "4:4:4 frames"); break;
            case VideoPixelLayout.Gray: this.RequireCapability(VideoProcessorCapabilities.GrayFrameSource, "grey frames"); break;
        }

        if (options.BitDepth > 8) this.RequireCapability(VideoProcessorCapabilities.HighBitDepthFrameSource, "frames above 8 bits");
        return new FakeFrameSource(this, options.StartTime);
    }

    public IAudioSampleSource OpenAudio(string path, AudioSourceOptions options = null) =>
        throw new MediaFormatException("The fake engine's files have no audio stream.");

    public IStandardVideoWriter CreateWriter(StandardVideoRequest request, IProgress<VideoProgress> progress = null)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.Validate();
        if (request.Profile == StandardVideoProfile.Mp4HevcAac) this.RequireCapability(VideoProcessorCapabilities.HevcOutput, "HEVC output");

        FakeWriter writer = new FakeWriter(request.Clone(), progress);
        Writers.Add(writer);
        return writer;
    }

    public void TranscodeFile(TranscodeRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default)
    {
        this.RequireCapability(VideoProcessorCapabilities.DirectTranscode, nameof(TranscodeFile));
        request.Validate();
    }

    public void TrimFile(TrimRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default)
    {
        this.RequireCapability(VideoProcessorCapabilities.DirectTrim, nameof(TrimFile));
        request.Validate();
    }

    public void ConcatenateFiles(ConcatenateRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default)
    {
        this.RequireCapability(VideoProcessorCapabilities.DirectConcatenate, nameof(ConcatenateFiles));
        request.Validate();
    }

    private sealed class FakeFrameSource : IVideoFrameSource
    {
        private readonly FakeVideoProcessor owner;
        private readonly byte[] y;
        private readonly byte[] u;
        private readonly byte[] v;
        private long next;
        private bool disposed;

        public FakeFrameSource(FakeVideoProcessor owner, TimeSpan start)
        {
            this.owner = owner;
            y = new byte[owner.Width * owner.Height];
            u = new byte[((owner.Width + 1) / 2) * ((owner.Height + 1) / 2)];
            v = new byte[u.Length];
            Info = new VideoSourceInfo
            {
                Width = owner.Width,
                Height = owner.Height,
                FrameRate = owner.Rate,
                EstimatedFrameCount = owner.FrameCount,
                PixelLayout = VideoPixelLayout.I420,
                SourceBitDepth = 8
            };
            Seek(start);
        }

        public VideoSourceInfo Info { get; }

        public bool CanSeek => true;

        public bool TryReadFrame(out VideoFrameData frame)
        {
            if (disposed) throw new ObjectDisposedException(nameof(FakeFrameSource));
            if (next >= owner.FrameCount)
            {
                frame = default;
                return false;
            }

            // The SAME buffers every time - like a real engine - with the frame index painted into them.
            Array.Fill(y, (byte)(16 + next));
            Array.Fill(u, (byte)128);
            Array.Fill(v, (byte)128);
            int cw = (owner.Width + 1) / 2;
            int ch = (owner.Height + 1) / 2;
            frame = VideoFrameData.CreateI420(
                owner.Width,
                owner.Height,
                VideoPlane.FromArray(y, owner.Width, owner.Height),
                VideoPlane.FromArray(u, cw, ch),
                VideoPlane.FromArray(v, cw, ch),
                owner.Rate.TimestampOf(next),
                next);
            next++;
            return true;
        }

        public void Seek(TimeSpan position)
        {
            if (position < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(position));
            long index = owner.Rate.FrameIndexAt(position);
            next = owner.Rate.TimestampOf(index) < position ? index + 1 : index;
        }

        public void Dispose() => disposed = true;
    }

    internal sealed class FakeWriter : IStandardVideoWriter
    {
        private readonly IProgress<VideoProgress> progress;

        public FakeWriter(StandardVideoRequest request, IProgress<VideoProgress> progress)
        {
            Request = request;
            this.progress = progress;
        }

        public StandardVideoRequest Request { get; }

        public long FramesWritten { get; private set; }

        public long AudioFramesWritten { get; private set; }

        public List<VideoFrameData> Frames { get; } = new List<VideoFrameData>();

        public bool Finished { get; private set; }

        public bool Disposed { get; private set; }

        public void WriteFrame(in VideoFrameData frame)
        {
            if (Finished) throw new InvalidOperationException("Finished.");
            if (frame.Width != Request.Width || frame.Height != Request.Height || frame.Layout != Request.InputPixelLayout || frame.BitDepth != 8)
                throw new ArgumentException("The frame does not match the request.", nameof(frame));

            Frames.Add(frame.Copy().WithPosition(Request.FrameRate.TimestampOf(FramesWritten), FramesWritten));
            FramesWritten++;
            progress?.Report(new VideoProgress(null, FramesWritten, Request.FrameRate.TimestampOf(FramesWritten), TimeSpan.Zero));
        }

        public void WriteAudio(ReadOnlySpan<float> interleaved)
        {
            if (!Request.HasAudio) throw new InvalidOperationException("No audio.");
            if (interleaved.Length % Request.AudioChannels != 0) throw new ArgumentException("Not a whole number of frames.", nameof(interleaved));
            AudioFramesWritten += interleaved.Length / Request.AudioChannels;
        }

        public void Finish()
        {
            if (Finished) return;
            if (FramesWritten == 0) throw new InvalidOperationException("No frame was written.");
            Finished = true;
        }

        public void Dispose() => Disposed = true;
    }
}
