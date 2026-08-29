using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments;

/// <summary>
///     Represents FFmpeg's <c>-autorotate</c> / <c>-noautorotate</c> switch, which decides whether a
///     container's rotation metadata is applied while decoding.
/// </summary>
/// <remarks>
///     This is an INPUT-side option: FFmpeg only honours it when it appears before the <c>-i</c> it
///     belongs to, which is where the input callback puts it.
/// </remarks>
public class AutoRotateArgument : IArgument
{
    /// <summary>
    ///     Turns automatic rotation on or off for the input it is attached to.
    /// </summary>
    /// <param name="enabled">
    ///     <see langword="true" /> to apply the rotation metadata (FFmpeg's default),
    ///     <see langword="false" /> to decode the stored orientation untouched.
    /// </param>
    public AutoRotateArgument(bool enabled)
    {
        Enabled = enabled;
    }

    /// <summary>Whether automatic rotation is enabled.</summary>
    public bool Enabled { get; }

    /// <summary>The emitted text, <c>-autorotate</c> or <c>-noautorotate</c>.</summary>
    public string Text => Enabled ? "-autorotate" : "-noautorotate";
}
