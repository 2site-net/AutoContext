namespace AutoContext.Engine.Core.Tests.Support.Lifecycle;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Protocol.Messages.Lifecycle;

internal static class LifecycleStreamTestReader
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public static async Task<IReadOnlyList<JsonLifecycleEvent>> ReadAllAsync(
        BroadcasterSubscription<JsonLifecycleEvent> subscription,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout ?? DefaultTimeout);

        var events = new List<JsonLifecycleEvent>();

        await foreach (var evt in LifecycleEventFrames.MapAsync(subscription, cts.Token).ConfigureAwait(false))
        {
            events.Add(evt);
        }

        return events;
    }

    public static async Task<IReadOnlyList<JsonLifecycleEvent>> ReadUntilCountAsync(
        BroadcasterSubscription<JsonLifecycleEvent> subscription,
        int expectedCount,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedCount);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout ?? DefaultTimeout);

        var events = new List<JsonLifecycleEvent>();

        await foreach (var evt in LifecycleEventFrames.MapAsync(subscription, cts.Token).ConfigureAwait(false))
        {
            events.Add(evt);

            if (events.Count >= expectedCount)
            {
                break;
            }
        }

        return events;
    }
}
