namespace AutoContext.Framework.Workers;

/// <summary>
/// Strongly-typed options for the shared worker host: the named pipe to
/// listen on and the stderr ready-marker emitted once the pipe is accepting
/// connections.
/// </summary>
/// <remarks>
/// Each <c>AutoContext.Worker.*</c> process binds and registers an instance
/// of this type. <see cref="Pipe"/> is computed by
/// <c>WorkerHostBuilderExtensions.ConfigureWorkerHost</c> by formatting
/// <c>worker-&lt;workerId&gt;</c> with the parsed
/// <c>--instance-id</c> via
/// <see cref="ServiceAddressFormatter.Format"/>; workers no longer
/// receive an explicit <c>--pipe</c> switch. <see cref="ReadyMarker"/>
/// is supplied by the process itself (e.g.
/// <c>"[AutoContext.Worker.DotNet] Ready."</c>) and is scraped from the
/// worker's stderr by the parent process to detect that the pipe is up.
/// </remarks>
public sealed class WorkerHostOptions
{
    /// <summary>
    /// Named pipe the worker listens on for per-task requests. Computed
    /// at host-bootstrap time from <c>worker-&lt;workerId&gt;</c> +
    /// <c>--instance-id</c>; never set directly via configuration.
    /// </summary>
    public string Pipe { get; init; } = string.Empty;

    /// <summary>
    /// Exact text written to stderr once the pipe server is accepting
    /// connections. Used by parent processes as a readiness handshake.
    /// </summary>
    public string ReadyMarker { get; init; } = string.Empty;

    /// <summary>
    /// Optional service address (<c>autocontext.log#&lt;instance-id&gt;</c>)
    /// the worker connects to for streaming structured log records
    /// (NDJSON) to the parent process. When empty (standalone runs, or
    /// the parent did not pass <c>--service log=...</c>) log output
    /// falls back to stderr.
    /// </summary>
    public string LogServiceAddress { get; init; } = string.Empty;

    /// <summary>
    /// Optional service address
    /// (<c>autocontext.health-monitor#&lt;instance-id&gt;</c>) the worker
    /// connects to on startup to announce its presence to the
    /// extension-side <c>HealthMonitorServer</c>. The connection is held
    /// open for the lifetime of the worker process and the server uses
    /// its closure as the worker's exit signal. When empty (standalone
    /// runs, or the parent did not pass
    /// <c>--service health-monitor=...</c>) no liveness signal is sent.
    /// </summary>
    public string HealthMonitorServiceAddress { get; init; } = string.Empty;
}
