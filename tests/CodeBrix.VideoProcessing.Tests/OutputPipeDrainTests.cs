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

    // TinyDecode's single 16x16 yuv420p frame.
    private const int TinyFrameBytes = 16 * 16 * 3 / 2;

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
        var processor = TinyDecode(StallingSink()).CancellableThrough(cancellation.Token);
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
        var processor = TinyDecode(StallingSink()).CancellableThrough(TestContext.Current.CancellationToken);
        processor.OutputPipeDrainTimeout = TimeSpan.FromSeconds(1);

        //Act
        Action act = () => processor.ProcessSynchronously();

        //Assert
        var thrown = Assert.Throws<FFMpegException>(act);
        (thrown.Type).Should().Be(FFMpegExceptionType.Process);
        (thrown.Message).Should().Contain("did not drain");
    }

    [Fact]
    public async Task Cancelling_while_a_token_ignoring_sink_drains_after_ffmpeg_exited_surfaces_operation_canceled_promptly()
    {
        //Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var processor = TinyDecode(TokenIgnoringSink()).CancellableThrough(cancellation.Token);
        var stopwatch = Stopwatch.StartNew();
        cancellation.CancelAfter(TimeSpan.FromSeconds(2));

        //Act
        var act = () => processor.ProcessAsynchronously();

        //Assert - before the fix this waited out the 30 s default and then surfaced ObjectDisposedException.
        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
        (thrown.CancellationToken).Should().Be(cancellation.Token);
        (stopwatch.Elapsed).Should().BeLessThan(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Cancelling_while_ffmpeg_runs_with_a_token_ignoring_sink_surfaces_operation_canceled()
    {
        //Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var verySlow = new ThrottledStream(TimeSpan.FromMilliseconds(20), 4096);
        var sink = new StreamPipeSink((input, token) => input.CopyToAsync(verySlow, 4096, CancellationToken.None));
        var processor = RawVideoDecode(sink).CancellableThrough(cancellation.Token);
        var stopwatch = Stopwatch.StartNew();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));

        //Act
        Action act = () => processor.ProcessSynchronously();

        //Assert - the sink fails on its disposed pipe; the caller is told about the cancel, not about the pipe.
        var thrown = Assert.ThrowsAny<OperationCanceledException>(act);
        (thrown.CancellationToken).Should().Be(cancellation.Token);
        (stopwatch.Elapsed).Should().BeLessThan(TimeSpan.FromSeconds(15));
        (verySlow.BytesWritten).Should().BeLessThan(31_000_000);
    }

    [Fact]
    public void A_sink_failure_that_is_not_a_tear_down_symptom_still_surfaces_after_a_cancel()
    {
        //Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var sink = new StreamPipeSink(async (input, token) =>
        {
            var frame = new byte[TinyFrameBytes];
            await input.ReadExactlyAsync(frame, CancellationToken.None).ConfigureAwait(false);
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(20, CancellationToken.None).ConfigureAwait(false);
            }

            throw new InvalidOperationException("sink failed on its own");
        });
        var processor = TinyDecode(sink).CancellableThrough(cancellation.Token);
        cancellation.CancelAfter(TimeSpan.FromSeconds(2));

        //Act
        Action act = () => processor.ProcessSynchronously();

        //Assert
        var thrown = Assert.Throws<InvalidOperationException>(act);
        (thrown.Message).Should().Be("sink failed on its own");
    }

    [Fact]
    public void Drain_timeout_set_on_ffoptions_is_honoured()
    {
        //Arrange
        var options = GlobalFFOptions.Current.Clone();
        options.OutputPipeDrainTimeout = TimeSpan.FromSeconds(1);
        var processor = TinyDecode(StallingSink()).CancellableThrough(TestContext.Current.CancellationToken);
        var stopwatch = Stopwatch.StartNew();

        //Act
        Action act = () => processor.ProcessSynchronously(true, options);

        //Assert
        var thrown = Assert.Throws<FFMpegException>(act);
        (thrown.Type).Should().Be(FFMpegExceptionType.Process);
        (thrown.Message).Should().Contain("did not drain");
        (thrown.Message).Should().Contain(" 1 s later");
        (stopwatch.Elapsed).Should().BeLessThan(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Drain_timeout_set_through_configure_is_honoured_asynchronously()
    {
        //Arrange
        var processor = TinyDecode(StallingSink())
            .Configure(options => options.OutputPipeDrainTimeout = TimeSpan.FromMilliseconds(1500))
            .CancellableThrough(TestContext.Current.CancellationToken);

        //Act
        var act = () => processor.ProcessAsynchronously();

        //Assert
        var thrown = await Assert.ThrowsAsync<FFMpegException>(act);
        (thrown.Message).Should().Contain(" 1.5 s later");
    }

    [Fact]
    public void Drain_timeout_defaults_to_thirty_seconds()
        => (new FFOptions().OutputPipeDrainTimeout).Should().Be(TimeSpan.FromSeconds(30));

    [Fact]
    public void Drain_timeout_survives_clone_and_json()
    {
        //Arrange
        var options = new FFOptions { OutputPipeDrainTimeout = TimeSpan.FromSeconds(5) };

        //Act
        var cloned = options.Clone();
        var json = System.Text.Json.JsonSerializer.Serialize(options);
        var read = System.Text.Json.JsonSerializer.Deserialize<FFOptions>(json);

        //Assert
        (cloned.OutputPipeDrainTimeout).Should().Be(TimeSpan.FromSeconds(5));
        (read.OutputPipeDrainTimeout).Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void A_negative_drain_timeout_fails_before_ffmpeg_starts()
    {
        //Arrange
        var options = GlobalFFOptions.Current.Clone();
        options.OutputPipeDrainTimeout = TimeSpan.FromSeconds(-2);
        var processor = TinyDecode(StallingSink()).CancellableThrough(TestContext.Current.CancellationToken);

        //Act
        Action act = () => processor.ProcessSynchronously(true, options);

        //Assert
        var thrown = Assert.Throws<ArgumentOutOfRangeException>(act);
        (thrown.ParamName).Should().Be(nameof(FFOptions.OutputPipeDrainTimeout));
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

    // One 16x16 frame, so ffmpeg is done almost at once and the processor is left waiting on the sink alone.
    private static FFMpegArgumentProcessor TinyDecode(IPipeSink sink)
    {
        return FFMpegArguments
            .FromFileInput(TestResources.WebmVideo)
            .OutputToPipe(sink, opt => opt
                .WithCustomArgument("-an -frames:v 1 -vf scale=16:16")
                .ForcePixelFormat("yuv420p")
                .ForceFormat("rawvideo"));
    }

    // Reads the one frame TinyDecode writes, then stalls without ever reading to EOF. It must read that frame: on
    // Windows the output pipe has no buffer, so ffmpeg's write blocks until the sink reads it, and a sink that read
    // nothing would keep ffmpeg from ever exiting - the drain timeout counts from that exit, so it never started and
    // the test hung. (Unix domain sockets buffer the frame, which is why reading nothing used to be enough there.)
    private static StreamPipeSink StallingSink()
    {
        return new StreamPipeSink(async (input, token) =>
        {
            var frame = new byte[TinyFrameBytes];
            await input.ReadExactlyAsync(frame, token).ConfigureAwait(false);
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
        });
    }

    // Reads the one frame TinyDecode writes, then never finishes and never looks at its token: every 50 ms it reads
    // the pipe again (0 bytes at EOF) - until the pipe is disposed under it and the read throws ObjectDisposedException.
    private static StreamPipeSink TokenIgnoringSink()
    {
        return new StreamPipeSink(async (input, token) =>
        {
            var frame = new byte[TinyFrameBytes];
            await input.ReadExactlyAsync(frame, CancellationToken.None).ConfigureAwait(false);
            var probe = new byte[1];
            while (true)
            {
                await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
                _ = await input.ReadAsync(probe, CancellationToken.None).ConfigureAwait(false);
            }
        });
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
