using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SilverAssertions;
using CodeBrix.VideoProcessing.Tests.Utilities;
namespace CodeBrix.VideoProcessing.Tests;
public class FFMpegArgumentProcessorTest
{
    private static FFMpegArgumentProcessor CreateArgumentProcessor()
    {
        return FFMpegArguments
            .FromFileInput("")
            .OutputToFile("");
    }

    [Fact]
    public void ZZZ_Processor_GlobalOptions_GetUsed()
    {
        var globalWorkingDir = "Whatever";
        var processor = CreateArgumentProcessor();

        try
        {
            GlobalFFOptions.Configure(new FFOptions { WorkingDirectory = globalWorkingDir });

            var options = processor.GetConfiguredOptions(null);

            (options.WorkingDirectory).Should().Be(globalWorkingDir);
        }
        finally
        {
            GlobalFFOptions.Configure(new FFOptions());
        }
    }

    [Fact]
    public void Processor_SessionOptions_GetUsed()
    {
        var sessionWorkingDir = "./CurrentRunWorkingDir";

        var processor = CreateArgumentProcessor();
        processor.Configure(options => options.WorkingDirectory = sessionWorkingDir);
        var options = processor.GetConfiguredOptions(null);

        (options.WorkingDirectory).Should().Be(sessionWorkingDir);
    }

    [Fact]
    public void ZZZ_Processor_Options_CanBeOverridden_And_Configured()
    {
        var globalConfig = "Whatever";

        try
        {
            var processor = CreateArgumentProcessor();

            var sessionTempDir = "./CurrentRunWorkingDir";
            processor.Configure(options => options.TemporaryFilesFolder = sessionTempDir);

            var overrideOptions = new FFOptions { WorkingDirectory = "override" };

            GlobalFFOptions.Configure(new FFOptions { WorkingDirectory = globalConfig, TemporaryFilesFolder = globalConfig, BinaryFolder = globalConfig });
            var options = processor.GetConfiguredOptions(overrideOptions);

            (overrideOptions.WorkingDirectory).Should().Be(options.WorkingDirectory);
            (overrideOptions.TemporaryFilesFolder).Should().Be(options.TemporaryFilesFolder);
            (overrideOptions.BinaryFolder).Should().Be(options.BinaryFolder);

            (options.TemporaryFilesFolder).Should().Be(sessionTempDir);
            (options.BinaryFolder).Should().NotBe(globalConfig);
        }
        finally
        {
            GlobalFFOptions.Configure(new FFOptions());
        }
    }

    [Fact]
    public void ZZZ_Options_Global_And_Session_Options_Can_Differ()
    {
        var globalWorkingDir = "Whatever";

        try
        {
            var processor1 = CreateArgumentProcessor();
            var sessionWorkingDir = "./CurrentRunWorkingDir";
            processor1.Configure(options => options.WorkingDirectory = sessionWorkingDir);
            var options1 = processor1.GetConfiguredOptions(null);
            (options1.WorkingDirectory).Should().Be(sessionWorkingDir);

            var processor2 = CreateArgumentProcessor();
            GlobalFFOptions.Configure(new FFOptions { WorkingDirectory = globalWorkingDir });
            var options2 = processor2.GetConfiguredOptions(null);
            (options2.WorkingDirectory).Should().Be(globalWorkingDir);
        }
        finally
        {
            GlobalFFOptions.Configure(new FFOptions());
        }
    }
}
