using System;
using System.Collections.Generic;
using CodeBrix.VideoProcessing.Android.Managed;
using CodeBrix.VideoProcessing.Core.Media;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Android.Tests;

public class ConstantRateSchedulerTests
{
    private static readonly FrameRate Thirty = new FrameRate(30, 1);

    // Drives the scheduler the way the frame source does: offer pictures only when it asks for input.
    private static List<(int Picture, long Slot)> Run(ConstantRateScheduler<int> scheduler, IReadOnlyList<long> times)
    {
        var delivered = new List<(int, long)>();
        int next = 0;
        while (true)
        {
            if (scheduler.TryDeliver(out int picture, out long slot))
            {
                delivered.Add((picture, slot));
                continue;
            }

            if (!scheduler.NeedsInput) return delivered;
            if (next < times.Count) scheduler.Offer(next, times[next++]);
            else scheduler.EndOfInput();
        }
    }

    private static long[] Grid(int count, long origin = 0) // the microsecond times a 30 fps decoder reports
    {
        long[] times = new long[count];
        for (int i = 0; i < count; i++) times[i] = origin + i * 1_000_000L / 30;
        return times;
    }

    [Fact]
    public void a_constant_rate_stream_maps_one_to_one()
    {
        //Arrange
        var scheduler = new ConstantRateScheduler<int>(Thirty, 0, 0, null);

        //Act
        List<(int Picture, long Slot)> frames = Run(scheduler, Grid(240));

        //Assert
        frames.Count.Should().Be(240);
        for (int i = 0; i < frames.Count; i++) frames[i].Should().Be((i, (long)i));
    }

    [Fact]
    public void a_missing_picture_repeats_the_previous_one()
    {
        //Arrange
        var times = new List<long>(Grid(6));
        times.RemoveAt(3);
        var scheduler = new ConstantRateScheduler<int>(Thirty, 0, 0, null);

        //Act
        List<(int Picture, long Slot)> frames = Run(scheduler, times);

        //Assert
        frames.Count.Should().Be(6);
        frames[3].Should().Be((2, 3L));
        frames[4].Should().Be((3, 4L));
    }

    [Fact]
    public void pictures_in_one_slot_keep_the_latest()
    {
        //Arrange
        long[] times = { 0, 33_333, 40_000, 66_666 };
        var released = new List<int>();
        var scheduler = new ConstantRateScheduler<int>(Thirty, 0, 0, released.Add);

        //Act
        List<(int Picture, long Slot)> frames = Run(scheduler, times);

        //Assert
        frames.Should().Equal((0, 0L), (2, 1L), (3, 2L));
        released.Should().Contain(1);
    }

    [Fact]
    public void the_origin_is_subtracted_and_a_start_slot_skips_earlier_pictures()
    {
        //Arrange
        var scheduler = new ConstantRateScheduler<int>(Thirty, 1_000_000, 10, null);

        //Act
        List<(int Picture, long Slot)> frames = Run(scheduler, Grid(20, origin: 1_000_000));

        //Assert
        frames.Count.Should().Be(10);
        frames[0].Should().Be((10, 10L));
        frames[^1].Should().Be((19, 19L));
    }

    [Fact]
    public void a_stream_that_starts_late_jumps_to_its_first_picture()
    {
        //Arrange
        var scheduler = new ConstantRateScheduler<int>(Thirty, 0, 0, null);

        //Act
        List<(int Picture, long Slot)> frames = Run(scheduler, new long[] { 100_000, 133_333 });

        //Assert
        frames.Should().Equal((0, 3L), (1, 4L));
    }

    [Fact]
    public void no_slot_is_produced_after_the_last_picture()
    {
        //Arrange
        var scheduler = new ConstantRateScheduler<int>(Thirty, 0, 0, null);

        //Act
        List<(int Picture, long Slot)> frames = Run(scheduler, Grid(3));

        //Assert
        frames.Count.Should().Be(3);
        scheduler.TryDeliver(out _, out _).Should().BeFalse();
        scheduler.NeedsInput.Should().BeFalse();
    }

    [Fact]
    public void reset_releases_the_held_pictures_and_restarts()
    {
        //Arrange
        var released = new List<int>();
        var scheduler = new ConstantRateScheduler<int>(Thirty, 0, 0, released.Add);
        scheduler.Offer(7, 0);
        scheduler.Offer(8, 100_000);

        //Act
        scheduler.Reset(5);

        //Assert
        released.Should().Equal(7, 8);
        scheduler.NextSlot.Should().Be(5);
        scheduler.NeedsInput.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(16_666, 0)]
    [InlineData(16_667, 1)]
    [InlineData(33_333, 1)]
    [InlineData(-5_000, 0)]
    [InlineData(4_000_000, 120)]
    public void slots_round_to_the_nearest_grid_point(long microseconds, long slot)
        => ConstantRateScheduler<int>.SlotOf(microseconds, 0, Thirty).Should().Be(slot);

    [Fact]
    public void ntsc_slots_stay_exact_over_an_hour()
    {
        //Arrange
        var rate = new FrameRate(30000, 1001);
        long index = 107_892; // one hour

        //Act
        long microseconds = (long)Math.Round(index * 1001 * 1_000_000.0 / 30000);

        //Assert
        ConstantRateScheduler<int>.SlotOf(microseconds, 0, rate).Should().Be(index);
    }
}
