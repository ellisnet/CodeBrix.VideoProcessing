using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Arguments;

/// <summary>
///     Marks a filter argument whose <c>Value</c> is already escaped for FFmpeg's filtergraph
///     parser, so that the <c>-vf</c> / <c>-af</c> chain builder emits it verbatim.
/// </summary>
/// <remarks>
///     <para>
///         Filter arguments that do NOT carry this marker get the historical treatment: the chain
///         builder replaces every <c>,</c> in the value with <c>\,</c> and emits the rest as-is.
///         That is the right thing for a value the argument fully controls (<c>scale=200:300</c>,
///         <c>transpose=2</c>) and it is kept unchanged for every argument shipped before this
///         marker existed.
///     </para>
///     <para>
///         Arguments that own a free-text value - a file path, a caption, a user-supplied option
///         string - escape it themselves with the full filtergraph rules and carry this marker so
///         the chain builder does not escape the commas a second time.
///     </para>
/// </remarks>
public interface IPreEscapedFilterArgument
{
}
