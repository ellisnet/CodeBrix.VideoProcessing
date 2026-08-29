using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments;

/// <summary>
///     Represents the WebM / Matroska muxer's <c>-reserve_index_space</c> option, which leaves a
///     gap at the start of the file for the cue (seek index) so it does not have to be appended.
/// </summary>
/// <remarks>
///     This is the older way of getting a seek index at the front of a file; on a build that has
///     <c>-cues_to_front</c>, that option is easier because it needs no size estimate. If the
///     reserved space turns out to be too small, muxing fails.
/// </remarks>
public class ReserveIndexSpaceArgument : IArgument
{
    /// <summary>
    ///     Reserves space for the cue at the start of the file.
    /// </summary>
    /// <param name="bytes">The number of bytes to reserve; 0 reserves nothing.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bytes" /> is negative.</exception>
    public ReserveIndexSpaceArgument(int bytes)
    {
        if (bytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "The reserved index space cannot be negative");
        }

        Bytes = bytes;
    }

    /// <summary>The number of bytes reserved.</summary>
    public int Bytes { get; }

    /// <summary>The emitted text, for example <c>-reserve_index_space 65536</c>.</summary>
    public string Text => $"-reserve_index_space {Bytes.ToString(CultureInfo.InvariantCulture)}";
}
