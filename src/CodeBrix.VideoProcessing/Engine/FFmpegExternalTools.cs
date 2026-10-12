using System;
using System.IO;
using System.Threading;
using CodeBrix.VideoProcessing.Core;
using CodeBrix.VideoProcessing.Core.Tools;

namespace CodeBrix.VideoProcessing.Engine;

/// <summary>
/// The ffmpeg engine's external tools (<see cref="IExternalTools" />): ffmpeg, ffprobe or any other tool, looked for
/// where the engine's own options say and run through Core's one <see cref="ExternalToolRunner" />.
/// </summary>
/// <remarks>
/// The folder is read LIVE from the engine's options (its own <see cref="FFOptions" />, else
/// <see cref="GlobalFFOptions.Current" />), so a later <c>GlobalFFOptions.Configure(o =&gt; o.BinaryFolder = ...)</c> is
/// seen. The lookup matches where the ported operations find their binaries
/// (<see cref="GlobalFFOptions.GetFFMpegBinaryPath" />): the folder's "x64" (or "x86") sub-folder, then the folder, then
/// the PATH. The ported operations themselves keep their own runner; only consumers' command lines come through here.
/// </remarks>
internal sealed class FFmpegExternalTools : IExternalTools
{
    private readonly Func<FFOptions> options;

    internal FFmpegExternalTools(Func<FFOptions> options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string BinaryFolder => options().BinaryFolder;

    /// <inheritdoc />
    public ExternalTool Locate(string toolName) => ExternalToolLocator.Locate(toolName, SearchFolders(BinaryFolder));

    /// <inheritdoc />
    public ExternalToolResult Run(ExternalToolCommand command, CancellationToken cancellationToken = default)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));

        ExternalTool tool = Locate(command.ToolName);
        if (!tool.Found)
        {
            throw new VideoProcessingException(
                $"The external tool '{command.ToolName}' was not found: it was looked for {tool.LookedIn}. Install it on the PATH, " +
                "or point the ffmpeg engine at its folder with GlobalFFOptions.Configure(o => o.BinaryFolder = ...).");
        }

        return ExternalToolRunner.Run(tool.Path, command, cancellationToken);
    }

    // The same candidates, in the same order, as the port's binary lookup.
    internal static string[] SearchFolders(string binaryFolder)
    {
        if (string.IsNullOrEmpty(binaryFolder)) return Array.Empty<string>();

        string target = Environment.Is64BitProcess ? "x64" : "x86";
        return new[] { Path.Combine(binaryFolder, target), binaryFolder };
    }
}
