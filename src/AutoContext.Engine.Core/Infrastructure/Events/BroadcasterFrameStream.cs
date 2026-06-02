namespace AutoContext.Engine.Core.Infrastructure.Events;

using System.Runtime.CompilerServices;

/// <summary>
/// Base <see cref="IBroadcasterFrameStream{TPayload, TFrame}"/> that owns the
/// drain-and-terminal-flush skeleton shared by every stream. It drains the
/// subscription and maps each payload through <see cref="ToFrame"/>. When the
/// broadcaster dropped the subscriber for slowness, it yields one terminal
/// frame from <see cref="CreateDroppedFrame"/> after the channel completes, so
/// the caller can flush it to the wire before closing the connection.
/// Subclasses supply only the two domain mappings.
/// </summary>
/// <typeparam name="TPayload">Payload fanned in by the broadcaster.</typeparam>
/// <typeparam name="TFrame">Wire frame yielded to the caller.</typeparam>
internal abstract class BroadcasterFrameStream<TPayload, TFrame> : IBroadcasterFrameStream<TPayload, TFrame>
    where TPayload : class
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<TFrame> StreamAsync(
        BroadcasterSubscription<TPayload> subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        await foreach (var payload in subscription.ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return ToFrame(payload);
        }

        if (subscription.WasDropped)
        {
            yield return CreateDroppedFrame();
        }
    }

    /// <summary>
    /// Creates the terminal frame yielded when the subscriber was
    /// dropped for slowness.
    /// </summary>
    /// <returns>The terminal <c>dropped</c> frame.</returns>
    protected abstract TFrame CreateDroppedFrame();

    /// <summary>
    /// Maps a drained <paramref name="payload"/> onto its wire frame.
    /// </summary>
    /// <param name="payload">Payload fanned in by the broadcaster.</param>
    /// <returns>The wire frame for <paramref name="payload"/>.</returns>
    protected abstract TFrame ToFrame(TPayload payload);
}
