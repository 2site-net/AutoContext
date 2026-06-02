namespace AutoContext.Engine.Core.Lifecycle;

using System.Runtime.CompilerServices;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Lifecycle;

/// <summary>
/// Maps a <see cref="BroadcasterSubscription{T}"/> of
/// <see cref="JsonLifecycleEvent"/> onto the lifecycle wire stream,
/// appending a terminal <see cref="LifecycleEventKinds.Evicted"/>
/// event when the subscriber is dropped for slowness.
/// </summary>
internal static class LifecycleEventFrames
{
    /// <summary>
    /// Drains <paramref name="subscription"/> until the channel
    /// completes or <paramref name="cancellationToken"/> fires,
    /// yielding each raw <see cref="JsonLifecycleEvent"/>. If the
    /// stream evicted the subscriber for slowness, a final
    /// <see cref="LifecycleEventKinds.Evicted"/> event is yielded after
    /// the channel completes so the caller can flush it to the wire
    /// before closing the connection.
    /// </summary>
    /// <param name="subscription">Live lifecycle subscription to drain.</param>
    /// <param name="cancellationToken">Cancels the drain.</param>
    /// <returns>The lifecycle event stream.</returns>
    public static async IAsyncEnumerable<JsonLifecycleEvent> MapAsync(
        BroadcasterSubscription<JsonLifecycleEvent> subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        await foreach (var evt in subscription.ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return evt;
        }

        if (subscription.WasEvicted)
        {
            yield return new JsonLifecycleEvent
            {
                Kind = LifecycleEventKinds.Evicted,
                Reason = "slow-subscriber",
            };
        }
    }
}
