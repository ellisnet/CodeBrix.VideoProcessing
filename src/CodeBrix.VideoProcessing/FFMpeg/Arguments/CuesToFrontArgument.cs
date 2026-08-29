using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments;

/// <summary>
///     Represents the WebM / Matroska muxer's <c>-cues_to_front</c> option, which rewrites the file
///     at the end of muxing so the cue (seek index) sits before the media data.
/// </summary>
/// <remarks>
///     Players that stream a file over the network can start seeking as soon as the header arrives
///     when the cues are at the front. The muxer needs a seekable output to do it.
/// </remarks>
public class CuesToFrontArgument : IArgument
{
    /// <summary>
    ///     Turns the cues-to-front rewrite on or off.
    /// </summary>
    /// <param name="enabled"><see langword="true" /> to move the cues to the front of the file.</param>
    public CuesToFrontArgument(bool enabled)
    {
        Enabled = enabled;
    }

    /// <summary>Whether the cues are moved to the front.</summary>
    public bool Enabled { get; }

    /// <summary>The emitted text, <c>-cues_to_front 1</c> or <c>-cues_to_front 0</c>.</summary>
    public string Text => $"-cues_to_front {(Enabled ? "1" : "0")}";
}
