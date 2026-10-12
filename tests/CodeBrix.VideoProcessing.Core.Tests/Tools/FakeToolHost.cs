using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Core.Frames;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Output;
using CodeBrix.VideoProcessing.Core.Tools;

namespace CodeBrix.VideoProcessing.Core.Tests.Tools;

/// <summary>An engine that implements the external-tools facet: the fake engine plus a Tools property a test sets.</summary>
internal sealed class FakeToolHost : IVideoProcessor, IExternalToolHost
{
    private readonly FakeVideoProcessor inner;

    public FakeToolHost(VideoProcessorCapabilities capabilities, IExternalTools tools, string name = "host")
    {
        inner = new FakeVideoProcessor(name, capabilities);
        Tools = tools;
    }

    public IExternalTools Tools { get; }

    public string Name => inner.Name;

    public VideoProcessorCapabilities Capabilities => inner.Capabilities;

    public MediaInfo Analyse(string path, CancellationToken cancellationToken = default) => inner.Analyse(path, cancellationToken);

    public Task<MediaInfo> AnalyseAsync(string path, CancellationToken cancellationToken = default) => inner.AnalyseAsync(path, cancellationToken);

    public VideoFrameData Snapshot(string path, TimeSpan at, CancellationToken cancellationToken = default) => inner.Snapshot(path, at, cancellationToken);

    public IVideoFrameSource OpenVideo(string path, VideoSourceOptions options = null) => inner.OpenVideo(path, options);

    public IAudioSampleSource OpenAudio(string path, AudioSourceOptions options = null) => inner.OpenAudio(path, options);

    public IStandardVideoWriter CreateWriter(StandardVideoRequest request, IProgress<VideoProgress> progress = null) => inner.CreateWriter(request, progress);

    public void TranscodeFile(TranscodeRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default) =>
        inner.TranscodeFile(request, progress, cancellationToken);

    public void TrimFile(TrimRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default) =>
        inner.TrimFile(request, progress, cancellationToken);

    public void ConcatenateFiles(ConcatenateRequest request, IProgress<VideoProgress> progress = null, CancellationToken cancellationToken = default) =>
        inner.ConcatenateFiles(request, progress, cancellationToken);
}

/// <summary>A do-nothing <see cref="IExternalTools" /> for the facet-discovery tests.</summary>
internal sealed class NullExternalTools : IExternalTools
{
    public string BinaryFolder => null;

    public ExternalTool Locate(string toolName) => new ExternalTool(toolName, null, "nowhere");

    public ExternalToolResult Run(ExternalToolCommand command, CancellationToken cancellationToken = default) =>
        new ExternalToolResult(0, command.ToolName, string.Empty, string.Empty);
}
