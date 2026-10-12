using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Media;
using CodeBrix.VideoProcessing.Core.Tools;
using CodeBrix.VideoProcessing.Engine;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests.Engine;

public class FFmpegExternalToolsTests
{
    private readonly FFmpegVideoProcessor engine = new FFmpegVideoProcessor();

    [Fact]
    public void the_engine_reports_the_capability_and_offers_its_tools()
    {
        //Act
        bool offered = engine.TryGetExternalTools(out IExternalTools tools);

        //Assert
        engine.Supports(VideoProcessorCapabilities.ExternalTools).Should().BeTrue();
        offered.Should().BeTrue();
        tools.Should().BeSameAs(engine.Tools);
        tools.BinaryFolder.Should().Be(GlobalFFOptions.Current.BinaryFolder);
    }

    [Fact]
    public void ffmpeg_and_ffprobe_are_found_on_this_machine()
    {
        //Act
        ExternalTool ffmpeg = engine.Tools.Locate("ffmpeg");
        ExternalTool ffprobe = engine.Tools.Locate("ffprobe");

        //Assert
        ffmpeg.Found.Should().BeTrue();
        ffprobe.Found.Should().BeTrue();
        Path.IsPathRooted(ffmpeg.Path).Should().BeTrue();
    }

    [Fact]
    public void Run_streams_the_lines_of_ffmpeg_version()
    {
        //Arrange
        var lines = new List<string>();
        var command = new ExternalToolCommand("ffmpeg", new ExternalToolArguments().Add("-hide_banner").Add("-version"))
        {
            OnStandardOutputLine = lines.Add
        };

        //Act
        ExternalToolResult result = engine.Tools.Run(command, TestContext.Current.CancellationToken);

        //Assert
        result.Succeeded.Should().BeTrue();
        lines.Should().NotBeEmpty();
        lines[0].Should().StartWith("ffmpeg version");
        result.StandardOutput.Should().BeEmpty();
        result.RenderedCommand.Should().EndWith("ffmpeg -hide_banner -version");
    }

    [Fact]
    public void a_failing_ffprobe_run_is_a_result_with_its_error_text()
    {
        //Arrange
        var command = new ExternalToolCommand("ffprobe", new ExternalToolArguments().Add("-v error").AddPath("/no/such/file.mp4"));

        //Act
        ExternalToolResult result = engine.Tools.Run(command, TestContext.Current.CancellationToken);

        //Assert
        result.Succeeded.Should().BeFalse();
        result.StandardError.Should().Contain("No such file");
    }

    [Fact]
    public void the_binary_folder_is_read_live_from_the_engines_options()
    {
        //Arrange
        var options = new FFOptions { BinaryFolder = "/first" };
        var own = new FFmpegVideoProcessor(options);

        //Act
        options.BinaryFolder = "/second";

        //Assert
        own.Tools.BinaryFolder.Should().Be("/second");
    }

    [Fact]
    public void Locate_looks_in_the_architecture_folder_then_the_folder_then_the_PATH_like_the_port()
    {
        //Arrange
        string root = Path.Combine(Path.GetTempPath(), "cbvp-ffbin-" + Guid.NewGuid().ToString("N"));
        string architecture = Path.Combine(root, Environment.Is64BitProcess ? "x64" : "x86");
        Directory.CreateDirectory(architecture);
        string name = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var options = new FFOptions { BinaryFolder = root };
        var own = new FFmpegVideoProcessor(options);

        try
        {
            //Act + Assert - only on the PATH
            ExternalTool fromPath = own.Tools.Locate("ffmpeg");
            fromPath.Found.Should().BeTrue();
            fromPath.Path.Should().NotStartWith(root);
            fromPath.LookedIn.Should().Be($"in '{architecture}', then in '{root}', and then on the PATH");

            //Act + Assert - in the folder
            File.WriteAllText(Path.Combine(root, name), string.Empty);
            own.Tools.Locate("ffmpeg").Path.Should().Be(Path.Combine(root, name));
            GlobalFFOptions.GetFFMpegBinaryPath(options).Should().Be(Path.Combine(root, name));

            //Act + Assert - the architecture folder wins, as in the port
            File.WriteAllText(Path.Combine(architecture, name), string.Empty);
            own.Tools.Locate("ffmpeg").Path.Should().Be(Path.Combine(architecture, name));
            GlobalFFOptions.GetFFMpegBinaryPath(options).Should().Be(Path.Combine(architecture, name));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Run_of_a_missing_tool_names_it_where_it_was_looked_for_and_the_setting()
    {
        //Act
        var exception = Assert.Throws<VideoProcessingException>(() =>
            engine.Tools.Run(new ExternalToolCommand("cbvp-no-such-tool"), TestContext.Current.CancellationToken));

        //Assert
        exception.Message.Should().Contain("'cbvp-no-such-tool'");
        exception.Message.Should().Contain("on the PATH");
        exception.Message.Should().Contain("GlobalFFOptions.Configure");
    }

    [Fact]
    public void Analyse_reports_subtitle_streams_and_chapters()
    {
        //Arrange
        using var clip = new TemporaryFile("subtitles-chapters.mkv");
        EngineTestMedia.CreateClipWithSubtitlesAndChapters(clip);

        //Act
        MediaInfo info = engine.Analyse(clip, TestContext.Current.CancellationToken);

        //Assert
        info.HasSubtitles.Should().BeTrue();
        info.SubtitleStreams.Should().HaveCount(1);
        SubtitleStreamInfo subtitle = info.SubtitleStreams[0];
        subtitle.Index.Should().Be(2);
        subtitle.CodecName.Should().Be("subrip");
        subtitle.Language.Should().Be("eng");
        subtitle.Title.Should().Be("English");
        subtitle.Tags["LANGUAGE"].Should().Be("eng");

        info.HasChapters.Should().BeTrue();
        info.Chapters.Should().HaveCount(2);
        info.Chapters[0].Index.Should().Be(0);
        info.Chapters[0].Title.Should().Be("Opening");
        info.Chapters[0].Start.Should().Be(TimeSpan.Zero);
        info.Chapters[0].End.Should().Be(TimeSpan.FromSeconds(1));
        info.Chapters[0].Tags["title"].Should().Be("Opening");
        info.Chapters[1].Index.Should().Be(1);
        info.Chapters[1].Title.Should().Be("Closing");
        info.Chapters[1].Start.Should().Be(TimeSpan.FromSeconds(1));
        info.Chapters[1].End.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Analyse_reports_the_mov_text_track_of_the_shared_subtitle_asset()
    {
        //Act
        MediaInfo info = engine.Analyse("./Assets/clip-h264-aac-srt.mp4", TestContext.Current.CancellationToken);

        //Assert
        info.SubtitleStreams.Should().HaveCount(1);
        info.SubtitleStreams[0].CodecName.Should().Be("mov_text");
        info.SubtitleStreams[0].Language.Should().Be("eng");
        info.SubtitleStreams[0].Index.Should().Be(2);
        info.HasChapters.Should().BeFalse();
        info.HasVideo.Should().BeTrue();
        info.HasAudio.Should().BeTrue();
    }

    [Fact]
    public void Analyse_of_a_file_without_subtitles_or_chapters_reports_empty_lists()
    {
        //Act
        MediaInfo info = engine.Analyse(Resources.TestResources.Mp4Video, TestContext.Current.CancellationToken);

        //Assert
        info.SubtitleStreams.Should().BeEmpty();
        info.Chapters.Should().BeEmpty();
    }
}
