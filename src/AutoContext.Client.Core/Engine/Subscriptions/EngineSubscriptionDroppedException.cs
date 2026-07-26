namespace AutoContext.Client.Core.Engine.Subscriptions;

/// <summary>
/// Thrown when the engine drops a subscription because this client
/// could not keep up with the stream: the engine's per-subscriber
/// buffer overflowed and it sent the terminal <c>dropped</c> frame
/// before closing the stream. Signals that the consumer fell behind,
/// not that the engine faulted — a caller that catches this can
/// re-subscribe to resume from the current snapshot.
/// </summary>
public sealed class EngineSubscriptionDroppedException(string method, string reason)
    : Exception($"Engine subscription '{method}' was dropped: {reason}")
{
    /// <summary>The subscription method that was dropped.</summary>
    public string Method { get; } = method;

    /// <summary>The engine's wire reason for the drop.</summary>
    public string Reason { get; } = reason;
}
