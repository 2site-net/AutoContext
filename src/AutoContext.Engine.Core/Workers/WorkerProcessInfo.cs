namespace AutoContext.Engine.Core.Workers;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;

/// <summary>
/// Launch specification for an engine worker process. Extends the base
/// <see cref="ProcessInfo"/> with the worker's stable identifier and the
/// stderr ready marker the manager waits on before treating the worker as
/// available.
/// </summary>
internal sealed record WorkerProcessInfo : ProcessInfo
{
    /// <summary>
    /// The exact stderr line the worker emits once it is ready to accept
    /// requests (for example <c>[Worker.DotNet] Ready.</c>).
    /// </summary>
    public required string ReadyMarker { get; init; }

    /// <summary>
    /// The worker's stable short identifier (for example <c>dotnet</c>);
    /// the key callers gate launches on.
    /// </summary>
    public required string WorkerId { get; init; }
}
