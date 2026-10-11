using System;
using System.Collections.Generic;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// A streaming LINEAR-INTERPOLATION sample-rate converter for interleaved float audio. Output frame n sits at input
/// position n * inputRate / outputRate, computed in exact integer arithmetic (no drift however long the stream), and
/// is the straight-line blend of the two input frames around it. Simple and deterministic; it does not low-pass
/// filter, so downsampling can alias - adequate for the engine's "deliver at the requested rate" job, not a
/// mastering-grade resampler.
/// </summary>
internal sealed class AudioResampler
{
    private readonly int inputRate;
    private readonly int outputRate;
    private readonly int channels;
    private readonly List<float> pending = new List<float>();

    private long pendingStart;      // absolute input frame index of pending[0]
    private long inputFrames;       // input frames received so far
    private long outputFrames;      // output frames produced so far

    /// <summary>Creates a converter.</summary>
    /// <param name="inputRate">The source sample rate.</param>
    /// <param name="outputRate">The wanted sample rate.</param>
    /// <param name="channels">The (unchanged) channel count.</param>
    internal AudioResampler(int inputRate, int outputRate, int channels)
    {
        if (inputRate <= 0) throw new ArgumentOutOfRangeException(nameof(inputRate));
        if (outputRate <= 0) throw new ArgumentOutOfRangeException(nameof(outputRate));
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
        this.inputRate = inputRate;
        this.outputRate = outputRate;
        this.channels = channels;
    }

    /// <summary>True when input and output rates are equal (the converter then only copies).</summary>
    internal bool IsPassThrough => inputRate == outputRate;

    /// <summary>Adds input frames and appends every output frame that can now be computed.</summary>
    /// <param name="input">Interleaved frames (a multiple of the channel count).</param>
    /// <param name="output">Receives interleaved output samples.</param>
    internal void Process(ReadOnlySpan<float> input, List<float> output)
    {
        if (input.Length % channels != 0) throw new ArgumentException("The input must hold whole frames.", nameof(input));
        if (IsPassThrough)
        {
            foreach (float sample in input) output.Add(sample);
            inputFrames += input.Length / channels;
            outputFrames = inputFrames;
            return;
        }

        foreach (float sample in input) pending.Add(sample);
        inputFrames += input.Length / channels;
        Produce(output, endOfStream: false);
    }

    /// <summary>Signals the end of the input and appends the remaining output frames.</summary>
    /// <param name="output">Receives interleaved output samples.</param>
    internal void Flush(List<float> output)
    {
        if (IsPassThrough) return;
        Produce(output, endOfStream: true);
        pending.Clear();
    }

    private void Produce(List<float> output, bool endOfStream)
    {
        while (true)
        {
            // Input position of output frame n: n * inputRate / outputRate = whole + fraction.
            long numerator = outputFrames * inputRate;
            long whole = numerator / outputRate;
            long remainder = numerator % outputRate;

            if (whole >= inputFrames) break;                      // past the end of what we have
            bool needNext = remainder != 0;
            if (needNext && whole + 1 >= inputFrames && !endOfStream) break;

            float fraction = (float)remainder / outputRate;
            int baseIndex = (int)(whole - pendingStart) * channels;
            long nextFrame = Math.Min(whole + 1, inputFrames - 1);
            int nextIndex = (int)(nextFrame - pendingStart) * channels;
            for (int channel = 0; channel < channels; channel++)
            {
                float a = pending[baseIndex + channel];
                float b = pending[nextIndex + channel];
                output.Add(a + (b - a) * fraction);
            }

            outputFrames++;
        }

        // Drop the input frames no future output frame needs (keep the one at the next position).
        long keepFrom = Math.Min(outputFrames * inputRate / outputRate, inputFrames);
        long drop = keepFrom - pendingStart;
        if (drop > 0)
        {
            pending.RemoveRange(0, (int)drop * channels);
            pendingStart = keepFrom;
        }
    }
}
