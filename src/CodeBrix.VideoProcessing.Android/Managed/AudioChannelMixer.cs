using System;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// Changes the channel count of interleaved float audio. The rules, in order: the same count is copied; a MONO
/// source is copied to every output channel; a MONO output is the average of every source channel; otherwise the
/// first min(in, out) channels are copied in order - extra source channels are dropped and extra output channels are
/// silent.
/// </summary>
internal static class AudioChannelMixer
{
    /// <summary>Mixes <paramref name="frames" /> frames from <paramref name="inputChannels" /> to <paramref name="outputChannels" /> channels.</summary>
    /// <param name="input">At least frames * inputChannels samples.</param>
    /// <param name="inputChannels">The source channel count.</param>
    /// <param name="output">At least frames * outputChannels samples.</param>
    /// <param name="outputChannels">The wanted channel count.</param>
    /// <param name="frames">The number of frames (one sample per channel).</param>
    internal static void Mix(ReadOnlySpan<float> input, int inputChannels, Span<float> output, int outputChannels, int frames)
    {
        if (inputChannels <= 0) throw new ArgumentOutOfRangeException(nameof(inputChannels));
        if (outputChannels <= 0) throw new ArgumentOutOfRangeException(nameof(outputChannels));

        if (inputChannels == outputChannels)
        {
            input.Slice(0, frames * inputChannels).CopyTo(output);
            return;
        }

        for (int frame = 0; frame < frames; frame++)
        {
            ReadOnlySpan<float> source = input.Slice(frame * inputChannels, inputChannels);
            Span<float> destination = output.Slice(frame * outputChannels, outputChannels);

            if (inputChannels == 1)
            {
                destination.Fill(source[0]);
            }
            else if (outputChannels == 1)
            {
                float sum = 0;
                for (int channel = 0; channel < inputChannels; channel++) sum += source[channel];
                destination[0] = sum / inputChannels;
            }
            else
            {
                int shared = Math.Min(inputChannels, outputChannels);
                source.Slice(0, shared).CopyTo(destination);
                if (outputChannels > shared) destination.Slice(shared).Clear();
            }
        }
    }
}
