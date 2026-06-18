namespace AutoContext.Engine.Core.Workers;

using System.IO.Pipes;

using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Production <see cref="IWorkerConnectionProbe"/> that polls the worker's
/// named pipe through <see cref="PipeTransport"/>. Each attempt opens a
/// connection with a short connect timeout and closes it immediately; a
/// clean failure (the worker has not bound the pipe yet) is swallowed and
/// retried after a brief delay. The probe never imposes an overall
/// deadline — readiness is bounded instead by the caller's cancellation
/// and by the worker's process-exit signal, which the
/// <see cref="WorkerProcessService"/> translates into cancellation.
/// </summary>
internal sealed partial class WorkerConnectionProbe : IWorkerConnectionProbe
{
    private const int ConnectTimeoutMilliseconds = 250;
    private const int RetryDelayMilliseconds = 25;

    private readonly PipeTransport _transport;
    private readonly ILogger<WorkerConnectionProbe> _logger;

    /// <summary>
    /// Creates a new <see cref="WorkerConnectionProbe"/>.
    /// </summary>
    /// <param name="transport">The connect primitive.</param>
    /// <param name="logger">Diagnostic sink.</param>
    /// <exception cref="ArgumentNullException">Any argument is
    /// <see langword="null"/>.</exception>
    public WorkerConnectionProbe(PipeTransport transport, ILogger<WorkerConnectionProbe> logger)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(logger);

        _transport = transport;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task WaitForConnectionAsync(string endpoint, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpoint);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var stream = await _transport.ConnectAsync(
                    endpoint,
                    ConnectTimeoutMilliseconds,
                    PipeDirection.InOut,
                    cancellationToken).ConfigureAwait(false);

                await stream.DisposeAsync().ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (
                exception is IOException or TimeoutException or UnauthorizedAccessException)
            {
                LogNotReady(_logger, endpoint);
            }

            await Task.Delay(RetryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Trace,
        Message = "Worker endpoint '{Endpoint}' not yet connectable; retrying.")]
    private static partial void LogNotReady(ILogger logger, string endpoint);
}
