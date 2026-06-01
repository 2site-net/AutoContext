namespace AutoContext.Engine.Core.Logging;

using System.Runtime.CompilerServices;
using System.Threading.Channels;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Logs;

/// <summary>
/// Handle returned from
/// <see cref="LogSubscriptionBroadcaster.Subscribe"/>. Drains
/// <see cref="JsonLogStreamFrame"/> values via <see cref="ReadAllAsync"/>
/// and releases the subscription on <see cref="Subscription{T}.Dispose"/>.
/// </summary>
/// <remarks>
/// Each drained <see cref="JsonLogRecord"/> is yielded as a
/// <see cref="JsonLogRecordFrame"/>; if the broadcaster evicted the
/// subscriber for slowness, a terminal
/// <see cref="JsonLogEvictedFrame"/> with reason
/// <see cref="JsonLogEvictedFrame.SlowSubscriberReason"/> is yielded
/// after the underlying channel completes so the pipe acceptor can
/// flush it to the wire before closing the connection.
/// </remarks>
/// <param name="reader">Reader half of the per-subscriber
/// bounded channel the broadcaster fans records into.</param>
/// <param name="release">Callback invoked exactly once on
/// <see cref="Subscription{T}.Dispose"/> to unsubscribe from the
/// broadcaster and complete the underlying channel.</param>
/// <param name="wasEvicted">Probe consulted after the channel
/// completes to decide whether a terminal
/// <see cref="JsonLogEvictedFrame"/> is yielded.</param>
/// <exception cref="ArgumentNullException">
/// Any argument is <see langword="null"/>.
/// </exception>
internal sealed class LogSubscription(
    ChannelReader<JsonLogRecord> reader,
    Action release,
    Func<bool> wasEvicted)
    : Subscription<JsonLogRecord>(reader, release, wasEvicted)
{
    /// <summary>
    /// Drains broadcaster frames until the channel completes or
    /// <paramref name="cancellationToken"/> fires. If the subscriber
    /// was evicted for slowness, yields a final
    /// <see cref="JsonLogEvictedFrame"/> after the channel completes so
    /// the caller can flush it to the wire before closing.
    /// </summary>
    public async IAsyncEnumerable<JsonLogStreamFrame> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var record in ReadPayloadsAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return new JsonLogRecordFrame(record);
        }

        if (WasEvicted)
        {
            yield return new JsonLogEvictedFrame(JsonLogEvictedFrame.SlowSubscriberReason);
        }
    }
}
