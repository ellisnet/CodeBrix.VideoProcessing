using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Exceptions;
using CodeBrix.VideoProcessing.Pipes;
using CodeBrix.VideoProcessing.Tests.Resources;
using SilverAssertions;
using Xunit;

namespace CodeBrix.VideoProcessing.Tests;

// Regression fence for the CodeBrix fix to the output-pipe tail loss in FFMpegArgumentProcessor (see
// OUTPUT PIPES DRAIN BEFORE THEY ARE DISPOSED in MAINTAINER-README.txt). Upstream FFMpegCore cancelled
// the sink and disposed the output pipe the moment ffmpeg exited, so a sink slower than ffmpeg silently
// lost whatever was still buffered in the pipe (31,053,312 of 31,104,000 bytes in the original repro).
public class OutputPipeDrainTests
{
    // input_3sec.webm is 640x360; yuv420p is 1.5 bytes per pixel.
    private const long FrameBytes = 640 * 360 * 3 / 2;

    [Fact]
    public void Slow_sink_receives_every_byte_when_processed_synchronously()
    {
        //Arrange
        var expected = DecodeToRawVideo(new ThrottledStream(TimeSpan.Zero)).BytesWritten;
        var slow = new ThrottledStream(TimeSpan.FromMilliseconds(1));

        //Act
        var result = RawVideoDecode(new StreamPipeSink(slow))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously();

        //Assert
        (result).Should().BeTrue();
        (expected).Should().BeGreaterThan(0);
        (expected % FrameBytes).Should().Be(0);
        (slow.BytesWritten).Should().Be(expected);
    }

    [Fact]
    public async Task Slow_sink_receives_every_byte_when_processed_asynchronously()
    {
        //Arrange
        var expected = DecodeToRawVideo(new ThrottledStream(TimeSpan.Zero)).BytesWritten;
        var slow = new ThrottledStream(TimeSpan.FromMilliseconds(1));

        //Act
        var result = await RawVideoDecode(new StreamPipeSink(slow))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessAsynchronously();

        //Assert
        (result).Should().BeTrue();
        (expected % FrameBytes).Should().Be(0);
        (slow.BytesWritten).Should().Be(expected);
    }

    [Fact]
    public async Task Slow_sink_that_throws_after_draining_still_surfaces_its_exception()
    {
        //Arrange
        var slow = new ThrottledStream(TimeSpan.FromMilliseconds(1));
        var sink = new StreamPipeSink(async (input, token) =>
        {
            await input.CopyToAsync(slow, 4096, token).ConfigureAwait(false);
            throw new InvalidOperationException("sink failed after draining");
        });

        //Act
        Func<Task<bool>> act = () => RawVideoDecode(sink)
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessAsynchronously();

        //Assert
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(act);
        (thrown.Message).Should().Be("sink failed after draining");
        (slow.BytesWritten % FrameBytes).Should().Be(0);
    }

    [Fact]
    public void Sink_that_throws_part_way_still_surfaces_its_exception()
    {
        //Arrange
        var sink = new StreamPipeSink(async (input, token) =>
        {
            var buffer = new byte[65536];
            await input.ReadAtLeastAsync(buffer, buffer.Length, false, token).ConfigureAwait(false);
            throw new InvalidOperationException("sink failed part way");
        });

        //Act
        Action act = () => RawVideoDecode(sink)
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously();

        //Assert
        var thrown = Assert.Throws<InvalidOperationException>(act);
        (thrown.Message).Should().Be("sink failed part way");
    }

    [Fact]
    public async Task Cancelling_while_a_slow_sink_holds_ffmpeg_back_cancels_promptly()
    {
        //Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var verySlow = new ThrottledStream(TimeSpan.FromMilliseconds(20), 4096);
        var processor = RawVideoDecode(new StreamPipeSink(verySlow)).CancellableThrough(cancellation.Token);
        var stopwatch = Stopwatch.StartNew();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));

        //Act
        var act = () => processor.ProcessAsynchronously();

        //Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
        (stopwatch.Elapsed).Should().BeLessThan(TimeSpan.FromSeconds(15));
        (verySlow.BytesWritten).Should().BeLessThan(31_000_000);
    }

    [Fact]
    public async Task Cancelling_while_waiting_for_a_sink_to_drain_after_ffmpeg_exited_cancels_promptly()
    {
        //Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var processor = TinyDecode(NeverReadingSink()).CancellableThrough(cancellation.Token);
        var stopwatch = Stopwatch.StartNew();
        cancellation.CancelAfter(TimeSpan.FromSeconds(2));

        //Act
        var act = () => processor.ProcessAsynchronously();

        //Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
        (stopwatch.Elapsed).Should().BeLessThan(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Sink_that_does_not_drain_in_time_raises_instead_of_losing_the_tail()
    {
        //Arrange
        var processor = TinyDecode(NeverReadingSink()).CancellableThrough(TestContext.Current.CancellationToken);
        processor.OutputPipeDrainTimeout = TimeSpan.FromSeconds(1);

        //Act
        Action act = () => processor.ProcessSynchronously();

        //Assert
        var thrown = Assert.Throws<FFMpegException>(act);
        (thrown.Type).Should().Be(FFMpegExceptionType.Process);
        (thrown.Message).Should().Contain("did not drain");
    }

    private static ThrottledStream DecodeToRawVideo(ThrottledStream destination)
    {
        RawVideoDecode(new StreamPipeSink(destination))
            .CancellableThrough(TestContext.Current.CancellationToken)
            .ProcessSynchronously();
        return destination;
    }

    private static FFMpegArgumentProcessor RawVideoDecode(IPipeSink sink)
    {
        return FFMpegArguments
            .FromFileInput(TestResources.WebmVideo)
            .OutputToPipe(sink, opt => opt
                .WithCustomArgument("-an")
                .ForcePixelFormat("yuv420p")
                .ForceFormat("rawvideo"));
    }

    // One 16x16 frame: small enough to sit entirely in the pipe's buffer, so ffmpeg exits at once and the
    // processor is left waiting on the sink alone.
    private static FFMpegArgumentProcessor TinyDecode(IPipeSink sink)
    {
        return FFMpegArguments
            .FromFileInput(TestResources.WebmVideo)
            .OutputToPipe(sink, opt => opt
                .WithCustomArgument("-an -frames:v 1 -vf scale=16:16")
                .ForcePixelFormat("yuv420p")
                .ForceFormat("rawvideo"));
    }

    private static StreamPipeSink NeverReadingSink()
    {
        return new StreamPipeSink((input, token) => Task.Delay(Timeout.Infinite, token));
    }

    // A write-only sink destination that counts what it is given and pauses for Delay after every
    // BytesPerPause bytes, so it consumes far more slowly than ffmpeg can decode.
    private sealed class ThrottledStream : Stream
    {
        private readonly int _bytesPerPause;
        private readonly TimeSpan _delay;
        private long _sincePause;

        public ThrottledStream(TimeSpan delay, int bytesPerPause = 32768)
        {
            _delay = delay;
            _bytesPerPause = bytesPerPause;
        }

        public long BytesWritten { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => BytesWritten;

        public override long Position
        {
            get => BytesWritten;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Count(count))
            {
                Thread.Sleep(_delay);
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Count(buffer.Length))
            {
                await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        private bool Count(int count)
        {
            BytesWritten += count;
            _sincePause += count;
            if (_delay <= TimeSpan.Zero || _sincePause < _bytesPerPause)
            {
                return false;
            }

            _sincePause = 0;
            return true;
        }
    }
}
