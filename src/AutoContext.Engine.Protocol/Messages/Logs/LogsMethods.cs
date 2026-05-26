namespace AutoContext.Engine.Protocol.Messages.Logs;

/// <summary>
/// String constants for the logs-scoped JSON-RPC methods on the
/// engine wire. Kept in the protocol assembly so both sides
/// reference the same identifiers without copy-paste drift, and
/// grouped alongside the logs DTOs (<see cref="LogRecord"/>,
/// <see cref="LogStreamFrame"/>, <see cref="LogsGetEngineParams"/>,
/// <see cref="LogsGetEngineResult"/>) they pair with.
/// </summary>
/// <remarks>
/// <para>
/// Phase 2a row 6 introduces <see cref="GetEngine"/> only. The
/// matching <c>Logs.TailEngine</c> verb is deferred to the Phase 3
/// prelude, where it lands together with the server-streaming
/// response convention on the <c>rpc</c> pipe that
/// <c>Config.Subscribe</c> / <c>Instructions.Subscribe</c> also
/// rely on. <c>Logs.GetWorker</c> / <c>Logs.TailWorker</c> land in
/// Phase 8 alongside worker → engine logging integration.
/// </para>
/// </remarks>
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
}
