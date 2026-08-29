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
///     Represents a numeric <c>-preset</c> value.
/// </summary>
/// <remarks>
///     The AV1 encoders take a number rather than one of x264's words: <c>libsvtav1</c> uses 0-13
///     (higher is faster), <c>libaom-av1</c> uses <c>-cpu-used</c>-style numbers through
///     <c>-preset</c>, and <c>librav1e</c> uses 0-10. The word-based presets stay on
///     <c>SpeedPresetArgument</c>.
/// </remarks>
public class NumericSpeedPresetArgument : IArgument
{
    /// <summary>
    ///     Sets a numeric encoder preset.
    /// </summary>
    /// <param name="preset">The preset number. Its meaning and range belong to the chosen encoder.</param>
    public NumericSpeedPresetArgument(int preset)
    {
        Preset = preset;
    }

    /// <summary>The preset number this argument emits.</summary>
    public int Preset { get; }

    /// <summary>The emitted text, for example <c>-preset 8</c>.</summary>
    public string Text => $"-preset {Preset.ToString(CultureInfo.InvariantCulture)}";
}
