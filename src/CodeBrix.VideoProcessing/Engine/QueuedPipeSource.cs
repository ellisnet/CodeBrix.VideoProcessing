using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CodeBrix.VideoProcessing.Pipes;

namespace CodeBrix.VideoProcessing.Engine;

/// <summary>
/// An <see cref="IPipeSource" /> fed by the caller one chunk at a time - the bridge between the synchronous
/// <see cref="Core.Output.IStandardVideoWriter" /> calls and the asynchronous named-pipe machinery of
/// <see cref="FFMpegArgumentProcessor" />.
/// </summary>
/// <remarks>
/// Chunks are pooled arrays, returned to <see cref="ArrayPool{T}.Shared" /> once written into the pipe. The queue
/// is unbounded; <see cref="FFmpegStandardVideoWriter" /> applies the back-pressure, because only it knows whether
/// blocking one stream is safe while ffmpeg may be waiting on the other.
/// </remarks>
internal sealed class QueuedPipeSource : IPipeSource
{
    private readonly string streamArguments;
    private readonly Channel<(byte[] Buffer, int Length)> queue =
        Channel.CreateUnbounded<(byte[] Buffer, int Length)>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    private long queuedChunks;
    private long consumedChunks;

    internal QueuedPipeSource(string streamArguments)
    {
        this.streamArguments = streamArguments;
    }

    /// <summary>Raised (from the pipe thread) each time a chunk has been written into the pipe.</summary>
    internal event Action ChunkConsumed;

    /// <summary>Chunks enqueued but not yet written into the pipe.</summary>
    internal long Backlog => Interlocked.Read(ref queuedChunks) - Interlocked.Read(ref consumedChunks);

    /// <summary>Chunks written into the pipe so far.</summary>
    internal long Consumed => Interlocked.Read(ref consumedChunks);

    public string GetStreamArguments() => streamArguments;

    public async Task WriteAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        await foreach ((byte[] buffer, int length) in queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await outputStream.WriteAsync(buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                Interlocked.Increment(ref consumedChunks);
                ChunkConsumed?.Invoke();
            }
        }

        await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Queues a pooled buffer; ownership passes to the source.</summary>
    internal void Enqueue(byte[] pooledBuffer, int length)
    {
        Interlocked.Increment(ref queuedChunks);
        if (!queue.Writer.TryWrite((pooledBuffer, length)))
        {
            ArrayPool<byte>.Shared.Return(pooledBuffer);
            Interlocked.Increment(ref consumedChunks);
            throw new InvalidOperationException("The stream has already been completed.");
        }
    }

    /// <summary>Marks the end of the stream: once the queue drains, the pipe closes and ffmpeg sees end-of-file.</summary>
    internal void Complete() => queue.Writer.TryComplete();

    /// <summary>Completes the stream and returns every still-queued buffer to the pool.</summary>
    internal void Abandon()
    {
        queue.Writer.TryComplete();
        while (queue.Reader.TryRead(out (byte[] Buffer, int Length) chunk))
        {
            ArrayPool<byte>.Shared.Return(chunk.Buffer);
            Interlocked.Increment(ref consumedChunks);
        }
    }
}
