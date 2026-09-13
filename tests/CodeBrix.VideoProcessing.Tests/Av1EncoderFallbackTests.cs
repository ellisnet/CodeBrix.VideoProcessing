using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Arguments;
using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Exceptions;
using CodeBrix.VideoProcessing.Tests.Resources;
using CodeBrix.VideoProcessing.Tests.Utilities;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests;

/// <summary>
///     The missing-SVT-AV1 error and the opt-in <see cref="FFOptions.AllowAv1EncoderFallback" />.
/// </summary>
/// <remarks>
///     The tests that run ffmpeg against an SVT-AV1 request only mean something on a machine whose ffmpeg has NO
///     libsvtav1, so they skip themselves wherever it is present (and the fallback ones also where libaom-av1 is
///     missing). The tests that exercise the command rewrite and the message text do not run ffmpeg at all and run
///     everywhere.
/// </remarks>
public class Av1EncoderFallbackTests
{
    private const string SvtAv1 = "libsvtav1";
    private const string AomAv1 = "libaom-av1";

    private const string SvtAv1PresentReason =
        "This machine's ffmpeg has libsvtav1, so a missing SVT-AV1 encoder cannot be reproduced here";

    private const string AomAv1MissingReason =
        "This machine's ffmpeg has no libaom-av1 either, so there is nothing for the fallback to use";

    #region The command rewrite - no ffmpeg needed

    [Fact]
    public void Fallback_Is_Off_By_Default()
    {
        (new FFOptions().AllowAv1EncoderFallback).Should().BeFalse();
    }

    [Fact]
    public void Fallback_Option_Loads_From_Json_And_Survives_Clone()
    {
        var options = JsonSerializer.Deserialize<FFOptions>("""{ "AllowAv1EncoderFallback": true }""");

        (options.AllowAv1EncoderFallback).Should().BeTrue();
        (options.Clone().AllowAv1EncoderFallback).Should().BeTrue();
    }

    [Fact]
    public void Fallback_Rewrites_An_SvtAv1_Output_For_LibAom()
    {
        var arguments = FFMpegArguments.FromFileInput("input.mp4");
        var processor = arguments.OutputToFile("output.mkv", true, options => options
            .WithVideoCodec(SvtAv1)
            .WithSpeedPreset(8)
            .WithConstantRateFactor(30)
            .WithEncoderParams("svtav1-params", ("tune", "0"), ("film-grain", "8")));

        var outcome = Av1EncoderFallback.Apply(arguments.Arguments, OnlyLibAomIsAvailable);

        (outcome.Applied).Should().BeTrue();
        (outcome.Problem).Should().BeNull();
        (processor.Arguments).Should().Be("-i \"input.mp4\" -c:v libaom-av1 -cpu-used 8 -crf 30 -b:v 0 \"output.mkv\" -y");
        (outcome.Notes.Any(note => note.Contains("svtav1-params") && note.Contains("dropped"))).Should().BeTrue();
    }

    [Fact]
    public void Fallback_Caps_An_SvtAv1_Preset_At_The_Fastest_LibAom_Speed()
    {
        var arguments = FFMpegArguments.FromFileInput("input.mp4");
        var processor = arguments.OutputToFile("output.mkv", true, options => options
            .WithVideoCodec(SvtAv1)
            .WithSpeedPreset(12));

        var outcome = Av1EncoderFallback.Apply(arguments.Arguments, OnlyLibAomIsAvailable);

        (processor.Arguments).Should().Be("-i \"input.mp4\" -c:v libaom-av1 -cpu-used 8 \"output.mkv\" -y");
        (outcome.Notes.Any(note => note.Contains("-preset 12 became -cpu-used 8"))).Should().BeTrue();
    }

    [Fact]
    public void Fallback_Rewrites_Custom_Codec_Preset_And_Crf_Arguments()
    {
        var arguments = FFMpegArguments.FromFileInput("input.mp4");
        var processor = arguments.OutputToFile("output.webm", true, options => options
            .WithCustomArgument("-c:v libsvtav1")
            .WithCustomArgument("-preset 4")
            .WithCustomArgument("-crf 35")
            .WithCustomArgument("-svtav1-params tune=0"));

        var outcome = Av1EncoderFallback.Apply(arguments.Arguments, OnlyLibAomIsAvailable);

        (outcome.Applied).Should().BeTrue();
        (processor.Arguments).Should().Be("-i \"input.mp4\" -c:v libaom-av1 -cpu-used 4 -crf 35 -b:v 0 \"output.webm\" -y");
    }

    [Fact]
    public void Fallback_Keeps_An_Explicit_Video_Bitrate()
    {
        var arguments = FFMpegArguments.FromFileInput("input.mp4");
        var processor = arguments.OutputToFile("output.mkv", true, options => options
            .WithVideoCodec(SvtAv1)
            .WithConstantRateFactor(30)
            .WithVideoBitrate(500));

        Av1EncoderFallback.Apply(arguments.Arguments, OnlyLibAomIsAvailable);

        (processor.Arguments).Should().Be("-i \"input.mp4\" -c:v libaom-av1 -crf 30 -b:v 500k \"output.mkv\" -y");
    }

    [Fact]
    public void Fallback_Rewrites_Only_The_Output_That_Asks_For_SvtAv1()
    {
        var arguments = FFMpegArguments.FromFileInput("input.mp4");
        var processor = arguments.MultiOutput(outputs => outputs
            .OutputToFile("h264.mp4", true, options => options
                .WithVideoCodec(VideoCodec.LibX264)
                .WithSpeedPreset(Speed.Medium)
                .WithConstantRateFactor(20))
            .OutputToFile("av1.mkv", true, options => options
                .WithVideoCodec(SvtAv1)
                .WithSpeedPreset(6)));

        Av1EncoderFallback.Apply(arguments.Arguments, OnlyLibAomIsAvailable);

        (processor.Arguments).Should().Be(
            "-i \"input.mp4\" -c:v libx264 -preset medium -crf 20 \"h264.mp4\" -y -c:v libaom-av1 -cpu-used 6 \"av1.mkv\" -y");
    }

    [Fact]
    public void Fallback_Leaves_The_Command_Alone_When_SvtAv1_Is_Available()
    {
        var arguments = FFMpegArguments.FromFileInput("input.mp4");
        var processor = arguments.OutputToFile("output.mkv", true, options => options
            .WithVideoCodec(SvtAv1)
            .WithSpeedPreset(8)
            .WithConstantRateFactor(30));
        var before = processor.Arguments;

        var outcome = Av1EncoderFallback.Apply(arguments.Arguments, _ => true);

        (outcome.Applied).Should().BeFalse();
        (outcome.Problem).Should().BeNull();
        (outcome.Notes.Count).Should().Be(0);
        (processor.Arguments).Should().Be(before);
    }

    [Fact]
    public void Fallback_Leaves_The_Command_Alone_And_Says_Why_When_LibAom_Is_Missing_Too()
    {
        var arguments = FFMpegArguments.FromFileInput("input.mp4");
        var processor = arguments.OutputToFile("output.mkv", true, options => options
            .WithVideoCodec(SvtAv1)
            .WithSpeedPreset(8));
        var before = processor.Arguments;

        var outcome = Av1EncoderFallback.Apply(arguments.Arguments, _ => false);

        (outcome.Applied).Should().BeFalse();
        (outcome.Problem).Should().Be(Av1EncoderFallback.LibAomMissingProblem);
        (processor.Arguments).Should().Be(before);
    }

    [Fact]
    public void Fallback_Does_Not_Ask_About_Encoders_When_No_Output_Wants_SvtAv1()
    {
        var questions = 0;
        var arguments = FFMpegArguments.FromFileInput("input.mp4", true, input => input.WithCustomArgument("-c:v libsvtav1"));
        var processor = arguments.OutputToFile("output.mp4", true, options => options
            .WithVideoCodec(VideoCodec.LibX264)
            .WithSpeedPreset(Speed.Fast));
        var before = processor.Arguments;

        var outcome = Av1EncoderFallback.Apply(arguments.Arguments, _ =>
        {
            questions++;
            return false;
        });

        (questions).Should().Be(0);
        (outcome.Applied).Should().BeFalse();
        (processor.Arguments).Should().Be(before);
    }

    [Fact]
    public void Encoder_Listing_Is_Parsed()
    {
        var encoders = Av1EncoderFallback.ParseEncoderListing(new[]
        {
            "Encoders:",
            " V..... = Video",
            " ------",
            " V....D libaom-av1           libaom AV1 (codec av1)",
            " V....D libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10 (codec h264)",
            " A....D libopus              libopus Opus (codec opus)"
        });

        (encoders.Contains(AomAv1)).Should().BeTrue();
        (encoders.Contains("libx264")).Should().BeTrue();
        (encoders.Contains("libopus")).Should().BeTrue();
        (encoders.Contains(SvtAv1)).Should().BeFalse();
    }

    [Fact]
    public void Unknown_Encoder_Is_Found_In_FFMpeg_Error_Output()
    {
        (Av1EncoderFallback.FindUnknownEncoder(new[]
        {
            "  Stream #0:0: Video: wrapped_avframe, yuv420p, 320x240",
            "[vost#0:0 @ 00000168631f8c80] Unknown encoder 'libsvtav1'",
            "[vost#0:0 @ 00000168631f8c80] Error selecting an encoder"
        })).Should().Be(SvtAv1);

        (Av1EncoderFallback.FindUnknownEncoder(new[] { "Error opening output file out.mkv." })).Should().BeNull();
    }

    [Fact]
    public void Missing_SvtAv1_Explanation_Offers_The_Fallback_When_It_Is_Off()
    {
        var explanation = Av1EncoderFallback.DescribeMissingSvtAv1(false, null);

        (explanation).Should().StartWith("The SVT-AV1 encoder library (libsvtav1) is not present on this machine");
        (explanation).Should().Contain("FFOptions.AllowAv1EncoderFallback = true");
        (explanation).Should().Contain("libaom-av1");
    }

    [Fact]
    public void Missing_SvtAv1_Explanation_Says_Why_The_Fallback_Did_Not_Help_When_It_Is_On()
    {
        var explanation = Av1EncoderFallback.DescribeMissingSvtAv1(true, Av1EncoderFallback.LibAomMissingProblem);

        (explanation).Should().StartWith("The SVT-AV1 encoder library (libsvtav1) is not present on this machine");
        (explanation).Should().Contain("is enabled, but the fallback could not be used");
        (explanation).Should().Contain(Av1EncoderFallback.LibAomMissingProblem);
        (explanation).Should().NotContain("AllowAv1EncoderFallback = true");
    }

    #endregion

    #region Against the installed ffmpeg

    [Fact]
    public void Missing_SvtAv1_Throws_A_Helpful_Error_That_Offers_The_Fallback()
    {
        Assert.SkipWhen(FFMpegEncoderList.Has(SvtAv1), SvtAv1PresentReason);

        using var outputFile = new TemporaryFile("out.mkv");
        var processor = SvtAv1Encode(outputFile)
            .Configure(options => options.AllowAv1EncoderFallback = false);

        var exception = Assert.Throws<FFMpegEncoderNotFoundException>(() => processor.ProcessSynchronously());

        AssertHelpfulMissingSvtAv1Error(exception);
        (exception.Message).Should().Contain("FFOptions.AllowAv1EncoderFallback = true");
        (processor.Av1EncoderFallbackApplied).Should().BeFalse();
    }

    [Fact]
    public async Task Missing_SvtAv1_Throws_A_Helpful_Error_That_Offers_The_Fallback_Async()
    {
        Assert.SkipWhen(FFMpegEncoderList.Has(SvtAv1), SvtAv1PresentReason);

        using var outputFile = new TemporaryFile("out.mkv");
        var processor = SvtAv1Encode(outputFile)
            .Configure(options => options.AllowAv1EncoderFallback = false);

        var exception = await Assert.ThrowsAsync<FFMpegEncoderNotFoundException>(() => processor.ProcessAsynchronously());

        AssertHelpfulMissingSvtAv1Error(exception);
        (exception.Message).Should().Contain("FFOptions.AllowAv1EncoderFallback = true");
    }

    [Fact]
    public void Missing_SvtAv1_Is_Still_Reported_As_An_FFMpegException_To_Existing_Callers()
    {
        Assert.SkipWhen(FFMpegEncoderList.Has(SvtAv1), SvtAv1PresentReason);

        using var outputFile = new TemporaryFile("out.mkv");
        var processor = SvtAv1Encode(outputFile)
            .Configure(options => options.AllowAv1EncoderFallback = false);

        FFMpegException caught = null;
        try
        {
            processor.ProcessSynchronously();
        }
        catch (FFMpegException exception)
        {
            caught = exception;
        }

        (caught).Should().NotBeNull();
        (caught.Type).Should().Be(FFMpegExceptionType.Process);
        (caught.FFMpegErrorOutput).Should().Contain("Unknown encoder 'libsvtav1'");
    }

    [Fact]
    public void Fallback_Encodes_Av1_With_LibAom_When_SvtAv1_Is_Missing()
    {
        Assert.SkipWhen(FFMpegEncoderList.Has(SvtAv1), SvtAv1PresentReason);
        Assert.SkipUnless(FFMpegEncoderList.Has(AomAv1), AomAv1MissingReason);

        using var outputFile = new TemporaryFile("out.mkv");
        var processor = SvtAv1Encode(outputFile)
            .Configure(options => options.AllowAv1EncoderFallback = true);

        (processor.ProcessSynchronously()).Should().BeTrue();

        AssertEncodedWithLibAom(processor, outputFile);
    }

    [Fact]
    public async Task Fallback_Encodes_Av1_With_LibAom_When_SvtAv1_Is_Missing_Async()
    {
        Assert.SkipWhen(FFMpegEncoderList.Has(SvtAv1), SvtAv1PresentReason);
        Assert.SkipUnless(FFMpegEncoderList.Has(AomAv1), AomAv1MissingReason);

        using var outputFile = new TemporaryFile("out.webm");
        var processor = SvtAv1Encode(outputFile, options => options.WithEncoderParams("svtav1-params", ("tune", "0")))
            .Configure(options => options.AllowAv1EncoderFallback = true);

        (await processor.ProcessAsynchronously()).Should().BeTrue();

        AssertEncodedWithLibAom(processor, outputFile);
        (processor.Arguments).Should().NotContain("svtav1-params");
        (processor.Av1EncoderFallbackNotes.Any(note => note.Contains("svtav1-params") && note.Contains("dropped"))).Should().BeTrue();
    }

    [Fact]
    public void ZZZ_Fallback_Can_Be_Turned_On_For_Every_Run_Through_The_Global_Options()
    {
        Assert.SkipWhen(FFMpegEncoderList.Has(SvtAv1), SvtAv1PresentReason);
        Assert.SkipUnless(FFMpegEncoderList.Has(AomAv1), AomAv1MissingReason);

        var previous = GlobalFFOptions.Current.AllowAv1EncoderFallback;
        try
        {
            GlobalFFOptions.Configure(options => options.AllowAv1EncoderFallback = true);

            using var outputFile = new TemporaryFile("out.mkv");
            var processor = SvtAv1Encode(outputFile);

            (processor.ProcessSynchronously()).Should().BeTrue();

            AssertEncodedWithLibAom(processor, outputFile);
        }
        finally
        {
            GlobalFFOptions.Configure(options => options.AllowAv1EncoderFallback = previous);
        }
    }

    [Fact]
    public void An_Unknown_Encoder_Other_Than_SvtAv1_Keeps_The_Original_Message()
    {
        const string noSuchEncoder = "codebrix_no_such_encoder";

        using var outputFile = new TemporaryFile("out.mkv");
        var processor = FFMpegArguments
            .FromFileInput(TestResources.Mp4Video)
            .OutputToFile(outputFile, true, options => options
                .WithVideoCodec(noSuchEncoder)
                .DisableChannel(Channel.Audio)
                .WithFrameOutputCount(1))
            .CancellableThrough(TestContext.Current.CancellationToken);

        var exception = Assert.Throws<FFMpegEncoderNotFoundException>(() => processor.ProcessSynchronously());

        (exception.EncoderName).Should().Be(noSuchEncoder);
        (exception.Type).Should().Be(FFMpegExceptionType.Process);
        (exception.Message).Should().StartWith("ffmpeg exited with non-zero exit-code (");
        (exception.Message).Should().NotContain("SVT-AV1");
    }

    #endregion

    private static bool OnlyLibAomIsAvailable(string encoderName)
    {
        return encoderName == AomAv1;
    }

    //A short, small encode: ten frames at 320x180 with no audio keeps libaom - which is slow - to a second or so.
    private static FFMpegArgumentProcessor SvtAv1Encode(string outputPath, Action<FFMpegArgumentOptions> moreOptions = null)
    {
        return FFMpegArguments
            .FromFileInput(TestResources.Mp4Video)
            .OutputToFile(outputPath, true, options =>
            {
                options
                    .WithVideoCodec(SvtAv1)
                    .WithSpeedPreset(12)
                    .WithConstantRateFactor(40)
                    .Resize(320, 180)
                    .DisableChannel(Channel.Audio)
                    .WithFrameOutputCount(10);
                moreOptions?.Invoke(options);
            })
            .CancellableThrough(TestContext.Current.CancellationToken);
    }

    private static void AssertHelpfulMissingSvtAv1Error(FFMpegEncoderNotFoundException exception)
    {
        (exception.EncoderName).Should().Be(SvtAv1);
        (exception.Type).Should().Be(FFMpegExceptionType.Process);
        (exception.FFMpegErrorOutput).Should().Contain("Unknown encoder 'libsvtav1'");

        //The new explanation comes first...
        (exception.Message).Should().StartWith(Av1EncoderFallback.MissingSvtAv1Explanation);

        //...and everything the message used to say follows it, unchanged and in full.
        var originalStart = exception.Message.IndexOf("ffmpeg exited with non-zero exit-code (", StringComparison.Ordinal);
        (originalStart).Should().BeGreaterThan(Av1EncoderFallback.MissingSvtAv1Explanation.Length);
        (exception.Message).Should().EndWith(" - " + exception.FFMpegErrorOutput + ")");
    }

    private static void AssertEncodedWithLibAom(FFMpegArgumentProcessor processor, string outputPath)
    {
        (processor.Av1EncoderFallbackApplied).Should().BeTrue();
        (processor.Av1EncoderFallbackNotes.Count).Should().BeGreaterThan(0);
        (processor.Arguments).Should().Contain("-c:v libaom-av1");
        (processor.Arguments).Should().Contain("-cpu-used 8");
        (processor.Arguments).Should().NotContain(SvtAv1);
        (processor.Arguments).Should().NotContain("-preset");

        (File.Exists(outputPath)).Should().BeTrue();
        var analysis = FFProbe.Analyse(outputPath);
        (analysis.PrimaryVideoStream.CodecName).Should().Be("av1");
    }
}
