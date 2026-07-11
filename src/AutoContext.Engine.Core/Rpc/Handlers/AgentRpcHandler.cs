namespace AutoContext.Engine.Core.Rpc.Handlers;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using AutoContext.Engine.Core.Features.Agent;
using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Agent;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging;

/// <summary>
/// The <c>Agent.*</c> handler — the engine-side end of the agent-loop
/// signal surface. The five <c>Agent.SubagentStarted</c> /
/// <c>SubagentStopped</c> / <c>Compacted</c> / <c>ToolUsed</c> /
/// <c>TurnEnded</c> methods are fire-and-forget JSON-RPC notifications
/// (no <c>id</c>, no response): each is mapped onto a unified
/// <see cref="JsonAgentEvent"/> and published to the shared
/// <see cref="Broadcaster{T}"/>. <c>Agent.Events.Subscribe</c> is the
/// server-streaming re-broadcast every other client drains — a pure
/// live tail with no snapshot-on-subscribe seed.
/// </summary>
/// <remarks>
/// Because a notification carries no <c>id</c>, the handler never replies
/// to the five signal methods — not even on a malformed payload. A record
/// that fails to deserialise, or arrives after the broadcaster has
/// completed, is logged at debug and dropped: agent events are best-effort
/// UX signals, never a correctness input.
/// </remarks>
internal sealed partial class AgentRpcHandler : IRpcMethodHandler
{
    private readonly Broadcaster<JsonAgentEvent> _broadcaster;
    private readonly AgentEventFrameStream _frameStream = new();
    private readonly ILogger<AgentRpcHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentRpcHandler"/>
    /// class.
    /// </summary>
    /// <param name="broadcaster">Shared fan-out the mapped agent events
    /// are published to and that <c>Agent.Events.Subscribe</c> drains.</param>
    /// <param name="logger">Diagnostic sink for dropped events.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public AgentRpcHandler(Broadcaster<JsonAgentEvent> broadcaster, ILogger<AgentRpcHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(broadcaster);
        ArgumentNullException.ThrowIfNull(logger);

        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Methods { get; } =
    [
        AgentMethods.SubagentStarted,
        AgentMethods.SubagentStopped,
        AgentMethods.Compacted,
        AgentMethods.ToolUsed,
        AgentMethods.TurnEnded,
        AgentMethods.EventsSubscribe,
    ];

    /// <inheritdoc />
    public ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Method == AgentMethods.EventsSubscribe)
        {
            return new ValueTask<RpcHandlerResult>(HandleEventsSubscribe());
        }

        return new ValueTask<RpcHandlerResult>(HandleNotification(request));
    }

    private static T? Deserialize<T>(JsonRpcRequest request, JsonTypeInfo<T> typeInfo)
        where T : class
        => request.Params is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } element
            ? element.Deserialize(typeInfo)
            : null;

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Agent notification '{Method}' dropped; the event broadcaster is closed.")]
    private static partial void LogEventDroppedBroadcasterClosed(ILogger logger, string method);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Agent notification '{Method}' dropped; it carried no params payload.")]
    private static partial void LogEventMissingParams(ILogger logger, string method);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Agent notification '{Method}' dropped; its params failed to parse.")]
    private static partial void LogEventParseFailed(ILogger logger, string method, Exception exception);

    private static JsonAgentEvent? MapCompacted(JsonRpcRequest request)
    {
        var parameters = Deserialize(request, ProtocolJsonContext.Default.JsonAgentCompactedParams);
        return parameters is null
            ? null
            : new JsonAgentEvent
            {
                Kind = AgentEventKinds.Compacted,
                SessionId = parameters.SessionId,
            };
    }

    private static JsonAgentEvent? MapEvent(JsonRpcRequest request)
        => request.Method switch
        {
            AgentMethods.SubagentStarted => MapSubagentStarted(request),
            AgentMethods.SubagentStopped => MapSubagentStopped(request),
            AgentMethods.Compacted => MapCompacted(request),
            AgentMethods.ToolUsed => MapToolUsed(request),
            _ => MapTurnEnded(request),
        };

    private static JsonAgentEvent? MapSubagentStarted(JsonRpcRequest request)
    {
        var parameters = Deserialize(request, ProtocolJsonContext.Default.JsonAgentSubagentStartedParams);
        return parameters is null
            ? null
            : new JsonAgentEvent
            {
                Kind = AgentEventKinds.SubagentStarted,
                SessionId = parameters.SessionId,
                TaskPrompt = parameters.TaskPrompt,
            };
    }

    private static JsonAgentEvent? MapSubagentStopped(JsonRpcRequest request)
    {
        var parameters = Deserialize(request, ProtocolJsonContext.Default.JsonAgentSubagentStoppedParams);
        return parameters is null
            ? null
            : new JsonAgentEvent
            {
                Kind = AgentEventKinds.SubagentStopped,
                SessionId = parameters.SessionId,
            };
    }

    private static JsonAgentEvent? MapToolUsed(JsonRpcRequest request)
    {
        var parameters = Deserialize(request, ProtocolJsonContext.Default.JsonAgentToolUsedParams);
        return parameters is null
            ? null
            : new JsonAgentEvent
            {
                Kind = AgentEventKinds.ToolUsed,
                SessionId = parameters.SessionId,
                ToolName = parameters.ToolName,
                Outcome = parameters.Outcome,
            };
    }

    private static JsonAgentEvent? MapTurnEnded(JsonRpcRequest request)
    {
        var parameters = Deserialize(request, ProtocolJsonContext.Default.JsonAgentTurnEndedParams);
        return parameters is null
            ? null
            : new JsonAgentEvent
            {
                Kind = AgentEventKinds.TurnEnded,
                SessionId = parameters.SessionId,
            };
    }

    [SuppressMessage("Reliability", "CA2000",
        Justification = "Ownership of the subscription is handed off to StreamingHandlerResult.PostFlush, which the RpcConnectionProcessor runs in a finally block — disposal is guaranteed on every path.")]
    private StreamingHandlerResult HandleEventsSubscribe()
    {
        // Subscription is created up-front so its disposal can be
        // routed through StreamingHandlerResult.PostFlush, which the
        // processor runs in a finally — guaranteeing the broadcaster
        // slot is released even when the peer hangs up mid-stream or
        // the iterator faults. No seed: Agent.Events is a pure live
        // tail, so a fresh subscriber sees only events published after
        // it enrolled.
        var subscription = _broadcaster.Subscribe();

        return new StreamingHandlerResult(
            Payloads: MapFramesAsync(subscription),
            PostFlush: () =>
            {
                subscription.Dispose();
                return Task.CompletedTask;
            });
    }

    private NotificationHandlerResult HandleNotification(JsonRpcRequest request)
    {
        JsonAgentEvent? agentEvent;

        try
        {
            agentEvent = MapEvent(request);
        }
        catch (JsonException ex)
        {
            // Notification: no id, so no error reply — log and drop.
            LogEventParseFailed(_logger, request.Method, ex);
            return new NotificationHandlerResult();
        }

        if (agentEvent is null)
        {
            LogEventMissingParams(_logger, request.Method);
            return new NotificationHandlerResult();
        }

        if (!_broadcaster.TryPublish(agentEvent))
        {
            // The broadcaster has completed (engine shutting down);
            // nothing left to fan the event out to.
            LogEventDroppedBroadcasterClosed(_logger, request.Method);
        }

        return new NotificationHandlerResult();
    }

    private async IAsyncEnumerable<JsonElement> MapFramesAsync(
        BroadcasterSubscription<JsonAgentEvent> subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in _frameStream
            .StreamAsync(subscription, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return JsonSerializer.SerializeToElement(
                frame, ProtocolJsonContext.Default.JsonAgentEvent);
        }
    }
}
