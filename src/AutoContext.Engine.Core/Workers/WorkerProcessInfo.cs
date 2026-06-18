namespace AutoContext.Engine.Core.Workers;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;

/// <summary>
/// Launch specification for an engine worker process. Extends the base
/// <see cref="ProcessInfo"/> with the worker's stable identifier and the
/// named-pipe address the manager dials to confirm the worker is ready.
/// </summary>
internal sealed record WorkerProcessInfo : ProcessInfo
{
    /// <summary>
    /// The named-pipe address the worker listens on (for example
    /// <c>autocontext.worker-dotnet#&lt;instanceId&gt;</c>). The manager
    /// treats the worker as ready the first time a connection to this
    /// address succeeds.
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// The worker's stable short identifier (for example <c>dotnet</c>);
    /// the key callers gate launches on.
    /// </summary>
    public required string WorkerId { get; init; }
}
