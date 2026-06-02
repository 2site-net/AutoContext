namespace AutoContext.Engine.Core.Logging;

using System.Runtime.CompilerServices;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Logs;

/// <summary>
/// Maps a <see cref="BroadcasterSubscription{T}"/> of <see cref="JsonLogRecord"/>
/// onto the <see cref="JsonLogStreamFrame"/> wire protocol.
/// </summary>
internal static class LogStreamFrames
{
    /// <summary>
    /// Drains <paramref name="subscription"/> until the channel
    /// completes or <paramref name="cancellationToken"/> fires,
    /// yielding each <see cref="JsonLogRecord"/> as a
    /// <see cref="JsonLogRecordFrame"/>. If the broadcaster evicted
    /// the subscriber for slowness, a terminal
    /// <see cref="JsonLogEvictedFrame"/> with reason
    /// <see cref="JsonLogEvictedFrame.SlowSubscriberReason"/> is
    /// yielded after the channel completes so the caller can flush it
    /// to the wire before closing the connection.
    /// </summary>
    /// <param name="subscription">Live log subscription to drain.</param>
    /// <param name="cancellationToken">Cancels the drain.</param>
    /// <returns>The framed log stream.</returns>
    public static async IAsyncEnumerable<JsonLogStreamFrame> MapAsync(
        BroadcasterSubscription<JsonLogRecord> subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        await foreach (var record in subscription.ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return new JsonLogRecordFrame(record);
        }

        if (subscription.WasEvicted)
        {
            yield return new JsonLogEvictedFrame(JsonLogEvictedFrame.SlowSubscriberReason);
        }
    }
}
