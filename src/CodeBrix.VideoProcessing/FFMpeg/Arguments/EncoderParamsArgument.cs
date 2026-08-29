using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments;

/// <summary>
///     Represents one of the encoder-specific "parameters" options - <c>-svtav1-params</c>,
///     <c>-x264-params</c>, <c>-aom-params</c> and the like - whose value is a colon-joined list of
///     <c>key=value</c> pairs.
/// </summary>
/// <remarks>
///     Emits <c>-svtav1-params tune=0:film-grain=8</c>. The option name may be written with or
///     without its leading dash. If the joined value contains whitespace it is wrapped in double
///     quotes so it survives the command line.
/// </remarks>
public class EncoderParamsArgument : IArgument
{
    private readonly string _optionName;
    private readonly string _parameters;

    /// <summary>
    ///     Sets an encoder's parameter list.
    /// </summary>
    /// <param name="optionName">
    ///     The FFmpeg option name, for example <c>svtav1-params</c> or <c>-svtav1-params</c>; a
    ///     leading dash is optional and is normalised away.
    /// </param>
    /// <param name="parameters">
    ///     The parameters as key/value pairs; they are joined with <c>:</c> in the order given.
    ///     A null value is treated as an empty string.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     <paramref name="optionName" /> is null or empty, <paramref name="parameters" /> is null or
    ///     empty, or one of the keys is null or empty.
    /// </exception>
    public EncoderParamsArgument(string optionName, params (string Key, string Value)[] parameters)
    {
        if (string.IsNullOrWhiteSpace(optionName))
        {
            throw new ArgumentException("An option name is required", nameof(optionName));
        }

        if (parameters == null || parameters.Length == 0)
        {
            throw new ArgumentException("At least one parameter is required", nameof(parameters));
        }

        foreach (var parameter in parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Key))
            {
                throw new ArgumentException("Every encoder parameter needs a key", nameof(parameters));
            }
        }

        _optionName = optionName.TrimStart('-');
        _parameters = string.Join(":", parameters.Select(parameter => $"{parameter.Key}={parameter.Value ?? string.Empty}"));
    }

    /// <summary>The option name, without its leading dash.</summary>
    public string OptionName => _optionName;

    /// <summary>The colon-joined parameter list.</summary>
    public string Parameters => _parameters;

    /// <summary>The emitted text, for example <c>-svtav1-params tune=0:film-grain=8</c>.</summary>
    public string Text => _parameters.Any(char.IsWhiteSpace)
        ? $"-{_optionName} \"{_parameters}\""
        : $"-{_optionName} {_parameters}";
}
