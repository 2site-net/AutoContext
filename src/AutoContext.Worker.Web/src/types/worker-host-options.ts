/**
 * Strongly-typed options for a worker host: the named pipe to listen on
 * and the stderr ready-marker emitted once the pipe is accepting
 * connections.
 *
 * Each `AutoContext.Worker.*` process supplies an instance. `pipe`
 * is computed from the `--instance-id` command-line argument and the
 * worker's compile-time worker id.
 * `readyMarker` is process-specific (e.g. `[AutoContext.Worker.Web] Ready.`)
 * and is scraped from the worker's stderr by the parent process to detect
 * that the pipe is up.
 */
export interface WorkerHostOptions {
    /**
     * Named pipe the worker listens on for per-task requests. May be a
     * bare name (`autocontext.web-worker`) or a fully-qualified path;
     * the service normalizes bare names to `\\.\pipe\<name>` on Windows.
     */
    readonly pipe: string;

    /**
     * Exact text written to stderr once the pipe server is accepting
     * connections. Used by parent processes as a readiness handshake.
     */
    readonly readyMarker: string;

    /**
     * Optional named pipe the worker connects to for streaming
     * structured log records (NDJSON) to the parent process. When
     * omitted (the worker is launched standalone, or the parent did
     * not pass `--service log=<address>`), log output falls back to stderr.
     */
    readonly logPipe?: string;

    /**
     * Identifier sent on the LogServer greeting so the extension can
     * route this worker's records to a per-worker output channel
     * (e.g. `AutoContext.Worker.Web`). Counterpart of
     * `IHostEnvironment.ApplicationName` on the .NET side.
     */
    readonly clientName?: string;
}
