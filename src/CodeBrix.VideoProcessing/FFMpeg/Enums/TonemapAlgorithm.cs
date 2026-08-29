using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Enums;

/// <summary>
///     Tone-mapping curve used by FFmpeg's <c>tonemap</c> filter, emitted as its first (unnamed) option.
/// </summary>
public enum TonemapAlgorithm
{
    /// <summary>Do not tone map; only convert the dynamic range (<c>tonemap=none</c>).</summary>
    None,

    /// <summary>Hard-clip out-of-range values (<c>tonemap=clip</c>).</summary>
    Clip,

    /// <summary>Linear curve scaled by the tone-map parameter (<c>tonemap=linear</c>).</summary>
    Linear,

    /// <summary>Gamma curve (<c>tonemap=gamma</c>).</summary>
    Gamma,

    /// <summary>Reinhard curve (<c>tonemap=reinhard</c>).</summary>
    Reinhard,

    /// <summary>Hable / Uncharted 2 filmic curve (<c>tonemap=hable</c>).</summary>
    Hable,

    /// <summary>Mobius curve (<c>tonemap=mobius</c>).</summary>
    Mobius
}
