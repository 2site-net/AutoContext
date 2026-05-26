namespace AutoContext.Engine.Core.Logging;

using System.Runtime.CompilerServices;
using System.Threading.Channels;

using AutoContext.Engine.Protocol.Messages.Logs;

/// <summary>
/// Handle returned from
/// <see cref="LogSubscriptionBroadcaster.Subscribe"/>. Drains
/// <see cref="LogStreamFrame"/> values via <see cref="ReadAllAsync"/>
/// and releases the subscription on <see cref="Dispose"/>.
/// </summary>
/// <remarks>
/// Each drained <see cref="LogRecord"/> is yielded as a
/// <see cref="LogRecordFrame"/>; if the broadcaster evicted the
/// subscriber for slowness, a terminal
/// <see cref="LogEvictedFrame"/> with reason
/// <see cref="LogEvictedFrame.SlowSubscriberReason"/> is yielded
/// after the underlying channel completes so the pipe acceptor can
/// flush it to the wire before closing the connection.
/// </remarks>
internal sealed class LogSubscription : IDisposable
{
    private int _disposed;
    private readonly ChannelReader<LogRecord> _reader;
    private readonly Action _release;
    private readonly Func<bool> _wasEvicted;

    /// <summary>
    /// Creates a new <see cref="LogSubscription"/>.
    /// </summary>
    /// <param name="reader">Reader half of the per-subscriber
    /// bounded channel the broadcaster fans records into.</param>
    /// <param name="release">Callback invoked exactly once on
    /// <see cref="Dispose"/> to unsubscribe from the
    /// broadcaster and complete the underlying channel.</param>
    /// <param name="wasEvicted">Probe consulted after the channel
    /// completes to decide whether a terminal
    /// <see cref="LogEvictedFrame"/> is yielded; closes over the
    /// owning <see cref="LogSubscriber"/>'s state.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public LogSubscription(
        ChannelReader<LogRecord> reader,
        Action release,
        Func<bool> wasEvicted)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(wasEvicted);

        _reader = reader;
        _release = release;
        _wasEvicted = wasEvicted;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _release();
    }

    /// <summary>
    /// Drains broadcaster frames until the channel completes or
    /// <paramref name="cancellationToken"/> fires. If the subscriber
    /// was evicted for slowness, yields a final
    /// <see cref="LogEvictedFrame"/> after the channel completes so
    /// the caller can flush it to the wire before closing.
    /// </summary>
    public async IAsyncEnumerable<LogStreamFrame> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var record in _reader
            .ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return new LogRecordFrame(record);
        }

        if (_wasEvicted())
        {
            yield return new LogEvictedFrame(LogEvictedFrame.SlowSubscriberReason);
        }
    }
}
