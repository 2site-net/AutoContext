namespace AutoContext.Engine.Core.Lifecycle;

using System.Runtime.CompilerServices;
using System.Threading.Channels;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Lifecycle;

/// <summary>
/// Handle returned from <see cref="LifecycleEventStream.Subscribe"/>.
/// Drains <see cref="JsonLifecycleEvent"/> values via
/// <see cref="ReadAllAsync"/> and releases the subscription on
/// <see cref="Subscription{T}.Dispose"/>.
/// </summary>
/// <param name="reader">Reader half of the per-subscriber bounded
/// channel the stream fans events into.</param>
/// <param name="release">Callback invoked exactly once on
/// <see cref="Subscription{T}.Dispose"/> to unsubscribe from the
/// stream and complete the underlying channel.</param>
/// <param name="wasEvicted">Probe consulted after the channel
/// completes to decide whether a terminal
/// <see cref="LifecycleEventKinds.Evicted"/> frame is yielded.</param>
/// <exception cref="ArgumentNullException">
/// Any argument is <see langword="null"/>.
/// </exception>
internal sealed class LifecycleEventSubscription(
    ChannelReader<JsonLifecycleEvent> reader,
    Action release,
    Func<bool> wasEvicted)
    : Subscription<JsonLifecycleEvent>(reader, release, wasEvicted)
{
    /// <summary>
    /// Drains lifecycle events until the channel completes or
    /// <paramref name="cancellationToken"/> fires. If the subscriber
    /// was evicted for slowness, yields a final
    /// <see cref="LifecycleEventKinds.Evicted"/> frame so the caller
    /// can flush it to the wire before closing.
    /// </summary>
    public async IAsyncEnumerable<JsonLifecycleEvent> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var evt in ReadPayloadsAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return evt;
        }

        if (WasEvicted)
        {
            yield return new JsonLifecycleEvent
            {
                Kind = LifecycleEventKinds.Evicted,
                Reason = "slow-subscriber",
            };
        }
    }
}
