namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// The Android MediaFormat key strings and integer constants this engine reads and writes. They are fixed values of
/// the platform API (android.media.MediaFormat / MediaCodecInfo / AudioFormat), kept here as plain literals so the
/// platform-independent mapping code can use them on any host.
/// </summary>
internal static class MediaFormatKeys
{
    internal const string Mime = "mime";
    internal const string Width = "width";
    internal const string Height = "height";
    internal const string Duration = "durationUs";
    internal const string FrameRate = "frame-rate";
    internal const string Rotation = "rotation-degrees";
    internal const string ColorRange = "color-range";
    internal const string ColorStandard = "color-standard";
    internal const string ColorTransfer = "color-transfer";
    internal const string ColorFormat = "color-format";
    internal const string Profile = "profile";
    internal const string SampleRate = "sample-rate";
    internal const string ChannelCount = "channel-count";
    internal const string PcmEncoding = "pcm-encoding";
    internal const string Language = "language";
    internal const string BitRate = "bitrate";
    internal const string BitrateMode = "bitrate-mode";
    internal const string IFrameInterval = "i-frame-interval";
    internal const string AacProfile = "aac-profile";
    internal const string MaxInputSize = "max-input-size";
    internal const string Stride = "stride";
    internal const string SliceHeight = "slice-height";
    internal const string CropLeft = "crop-left";
    internal const string CropTop = "crop-top";
    internal const string CropRight = "crop-right";
    internal const string CropBottom = "crop-bottom";
    internal const string Csd0 = "csd-0";
    internal const string Csd1 = "csd-1";

    // MediaFormat.COLOR_RANGE_*
    internal const int ColorRangeFull = 1;
    internal const int ColorRangeLimited = 2;

    // MediaFormat.COLOR_STANDARD_*
    internal const int ColorStandardBt709 = 1;
    internal const int ColorStandardBt601Pal = 2;
    internal const int ColorStandardBt601Ntsc = 4;
    internal const int ColorStandardBt2020 = 6;

    // MediaFormat.COLOR_TRANSFER_*
    internal const int ColorTransferLinear = 1;
    internal const int ColorTransferSdrVideo = 3;
    internal const int ColorTransferSt2084 = 6;
    internal const int ColorTransferHlg = 7;

    // MediaCodecInfo.CodecCapabilities.COLOR_Format*
    internal const int ColorFormatYuv420Planar = 19;
    internal const int ColorFormatYuv420SemiPlanar = 21;
    internal const int ColorFormatYuv420Flexible = 0x7F420888;

    // MediaCodecInfo.EncoderCapabilities.BITRATE_MODE_*
    internal const int BitrateModeVbr = 1;

    // MediaCodecInfo.CodecProfileLevel.AACObjectLC
    internal const int AacObjectLc = 2;

    // AudioFormat.ENCODING_PCM_*
    internal const int PcmEncoding16Bit = 2;
    internal const int PcmEncoding8Bit = 3;
    internal const int PcmEncodingFloat = 4;
    internal const int PcmEncoding24BitPacked = 21;
    internal const int PcmEncoding32Bit = 22;

    // MIME types
    internal const string MimeAvc = "video/avc";
    internal const string MimeHevc = "video/hevc";
    internal const string MimeAac = "audio/mp4a-latm";
}
