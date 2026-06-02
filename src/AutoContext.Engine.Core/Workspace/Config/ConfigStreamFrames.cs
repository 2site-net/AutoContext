namespace AutoContext.Engine.Core.Workspace.Config;

using System.Runtime.CompilerServices;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Config;

/// <summary>
/// Maps a <see cref="BroadcasterSubscription{T}"/> of
/// <see cref="JsonConfigSnapshot"/> onto the
/// <see cref="JsonConfigStreamFrame"/> wire protocol.
/// </summary>
internal static class ConfigStreamFrames
{
    /// <summary>
    /// Drains <paramref name="subscription"/> until the channel
    /// completes or <paramref name="cancellationToken"/> fires,
    /// yielding each <see cref="JsonConfigSnapshot"/> as a
    /// <see cref="JsonConfigSnapshotFrame"/>. If the broadcaster
    /// evicted the subscriber for slowness, a terminal
    /// <see cref="JsonConfigEvictedFrame"/> with reason
    /// <see cref="JsonConfigEvictedFrame.SlowSubscriberReason"/> is
    /// yielded after the channel completes so the caller can flush it
    /// to the wire before closing the connection.
    /// </summary>
    /// <param name="subscription">Live config subscription to drain.</param>
    /// <param name="cancellationToken">Cancels the drain.</param>
    /// <returns>The framed config stream.</returns>
    public static async IAsyncEnumerable<JsonConfigStreamFrame> MapAsync(
        BroadcasterSubscription<JsonConfigSnapshot> subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        await foreach (var snapshot in subscription.ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return new JsonConfigSnapshotFrame(snapshot);
        }

        if (subscription.WasEvicted)
        {
            yield return new JsonConfigEvictedFrame(JsonConfigEvictedFrame.SlowSubscriberReason);
        }
    }
}
