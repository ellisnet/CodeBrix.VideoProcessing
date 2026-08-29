using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Enums;

namespace CodeBrix.VideoProcessing.Arguments;

/// <summary>
///     Represents FFmpeg's <c>tonemap</c> video filter, which maps one dynamic range onto another.
/// </summary>
/// <remarks>
///     <para>
///         Emits <c>tonemap=hable</c>, or <c>tonemap=hable:desat=0</c> when the optional numbers are
///         supplied. Every number is formatted with the invariant culture.
///     </para>
///     <para>
///         The filter works on linear light, so in practice it sits between two <c>zscale</c>
///         filters: one that converts to linear and one that converts back.
///     </para>
/// </remarks>
public class TonemapArgument : IVideoFilterArgument, IPreEscapedFilterArgument
{
    private readonly double? _desat;
    private readonly double? _param;
    private readonly double? _peak;

    /// <summary>
    ///     Applies a tone-mapping curve.
    /// </summary>
    /// <param name="algorithm">The tone-mapping curve.</param>
    /// <param name="param">
    ///     The curve's tunable parameter (<c>param</c>). Omitted when null, which leaves FFmpeg's
    ///     per-curve default in place.
    /// </param>
    /// <param name="desat">
    ///     Desaturation strength (<c>desat</c>), 0 to disable. Omitted when null (FFmpeg defaults to 2).
    /// </param>
    /// <param name="peak">
    ///     Signal peak override (<c>peak</c>). Omitted when null, which lets FFmpeg detect the peak.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">One of the numbers is not finite.</exception>
    public TonemapArgument(TonemapAlgorithm algorithm, double? param = null, double? desat = null, double? peak = null)
    {
        CheckFinite(param, nameof(param));
        CheckFinite(desat, nameof(desat));
        CheckFinite(peak, nameof(peak));

        Algorithm = algorithm;
        _param = param;
        _desat = desat;
        _peak = peak;
    }

    /// <summary>The tone-mapping curve this argument was created with.</summary>
    public TonemapAlgorithm Algorithm { get; }

    /// <summary>The filter name, <c>tonemap</c>.</summary>
    public string Key => "tonemap";

    /// <summary>The curve name followed by whichever optional numbers were supplied.</summary>
    public string Value
    {
        get
        {
            var parts = new List<string> { AlgorithmName() };

            if (_param.HasValue)
            {
                parts.Add($"param={_param.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            if (_desat.HasValue)
            {
                parts.Add($"desat={_desat.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            if (_peak.HasValue)
            {
                parts.Add($"peak={_peak.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            return string.Join(":", parts);
        }
    }

    private static void CheckFinite(double? value, string parameterName)
    {
        if (value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value)))
        {
            throw new ArgumentOutOfRangeException(parameterName, value.Value, "The value must be a finite number");
        }
    }

    private string AlgorithmName()
    {
        return Algorithm switch
        {
            TonemapAlgorithm.None => "none",
            TonemapAlgorithm.Clip => "clip",
            TonemapAlgorithm.Linear => "linear",
            TonemapAlgorithm.Gamma => "gamma",
            TonemapAlgorithm.Reinhard => "reinhard",
            TonemapAlgorithm.Hable => "hable",
            TonemapAlgorithm.Mobius => "mobius",
            _ => throw new ArgumentOutOfRangeException(nameof(Algorithm))
        };
    }
}
