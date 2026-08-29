using System;
using System.IO;
using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Tests.Resources;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests;

public class VideoFilterIntegrationTests
{
    private const string IdentityCubeLut =
        "TITLE \"CodeBrix identity 2x2x2\"\n"
        + "LUT_3D_SIZE 2\n"
        + "0.0 0.0 0.0\n"
        + "1.0 0.0 0.0\n"
        + "0.0 1.0 0.0\n"
        + "1.0 1.0 0.0\n"
        + "0.0 0.0 1.0\n"
        + "1.0 0.0 1.0\n"
        + "0.0 1.0 1.0\n"
        + "1.0 1.0 1.0\n";

    [Fact]
    public void a_lut3d_zscale_fps_format_chain_runs_through_ffmpeg()
    {
        //Arrange - the LUT lives in a folder whose name holds a comma and a space, the two
        //characters that broke the filter chain before the escaping helper existed
        var lutFolder = Path.Combine(Path.GetTempPath(), $"codebrix videoprocessing lut, test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(lutFolder);
        var lutPath = Path.Combine(lutFolder, "identity.cube");
        File.WriteAllText(lutPath, IdentityCubeLut);

        try
        {
            using var outputFile = new TemporaryFile($"out{VideoType.Mp4.Extension}");

            //Act
            var success = FFMpegArguments
                .FromFileInput(TestResources.Mp4Video)
                .OutputToFile(outputFile, false, opt => opt
                    .WithVideoFilters(filterOptions => filterOptions
                        .Lut3D(lutPath)
                        .ZScale(zScale =>
                        {
                            zScale.Width = 64;
                            zScale.Height = 64;
                            zScale.Matrix = ZScaleMatrix.Bt709;
                        })
                        .Fps(10)
                        .Format("yuv420p"))
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithSpeedPreset(Speed.UltraFast))
                .CancellableThrough(TestContext.Current.CancellationToken)
                .ProcessSynchronously();

            //Assert
            (success).Should().BeTrue();
            (File.Exists(outputFile)).Should().BeTrue();

            var analysis = FFProbe.Analyse(outputFile);
            (analysis.PrimaryVideoStream.FrameRate).Should().Be(10d);
            (analysis.PrimaryVideoStream.Width).Should().Be(64);
            (analysis.PrimaryVideoStream.Height).Should().Be(64);
            (analysis.PrimaryVideoStream.PixelFormat).Should().Be("yuv420p");
        }
        finally
        {
            Directory.Delete(lutFolder, true);
        }
    }
}
