namespace AutoContext.Client.Core.Engine.Rpc;

using System.Text.Json;

using AutoContext.Engine.Protocol.Messages.Agent;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Typed client for the engine's <c>Agent.*</c> notification surface
/// over a live <see cref="EngineConnection"/>. Each method is a
/// fire-and-forget JSON-RPC notification — no <c>id</c>, no response —
/// a hook script sends as an agent turn progresses; the engine
/// re-broadcasts them on <c>Agent.Events.Subscribe</c>. Lost
/// notifications are tolerable by design: they drive UX enrichment,
/// never a correctness decision. The re-broadcast stream is a separate
/// consumer (<c>Subscriptions.AgentEventsSubscription</c>).
/// </summary>
public sealed class AgentRpcClient
{
    private readonly EngineConnection _connection;

    /// <summary>
    /// Creates a new <see cref="AgentRpcClient"/> over
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">A live, handshaked <c>rpc</c>
    /// connection. Must not be <see langword="null"/>.</param>
    public AgentRpcClient(EngineConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    /// <summary>
    /// Signals that the agent host compacted the conversation history
    /// of session <paramref name="sessionId"/>.
    /// </summary>
    /// <param name="sessionId">Agent-loop session. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">Cancellation for the write.</param>
    public Task CompactedAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonAgentCompactedParams { SessionId = sessionId },
            ProtocolJsonContext.Default.JsonAgentCompactedParams);

        return _connection.SendNotificationAsync(AgentMethods.Compacted, parameters, cancellationToken);
    }

    /// <summary>
    /// Signals that a sub-agent started under session
    /// <paramref name="sessionId"/> with task
    /// <paramref name="taskPrompt"/>.
    /// </summary>
    /// <param name="sessionId">Agent-loop session. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="taskPrompt">The sub-agent's task prompt. Must not
    /// be <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">Cancellation for the write.</param>
    public Task SubagentStartedAsync(
        string sessionId, string taskPrompt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(taskPrompt);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonAgentSubagentStartedParams { SessionId = sessionId, TaskPrompt = taskPrompt },
            ProtocolJsonContext.Default.JsonAgentSubagentStartedParams);

        return _connection.SendNotificationAsync(AgentMethods.SubagentStarted, parameters, cancellationToken);
    }

    /// <summary>
    /// Signals that the sub-agent of session
    /// <paramref name="sessionId"/> stopped.
    /// </summary>
    /// <param name="sessionId">Agent-loop session. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">Cancellation for the write.</param>
    public Task SubagentStoppedAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonAgentSubagentStoppedParams { SessionId = sessionId },
            ProtocolJsonContext.Default.JsonAgentSubagentStoppedParams);

        return _connection.SendNotificationAsync(AgentMethods.SubagentStopped, parameters, cancellationToken);
    }

    /// <summary>
    /// Signals that tool <paramref name="toolName"/> returned with
    /// outcome <paramref name="outcome"/> in session
    /// <paramref name="sessionId"/>.
    /// </summary>
    /// <param name="sessionId">Agent-loop session. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="toolName">The tool that returned. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="outcome">The observed outcome. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">Cancellation for the write.</param>
    public Task ToolUsedAsync(
        string sessionId, string toolName, string outcome, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        ArgumentException.ThrowIfNullOrEmpty(outcome);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonAgentToolUsedParams { SessionId = sessionId, ToolName = toolName, Outcome = outcome },
            ProtocolJsonContext.Default.JsonAgentToolUsedParams);

        return _connection.SendNotificationAsync(AgentMethods.ToolUsed, parameters, cancellationToken);
    }

    /// <summary>
    /// Signals that the agent finished a turn in session
    /// <paramref name="sessionId"/>.
    /// </summary>
    /// <param name="sessionId">Agent-loop session. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">Cancellation for the write.</param>
    public Task TurnEndedAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonAgentTurnEndedParams { SessionId = sessionId },
            ProtocolJsonContext.Default.JsonAgentTurnEndedParams);

        return _connection.SendNotificationAsync(AgentMethods.TurnEnded, parameters, cancellationToken);
    }
}
