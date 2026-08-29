using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Arguments;
using CodeBrix.VideoProcessing.Enums;
using CodeBrix.VideoProcessing.Exceptions;

namespace CodeBrix.VideoProcessing; //was previously: FFMpegCore;

public class FFMpegArgumentOptions : FFMpegArgumentsBase
{
    internal FFMpegArgumentOptions() { }

    public FFMpegArgumentOptions WithAudioCodec(Codec audioCodec)
    {
        return WithArgument(new AudioCodecArgument(audioCodec));
    }

    public FFMpegArgumentOptions WithAudioCodec(string audioCodec)
    {
        return WithArgument(new AudioCodecArgument(audioCodec));
    }

    public FFMpegArgumentOptions WithAudioBitrate(AudioQuality audioQuality)
    {
        return WithArgument(new AudioBitrateArgument(audioQuality));
    }

    public FFMpegArgumentOptions WithAudioBitrate(int bitrate)
    {
        return WithArgument(new AudioBitrateArgument(bitrate));
    }

    public FFMpegArgumentOptions WithAudioSamplingRate(int samplingRate = 48000)
    {
        return WithArgument(new AudioSamplingRateArgument(samplingRate));
    }

    public FFMpegArgumentOptions WithVariableBitrate(int vbr)
    {
        return WithArgument(new VariableBitRateArgument(vbr));
    }

    public FFMpegArgumentOptions Resize(int width, int height)
    {
        return WithArgument(new SizeArgument(width, height));
    }

    public FFMpegArgumentOptions Resize(Size? size)
    {
        return WithArgument(new SizeArgument(size));
    }

    public FFMpegArgumentOptions Crop(Size? size, int left, int top)
    {
        return WithArgument(new CropArgument(size, top, left));
    }

    public FFMpegArgumentOptions Crop(int width, int height, int left, int top)
    {
        return WithArgument(new CropArgument(new Size(width, height), top, left));
    }

    public FFMpegArgumentOptions Crop(Size? size)
    {
        return WithArgument(new CropArgument(size, 0, 0));
    }

    public FFMpegArgumentOptions Crop(int width, int height)
    {
        return WithArgument(new CropArgument(new Size(width, height), 0, 0));
    }

    public FFMpegArgumentOptions WithBitStreamFilter(Channel channel, Filter filter)
    {
        return WithArgument(new BitStreamFilterArgument(channel, filter));
    }

    public FFMpegArgumentOptions WithConstantRateFactor(int crf)
    {
        return WithArgument(new ConstantRateFactorArgument(crf));
    }

    public FFMpegArgumentOptions CopyChannel(Channel channel = Channel.Both)
    {
        return WithArgument(new CopyArgument(channel));
    }

    public FFMpegArgumentOptions DisableChannel(Channel channel)
    {
        return WithArgument(new DisableChannelArgument(channel));
    }

    public FFMpegArgumentOptions WithDuration(TimeSpan? duration)
    {
        return WithArgument(new DurationArgument(duration));
    }

    public FFMpegArgumentOptions WithFastStart()
    {
        return WithArgument(new FaststartArgument());
    }

    public FFMpegArgumentOptions WithFrameOutputCount(int frames)
    {
        return WithArgument(new FrameOutputCountArgument(frames));
    }

    public FFMpegArgumentOptions WithHardwareAcceleration(HardwareAccelerationDevice hardwareAccelerationDevice = HardwareAccelerationDevice.Auto)
    {
        return WithArgument(new HardwareAccelerationArgument(hardwareAccelerationDevice));
    }

    public FFMpegArgumentOptions UsingShortest(bool shortest = true)
    {
        return WithArgument(new ShortestArgument(shortest));
    }

    public FFMpegArgumentOptions UsingMultithreading(bool multithread)
    {
        return WithArgument(new ThreadsArgument(multithread));
    }

    public FFMpegArgumentOptions UsingThreads(int threads)
    {
        return WithArgument(new ThreadsArgument(threads));
    }

    public FFMpegArgumentOptions WithVideoCodec(Codec videoCodec)
    {
        return WithArgument(new VideoCodecArgument(videoCodec));
    }

    public FFMpegArgumentOptions WithVideoCodec(string videoCodec)
    {
        return WithArgument(new VideoCodecArgument(videoCodec));
    }

    public FFMpegArgumentOptions WithVideoBitrate(int bitrate)
    {
        return WithArgument(new VideoBitrateArgument(bitrate));
    }

    /// <summary>
    ///     Builds the output's <c>-vf</c> video-filter chain.
    /// </summary>
    /// <param name="videoFilterOptions">Callback that adds every filter of the chain, in order.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    /// <exception cref="FFMpegArgumentException">
    ///     A video-filter chain has already been built on this output. FFmpeg accepts one <c>-vf</c>
    ///     per output and silently keeps only the last, so the whole chain has to be built in a
    ///     single call.
    /// </exception>
    public FFMpegArgumentOptions WithVideoFilters(Action<VideoFilterOptions> videoFilterOptions)
    {
        if (Arguments.Any(argument => argument is VideoFiltersArgument))
        {
            throw new FFMpegArgumentException(
                "A -vf video-filter chain has already been added to this output. ffmpeg accepts only one -vf "
                + "per output and keeps only the last one it is given, so every filter must be added in a "
                + "single WithVideoFilters call.");
        }

        var videoFilterOptionsObj = new VideoFilterOptions();
        videoFilterOptions(videoFilterOptionsObj);
        return WithArgument(new VideoFiltersArgument(videoFilterOptionsObj));
    }

    /// <summary>
    ///     Builds the output's <c>-af</c> audio-filter chain.
    /// </summary>
    /// <param name="audioFilterOptions">Callback that adds every filter of the chain, in order.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    /// <exception cref="FFMpegArgumentException">
    ///     An audio-filter chain has already been built on this output. FFmpeg accepts one <c>-af</c>
    ///     per output and silently keeps only the last, so the whole chain has to be built in a
    ///     single call.
    /// </exception>
    public FFMpegArgumentOptions WithAudioFilters(Action<AudioFilterOptions> audioFilterOptions)
    {
        if (Arguments.Any(argument => argument is AudioFiltersArgument))
        {
            throw new FFMpegArgumentException(
                "An -af audio-filter chain has already been added to this output. ffmpeg accepts only one -af "
                + "per output and keeps only the last one it is given, so every filter must be added in a "
                + "single WithAudioFilters call.");
        }

        var audioFilterOptionsObj = new AudioFilterOptions();
        audioFilterOptions(audioFilterOptionsObj);
        return WithArgument(new AudioFiltersArgument(audioFilterOptionsObj));
    }

    public FFMpegArgumentOptions WithFramerate(double framerate)
    {
        return WithArgument(new FrameRateArgument(framerate));
    }

    public FFMpegArgumentOptions WithoutMetadata()
    {
        return WithArgument(new RemoveMetadataArgument());
    }

    public FFMpegArgumentOptions WithSpeedPreset(Speed speed)
    {
        return WithArgument(new SpeedPresetArgument(speed));
    }

    public FFMpegArgumentOptions WithStartNumber(int startNumber)
    {
        return WithArgument(new StartNumberArgument(startNumber));
    }

    public FFMpegArgumentOptions WithCustomArgument(string argument)
    {
        return WithArgument(new CustomArgument(argument));
    }

    public FFMpegArgumentOptions Seek(TimeSpan? seekTo)
    {
        return WithArgument(new SeekArgument(seekTo));
    }

    public FFMpegArgumentOptions EndSeek(TimeSpan? seekTo)
    {
        return WithArgument(new EndSeekArgument(seekTo));
    }

    public FFMpegArgumentOptions Loop(int times)
    {
        return WithArgument(new LoopArgument(times));
    }

    public FFMpegArgumentOptions OverwriteExisting()
    {
        return WithArgument(new OverwriteArgument());
    }

    public FFMpegArgumentOptions SelectStream(int streamIndex, int inputFileIndex = 0,
        Channel channel = Channel.All)
    {
        return WithArgument(new MapStreamArgument(streamIndex, inputFileIndex, channel));
    }

    public FFMpegArgumentOptions SelectStreams(IEnumerable<int> streamIndices, int inputFileIndex = 0,
        Channel channel = Channel.All)
    {
        return streamIndices.Aggregate(this,
            (options, streamIndex) => options.SelectStream(streamIndex, inputFileIndex, channel));
    }

    public FFMpegArgumentOptions DeselectStream(int streamIndex, int inputFileIndex = 0,
        Channel channel = Channel.All)
    {
        return WithArgument(new MapStreamArgument(streamIndex, inputFileIndex, channel, true));
    }

    public FFMpegArgumentOptions DeselectStreams(IEnumerable<int> streamIndices, int inputFileIndex = 0,
        Channel channel = Channel.All)
    {
        return streamIndices.Aggregate(this,
            (options, streamIndex) => options.DeselectStream(streamIndex, inputFileIndex, channel));
    }

    public FFMpegArgumentOptions ForceFormat(ContainerFormat format)
    {
        return WithArgument(new ForceFormatArgument(format));
    }

    public FFMpegArgumentOptions ForceFormat(string format)
    {
        return WithArgument(new ForceFormatArgument(format));
    }

    public FFMpegArgumentOptions ForcePixelFormat(string pixelFormat)
    {
        return WithArgument(new ForcePixelFormat(pixelFormat));
    }

    public FFMpegArgumentOptions ForcePixelFormat(PixelFormat pixelFormat)
    {
        return WithArgument(new ForcePixelFormat(pixelFormat));
    }

    public FFMpegArgumentOptions WithAudibleEncryptionKeys(string key, string iv)
    {
        return WithArgument(new AudibleEncryptionKeyArgument(key, iv));
    }

    public FFMpegArgumentOptions WithAudibleActivationBytes(string activationBytes)
    {
        return WithArgument(new AudibleEncryptionKeyArgument(activationBytes));
    }

    public FFMpegArgumentOptions WithTagVersion(int id3v2Version = 3)
    {
        return WithArgument(new ID3V2VersionArgument(id3v2Version));
    }

    public FFMpegArgumentOptions WithGifPaletteArgument(int streamIndex, Size? size, double fps = 12)
    {
        return WithArgument(new GifPaletteArgument(streamIndex, fps, size));
    }

    public FFMpegArgumentOptions WithCopyCodec()
    {
        return WithArgument(new CopyCodecArgument());
    }

    /// <summary>
    ///     Sets a NUMERIC encoder preset, <c>-preset N</c>.
    /// </summary>
    /// <param name="preset">
    ///     The preset number, whose meaning and range belong to the chosen encoder: <c>libsvtav1</c>
    ///     takes 0-13 and <c>librav1e</c> 0-10, both "higher is faster".
    /// </param>
    /// <returns>The same options object, so calls can be chained.</returns>
    /// <remarks>The word-based x264/x265 presets keep their own overload.</remarks>
    public FFMpegArgumentOptions WithSpeedPreset(int preset)
    {
        return WithArgument(new NumericSpeedPresetArgument(preset));
    }

    /// <summary>
    ///     Sets an encoder-specific parameter list such as <c>-svtav1-params tune=0:film-grain=8</c>.
    /// </summary>
    /// <param name="optionName">
    ///     The FFmpeg option name, with or without its leading dash - for example
    ///     <c>svtav1-params</c>, <c>x264-params</c> or <c>aom-params</c>.
    /// </param>
    /// <param name="parameters">
    ///     The parameters as key/value pairs; they are joined with <c>:</c> in the order given.
    /// </param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public FFMpegArgumentOptions WithEncoderParams(string optionName, params (string Key, string Value)[] parameters)
    {
        return WithArgument(new EncoderParamsArgument(optionName, parameters));
    }

    /// <summary>
    ///     Turns FFmpeg's automatic rotation on or off, <c>-autorotate</c> / <c>-noautorotate</c>.
    /// </summary>
    /// <param name="enabled">
    ///     <see langword="true" /> to apply the container's rotation metadata while decoding
    ///     (FFmpeg's default), <see langword="false" /> to leave the stored orientation alone.
    /// </param>
    /// <returns>The same options object, so calls can be chained.</returns>
    /// <remarks>
    ///     This is an INPUT-side option: put it in the <c>addArguments</c> callback of
    ///     <c>FromFileInput</c> / <c>AddFileInput</c>, which emits it before the matching <c>-i</c>.
    ///     On an output it has no effect.
    /// </remarks>
    public FFMpegArgumentOptions AutoRotate(bool enabled = true)
    {
        return WithArgument(new AutoRotateArgument(enabled));
    }

    /// <summary>
    ///     Sets the subtitle codec, <c>-c:s</c>.
    /// </summary>
    /// <param name="subtitleCodec">The codec name, for example <c>webvtt</c>, <c>mov_text</c> or <c>copy</c>.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public FFMpegArgumentOptions WithSubtitleCodec(string subtitleCodec)
    {
        return WithArgument(new SubtitleCodecArgument(subtitleCodec));
    }

    /// <summary>
    ///     Sets the subtitle codec, <c>-c:s</c>, from a codec reported by the installed FFmpeg.
    /// </summary>
    /// <param name="subtitleCodec">The codec; it must be a subtitle codec.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public FFMpegArgumentOptions WithSubtitleCodec(Codec subtitleCodec)
    {
        return WithArgument(new SubtitleCodecArgument(subtitleCodec));
    }

    /// <summary>
    ///     Sets a metadata tag on the output as a whole, <c>-metadata key="value"</c>.
    /// </summary>
    /// <param name="key">The tag name, for example <c>title</c>.</param>
    /// <param name="value">The tag value; it is quoted, and any double quote in it is escaped.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public FFMpegArgumentOptions WithMetadata(string key, string value)
    {
        return WithArgument(new SetMetadataArgument(key, value));
    }

    /// <summary>
    ///     Sets a metadata tag on one output stream, <c>-metadata:s:N key="value"</c>.
    /// </summary>
    /// <param name="streamIndex">The zero-based output stream index, counted across every stream.</param>
    /// <param name="key">The tag name, for example <c>language</c>.</param>
    /// <param name="value">The tag value; it is quoted, and any double quote in it is escaped.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public FFMpegArgumentOptions WithStreamMetadata(int streamIndex, string key, string value)
    {
        return WithArgument(new SetMetadataArgument(streamIndex, key, value));
    }

    /// <summary>
    ///     Sets a metadata tag on one output stream of a given type,
    ///     <c>-metadata:s:&lt;type&gt;:N key="value"</c>.
    /// </summary>
    /// <param name="channel">
    ///     The stream type the index counts within - <c>Channel.Subtitle</c> gives
    ///     <c>-metadata:s:s:N</c>. <c>Channel.All</c> and <c>Channel.Both</c> leave the type out.
    /// </param>
    /// <param name="streamIndex">The zero-based stream index within that type.</param>
    /// <param name="key">The tag name, for example <c>language</c>.</param>
    /// <param name="value">The tag value; it is quoted, and any double quote in it is escaped.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public FFMpegArgumentOptions WithStreamMetadata(Channel channel, int streamIndex, string key, string value)
    {
        return WithArgument(new SetMetadataArgument(channel, streamIndex, key, value));
    }

    /// <summary>
    ///     Sets the disposition flags of one output stream, <c>-disposition:N flags</c>.
    /// </summary>
    /// <param name="streamIndex">The zero-based output stream index, counted across every stream.</param>
    /// <param name="flags">
    ///     The flags to set, joined with <c>+</c>. Setting a disposition REPLACES whatever the stream
    ///     had, so list every flag that should stay; no flags at all clears them.
    /// </param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public FFMpegArgumentOptions WithDisposition(int streamIndex, params Disposition[] flags)
    {
        return WithArgument(new DispositionArgument(streamIndex, flags));
    }

    /// <summary>
    ///     Sets the disposition flags of one output stream of a given type,
    ///     <c>-disposition:&lt;type&gt;:N flags</c>.
    /// </summary>
    /// <param name="channel">
    ///     The stream type the index counts within - <c>Channel.Subtitle</c> gives
    ///     <c>-disposition:s:N</c>. <c>Channel.All</c> and <c>Channel.Both</c> leave the type out.
    /// </param>
    /// <param name="streamIndex">The zero-based stream index within that type.</param>
    /// <param name="flags">
    ///     The flags to set, joined with <c>+</c>. Setting a disposition REPLACES whatever the stream
    ///     had, so list every flag that should stay; no flags at all clears them.
    /// </param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public FFMpegArgumentOptions WithDisposition(Channel channel, int streamIndex, params Disposition[] flags)
    {
        return WithArgument(new DispositionArgument(channel, streamIndex, flags));
    }

    /// <summary>
    ///     Sets the WebM / Matroska muxer's <c>-cues_to_front</c> option, which rewrites the finished
    ///     file so the seek index sits before the media data.
    /// </summary>
    /// <param name="enabled"><see langword="true" /> to move the cues to the front of the file.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    public FFMpegArgumentOptions CuesToFront(bool enabled = true)
    {
        return WithArgument(new CuesToFrontArgument(enabled));
    }

    /// <summary>
    ///     Sets the WebM / Matroska muxer's <c>-reserve_index_space</c> option, which leaves a gap at
    ///     the start of the file for the seek index.
    /// </summary>
    /// <param name="bytes">The number of bytes to reserve; muxing fails if the index does not fit.</param>
    /// <returns>The same options object, so calls can be chained.</returns>
    /// <remarks>On a build that has <c>-cues_to_front</c>, that option needs no size estimate.</remarks>
    public FFMpegArgumentOptions ReserveIndexSpace(int bytes)
    {
        return WithArgument(new ReserveIndexSpaceArgument(bytes));
    }

    public FFMpegArgumentOptions WithArgument(IArgument argument)
    {
        Arguments.Add(argument);
        return this;
    }
}
