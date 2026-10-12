namespace CodeBrix.VideoProcessing.Core.Tools;

/// <summary>
/// The OPTIONAL facet an engine class implements when it can locate and run external command-line tools. It is not
/// a member of <see cref="IVideoProcessor" />, so engines that have no tools are unaffected.
/// </summary>
/// <remarks>
/// An engine that implements this also reports <see cref="VideoProcessorCapabilities.ExternalTools" />. Consumers do
/// not test for the interface themselves: they call
/// <see cref="VideoProcessorExtensions.TryGetExternalTools" />, which checks both.
/// </remarks>
public interface IExternalToolHost
{
    /// <summary>The engine's tools; null means none (as if the capability were not reported).</summary>
    IExternalTools Tools { get; }
}
