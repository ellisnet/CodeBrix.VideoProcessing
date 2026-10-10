namespace CodeBrix.VideoProcessing.Core.Media;

/// <summary>The colour primaries (the gamut) the samples are expressed in.</summary>
public enum VideoColorPrimaries
{
    /// <summary>Not known or not tagged.</summary>
    Unspecified = 0,

    /// <summary>ITU-R BT.709 / sRGB primaries.</summary>
    Bt709 = 1,

    /// <summary>BT.601 625-line (PAL) primaries - ffmpeg "bt470bg".</summary>
    Bt601Pal = 2,

    /// <summary>BT.601 525-line (NTSC) primaries - ffmpeg "smpte170m".</summary>
    Bt601Ntsc = 3,

    /// <summary>ITU-R BT.2020 primaries.</summary>
    Bt2020 = 4,

    /// <summary>DCI-P3 primaries with the DCI white point (ffmpeg "smpte431").</summary>
    DciP3 = 5,

    /// <summary>Display P3 primaries with the D65 white point (ffmpeg "smpte432").</summary>
    DisplayP3 = 6,

    /// <summary>Primaries this contract does not name.</summary>
    Other = 7
}
