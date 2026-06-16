namespace AutoContext.Engine.Core.Workers;

/// <summary>
/// Confirms that a freshly spawned worker is ready to serve requests by
/// dialling its named pipe. The engine owns the worker process, so a
/// successful connection — rather than a stderr handshake line — is the
/// readiness barrier: the pipe is connectable only once the worker has
/// bound its listener.
/// </summary>
internal interface IWorkerConnectionProbe
{
    /// <summary>
    /// Completes once a connection to <paramref name="endpoint"/>
    /// succeeds. Implementations retry internally for the start-up window
    /// during which the worker has been launched but has not yet bound
    /// the pipe; the wait ends only on first success or when
    /// <paramref name="cancellationToken"/> fires.
    /// </summary>
    /// <param name="endpoint">The worker's listen address.</param>
    /// <param name="cancellationToken">Cancels the wait (for example when
    /// the worker exits before becoming connectable, or the manager is
    /// disposed).</param>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> fired before a connection
    /// succeeded.</exception>
    Task WaitForConnectionAsync(string endpoint, CancellationToken cancellationToken);
}
