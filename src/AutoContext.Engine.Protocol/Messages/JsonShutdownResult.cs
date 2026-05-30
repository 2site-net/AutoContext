namespace AutoContext.Engine.Protocol.Messages;

using System.Text.Json.Serialization;

/// <summary>
/// Result body for <c>Engine.Shutdown</c>. Acknowledges that the
/// engine has accepted the shutdown request and committed to the
/// drain-and-exit sequence described in <c>design § RPC surface</c>.
/// Returning <c>{ accepted: true }</c> is the only outcome today;
/// the field exists for forward compatibility (e.g. a future engine
/// might decline shutdown when blocking work is in-flight).
/// </summary>
public sealed record JsonShutdownResult
{
    /// <summary>
    /// Whether the engine accepted the shutdown request. Always
    /// <see langword="true"/> in the current protocol revision.
    /// </summary>
    [JsonPropertyName("accepted")]
    public bool Accepted { get; init; }
}
