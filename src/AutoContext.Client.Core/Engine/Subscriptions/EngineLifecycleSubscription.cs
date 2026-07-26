namespace AutoContext.Client.Core.Engine.Subscriptions;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages.Lifecycle;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Consumer of the engine's <c>Engine.Lifecycle</c> broadcast on the
/// <c>events</c> pipe. Unlike the <c>rpc</c>-stream subscriptions, this
/// writes no subscribe request — opening the <c>events</c> connection
/// and completing the handshake is itself the subscription; the engine
/// then pushes one <see cref="JsonLifecycleEvent"/> per notification
/// (the <c>started</c> seed first, then each transition). Each
/// enumeration opens its own dedicated connection and disposes it when
/// the caller stops enumerating. A slow-subscriber drop surfaces as an
/// <see cref="EngineSubscriptionDroppedException"/>.
/// </summary>
public sealed class EngineLifecycleSubscription
{
    private readonly EngineConnector _connector;

    /// <summary>
    /// Creates a new <see cref="EngineLifecycleSubscription"/> that
    /// resolves an engine through <paramref name="connector"/> on each
    /// subscribe.
    /// </summary>
    /// <param name="connector">Find-or-spawn resolver. Must not be
    /// <see langword="null"/>.</param>
    public EngineLifecycleSubscription(EngineConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connector);

        _connector = connector;
    }

    /// <summary>
    /// Subscribes to the engine lifecycle broadcast, yielding each
    /// event until the caller stops enumerating or the engine completes
    /// the connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for the stream.</param>
    /// <exception cref="EngineSubscriptionDroppedException">The engine
    /// dropped this subscription because the consumer fell behind.</exception>
    public async IAsyncEnumerable<JsonLifecycleEvent> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connection = await _connector
            .ConnectAsync(EndpointKind.Events, cancellationToken)
            .ConfigureAwait(false);

        await using (connection.ConfigureAwait(false))
        {
            var notifications = connection.ReceiveNotificationsAsync(cancellationToken);

            await foreach (var notification in notifications.ConfigureAwait(false))
            {
                if (!string.Equals(
                    notification.Method, LifecycleMethods.Notification, StringComparison.Ordinal))
                {
                    continue;
                }

                if (notification.Params is not { } paramsElement)
                {
                    continue;
                }

                var lifecycleEvent = paramsElement.Deserialize(
                    ProtocolJsonContext.Default.JsonLifecycleEvent);
                if (lifecycleEvent is null)
                {
                    continue;
                }

                if (string.Equals(
                    lifecycleEvent.Kind, LifecycleEventKinds.Dropped, StringComparison.Ordinal))
                {
                    throw new EngineSubscriptionDroppedException(
                        LifecycleMethods.Subscribe, lifecycleEvent.Reason ?? LifecycleEventKinds.Dropped);
                }

                yield return lifecycleEvent;
            }
        }
    }
}
