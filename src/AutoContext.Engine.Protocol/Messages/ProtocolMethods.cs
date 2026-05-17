namespace AutoContext.Engine.Protocol.Messages;

/// <summary>
/// String constants for the engine's JSON-RPC method names. Kept
/// in the protocol assembly so both sides of the wire reference
/// the same identifiers without copy-paste drift.
/// </summary>
public static class ProtocolMethods
{
    /// <summary>
    /// Mandatory first frame on every <c>rpc</c> and <c>events</c>
    /// pipe per <c>design § Lifecycle &gt; Wire-protocol handshake</c>.
    /// </summary>
    public const string Hello = "Engine.Hello";

    /// <summary>
    /// Requests graceful shutdown of the engine. The engine replies
    /// immediately with <c>{ accepted: true }</c> and then begins
    /// the drain-and-exit sequence (drain <c>rpc</c>, emit
    /// <c>shutting-down</c> on <c>events</c>, close pipes, exit 0).
    /// Defined in <c>design § RPC surface</c>.
    /// </summary>
    public const string Shutdown = "Engine.Shutdown";
}
