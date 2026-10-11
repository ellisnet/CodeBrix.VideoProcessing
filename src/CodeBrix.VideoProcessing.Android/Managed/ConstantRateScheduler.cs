using System;
using CodeBrix.VideoProcessing.Core.Media;

namespace CodeBrix.VideoProcessing.Android.Managed;

/// <summary>
/// Turns the decoder's pictures (each with its own presentation time) into a CONSTANT-RATE frame sequence - the
/// contract's rule that frame N is shown at <c>FrameRate.TimestampOf(N)</c> - the way ffmpeg's fps filter does for
/// the desktop engine. Each picture is placed in the grid slot NEAREST its presentation time; a slot with no picture
/// of its own repeats the previous picture, and when several pictures land in one slot the LATEST one wins. No slot
/// is produced after the last picture's own slot.
/// </summary>
/// <typeparam name="T">The picture token (the engine uses a buffer; tests use plain numbers).</typeparam>
internal sealed class ConstantRateScheduler<T>
{
    private readonly FrameRate rate;
    private readonly long originMicroseconds;
    private readonly Action<T> release;

    private bool hasHeld;
    private T held;
    private long heldSlot;
    private bool hasLookahead;
    private T lookahead;
    private long lookaheadSlot;
    private bool inputEnded;

    /// <summary>Creates a scheduler.</summary>
    /// <param name="rate">The constant output rate.</param>
    /// <param name="originMicroseconds">The presentation time of slot 0 (the stream's first picture).</param>
    /// <param name="startSlot">The first slot to deliver (from a start time or a seek).</param>
    /// <param name="release">Called when a picture token is no longer needed; may be null.</param>
    internal ConstantRateScheduler(FrameRate rate, long originMicroseconds, long startSlot, Action<T> release)
    {
        if (!rate.IsSpecified) throw new ArgumentException("The frame rate must be specified.", nameof(rate));
        this.rate = rate;
        this.originMicroseconds = originMicroseconds;
        this.release = release;
        NextSlot = startSlot;
    }

    /// <summary>The slot the next delivered frame will have.</summary>
    internal long NextSlot { get; private set; }

    /// <summary>True when <see cref="TryDeliver" /> needs another decoded picture (or the end of input) before it can answer.</summary>
    internal bool NeedsInput
    {
        get
        {
            if (inputEnded) return false;
            PromoteLookahead();
            return !hasLookahead;
        }
    }

    /// <summary>The grid slot nearest a presentation time (never negative).</summary>
    internal static long SlotOf(long presentationMicroseconds, long originMicroseconds, FrameRate rate)
    {
        long relative = presentationMicroseconds - originMicroseconds;
        if (relative <= 0) return 0;

        // round(relative * num / (den * 1e6)), half up.
        Int128 numerator = (Int128)relative * rate.Numerator * 2 + (Int128)rate.Denominator * 1_000_000;
        Int128 denominator = (Int128)rate.Denominator * 2_000_000;
        return (long)(numerator / denominator);
    }

    /// <summary>Gives the scheduler the next decoded picture, in presentation order.</summary>
    internal void Offer(T picture, long presentationMicroseconds)
    {
        long slot = SlotOf(presentationMicroseconds, originMicroseconds, rate);
        PromoteLookahead();

        if (slot <= NextSlot)
        {
            ReplaceHeld(picture, slot);
        }
        else if (hasLookahead)
        {
            // Only reachable if the caller offers without asking NeedsInput; keep the newest look-ahead.
            if (slot <= lookaheadSlot) { release?.Invoke(lookahead); lookahead = picture; lookaheadSlot = slot; }
            else
            {
                ReplaceHeld(lookahead, lookaheadSlot);
                lookahead = picture;
                lookaheadSlot = slot;
            }
        }
        else
        {
            hasLookahead = true;
            lookahead = picture;
            lookaheadSlot = slot;
        }
    }

    /// <summary>Tells the scheduler the decoder has no more pictures.</summary>
    internal void EndOfInput() => inputEnded = true;

    /// <summary>Produces the next constant-rate frame when it can.</summary>
    /// <param name="picture">The picture for the frame.</param>
    /// <param name="slot">The frame's index.</param>
    /// <returns>True with a frame; false either when <see cref="NeedsInput" /> or at the end.</returns>
    internal bool TryDeliver(out T picture, out long slot)
    {
        picture = default;
        slot = 0;
        PromoteLookahead();
        if (!hasLookahead && !inputEnded) return false;

        if (!hasHeld)
        {
            if (!hasLookahead) return false;

            // Nothing at or before the wanted slot (a stream that starts late): jump to the first picture.
            NextSlot = lookaheadSlot;
            PromoteLookahead();
        }

        if (!hasLookahead && inputEnded && heldSlot < NextSlot) return false;

        picture = held;
        slot = NextSlot;
        NextSlot++;
        return true;
    }

    /// <summary>Drops every picture (after a seek), releasing them, and restarts at a slot.</summary>
    internal void Reset(long startSlot)
    {
        if (hasHeld) release?.Invoke(held);
        if (hasLookahead) release?.Invoke(lookahead);
        hasHeld = false;
        hasLookahead = false;
        held = default;
        lookahead = default;
        inputEnded = false;
        NextSlot = startSlot;
    }

    private void PromoteLookahead()
    {
        if (hasLookahead && lookaheadSlot <= NextSlot)
        {
            hasLookahead = false;
            ReplaceHeld(lookahead, lookaheadSlot);
            lookahead = default;
        }
    }

    private void ReplaceHeld(T picture, long slot)
    {
        if (hasHeld && !ReferenceEquals(held, picture)) release?.Invoke(held);
        held = picture;
        heldSlot = slot;
        hasHeld = true;
    }
}
