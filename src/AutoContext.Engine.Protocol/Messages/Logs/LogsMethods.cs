namespace AutoContext.Engine.Protocol.Messages.Logs;

/// <summary>
/// String constants for the logs-scoped JSON-RPC methods on the
/// engine wire. Kept in the protocol assembly so both sides
/// reference the same identifiers without copy-paste drift, and
/// grouped alongside the logs DTOs (<see cref="JsonLogRecord"/>,
/// <see cref="JsonLogStreamFrame"/>, <see cref="JsonLogsGetEngineParams"/>,
/// <see cref="JsonLogsGetEngineResult"/>) they pair with.
/// </summary>
public static class LogsMethods
{
    /// <summary>
    /// Returns a bounded snapshot of records from the engine's
    /// active <c>engine.log</c> file. Unary RPC; never streams.
    /// Filters: <c>opts.lastN</c> caps from the tail, <c>opts.since</c>
    /// filters by timestamp. The reply carries
    /// <c>truncated: true</c> when the active file rolled past the
    /// requested range. Defined in <c>design § RPC surface</c>.
    /// </summary>
    public const string GetEngine = "Logs.GetEngine";

    /// <summary>
    /// Server-streaming RPC that tails the engine's live
    /// <see cref="JsonLogRecord"/> firehose: each frame on the wire
    /// is a <see cref="JsonLogStreamFrame"/> (<see cref="JsonLogRecordFrame"/>
    /// for records or a terminal <see cref="JsonLogDroppedFrame"/> when
    /// the subscriber is dropped for slowness) carried as the
    /// <c>result</c> of a <c>JsonRpcStreamNext</c> envelope.
    /// Graceful broadcaster completion or peer-close terminates
    /// the stream without a wire-level error. Defined in
    /// <c>design § RPC surface</c>.
    /// </summary>
    public const string TailEngine = "Logs.TailEngine";
}
