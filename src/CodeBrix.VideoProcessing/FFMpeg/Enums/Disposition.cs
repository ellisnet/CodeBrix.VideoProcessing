using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrix.VideoProcessing.Enums;

/// <summary>
///     A stream disposition flag, as accepted by FFmpeg's <c>-disposition</c> output option.
/// </summary>
/// <remarks>
///     Several flags can be combined; FFmpeg joins them with <c>+</c>, for example
///     <c>-disposition:s:0 default+forced</c>. <see cref="None" /> clears every flag and is
///     emitted as <c>0</c>; it cannot be combined with the others.
/// </remarks>
public enum Disposition
{
    /// <summary>Clears every disposition flag on the stream; emitted as <c>0</c>.</summary>
    None,

    /// <summary>The default stream of its type (<c>default</c>).</summary>
    Default,

    /// <summary>A dubbed translation of the original (<c>dub</c>).</summary>
    Dub,

    /// <summary>The original language track (<c>original</c>).</summary>
    Original,

    /// <summary>A commentary track (<c>comment</c>).</summary>
    Comment,

    /// <summary>A lyrics track (<c>lyrics</c>).</summary>
    Lyrics,

    /// <summary>A karaoke track (<c>karaoke</c>).</summary>
    Karaoke,

    /// <summary>The stream must be shown even when subtitles are switched off (<c>forced</c>).</summary>
    Forced,

    /// <summary>Intended for hearing-impaired viewers (<c>hearing_impaired</c>).</summary>
    HearingImpaired,

    /// <summary>Intended for visually-impaired viewers (<c>visual_impaired</c>).</summary>
    VisualImpaired,

    /// <summary>Audio without background sounds (<c>clean_effects</c>).</summary>
    CleanEffects,

    /// <summary>An attached picture such as cover art (<c>attached_pic</c>).</summary>
    AttachedPic,

    /// <summary>Closed captions (<c>captions</c>).</summary>
    Captions,

    /// <summary>Textual descriptions of the content (<c>descriptions</c>).</summary>
    Descriptions,

    /// <summary>A stream that depends on another one (<c>dependent</c>).</summary>
    Dependent,

    /// <summary>A metadata-only stream (<c>metadata</c>).</summary>
    Metadata
}

internal static class DispositionMethods
{
    /// <summary>
    ///     Returns the flag name FFmpeg expects on the <c>-disposition</c> command line.
    /// </summary>
    /// <returns>The snake_case FFmpeg flag name, or <c>0</c> for <see cref="Disposition.None" />.</returns>
    public static string FlagName(this Disposition disposition)
    {
        return disposition switch
        {
            Disposition.None => "0",
            Disposition.Default => "default",
            Disposition.Dub => "dub",
            Disposition.Original => "original",
            Disposition.Comment => "comment",
            Disposition.Lyrics => "lyrics",
            Disposition.Karaoke => "karaoke",
            Disposition.Forced => "forced",
            Disposition.HearingImpaired => "hearing_impaired",
            Disposition.VisualImpaired => "visual_impaired",
            Disposition.CleanEffects => "clean_effects",
            Disposition.AttachedPic => "attached_pic",
            Disposition.Captions => "captions",
            Disposition.Descriptions => "descriptions",
            Disposition.Dependent => "dependent",
            Disposition.Metadata => "metadata",
            _ => throw new ArgumentOutOfRangeException(nameof(disposition))
        };
    }
}
