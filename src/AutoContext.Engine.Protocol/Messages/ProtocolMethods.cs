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

    /// <summary>
    /// Worker → engine log ingest. A JSON-RPC 2.0 notification (no
    /// <c>id</c>, no response) whose params carry one
    /// <c>Messages.Logs.JsonLogRecord</c>; the engine enqueues the
    /// record onto its ingest channel and routes it by
    /// <c>category</c> prefix to the correct on-disk log. Defined in
    /// <c>design § RPC surface</c> and <c>design § Log categories</c>.
    /// </summary>
    public const string WriteLog = "Engine.WriteLog";
}
