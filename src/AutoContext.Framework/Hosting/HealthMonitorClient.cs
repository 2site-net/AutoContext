namespace AutoContext.Framework.Hosting;

using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Text;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service that announces this process's identity to the
/// extension-side <c>HealthMonitorServer</c> and keeps the named-pipe
/// connection open for the lifetime of the host. The server treats
/// the live socket as a liveness signal: when the process exits the
/// OS closes the socket and the extension UI updates its "running"
/// state.
/// </summary>
/// <remarks>
/// When <c>pipeName</c> is empty (standalone runs, no extension
/// parent) the service is a no-op — hosts stay diagnosable without
/// the call site needing to special-case standalone scenarios.
/// <para>
/// The wire protocol is intentionally minimal: connect, write the
/// client id as UTF-8 (no length prefix, no greeting wrapper), keep
/// the socket open. Failures (broken pipe, no listener) are swallowed
/// — health reporting is best-effort and must never crash the host.
/// </para>
/// <para>
/// Lives in <c>AutoContext.Framework</c> so it can be reused by every
/// hosted process (workers, the MCP server, any future managed
/// child). Callers wire it up via <c>IServiceCollection</c> with the
/// pipe name and stable client id appropriate to their process.
/// </para>
/// </remarks>
public sealed partial class HealthMonitorClient : IHostedService, IAsyncDisposable
{
    private const int ConnectTimeoutMs = 2000;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly ILogger<HealthMonitorClient> _logger;
    private readonly string _pipeName;
    private readonly string _clientId;
    private readonly CancellationTokenSource _cts = new();
    private Task? _runTask;

    /// <summary>
    /// Creates a new <see cref="HealthMonitorClient"/>. The client id
    /// is hard-coded by each host's entry point (e.g. <c>"dotnet"</c>,
    /// <c>"workspace"</c>, <c>"mcp-server"</c>) and must match the id
    /// referenced by the extension's manifests.
    /// </summary>
    /// <param name="pipeName">
    /// Name of the named pipe exposed by the extension's
    /// <c>HealthMonitorServer</c>. Empty string disables the service.
    /// </param>
    /// <param name="clientId">Stable identifier for this host process.</param>
    /// <param name="logger">Logger used for diagnostic output.</param>
    public HealthMonitorClient(
        string pipeName,
        string clientId,
        ILogger<HealthMonitorClient> logger)
    {
        ArgumentNullException.ThrowIfNull(pipeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(logger);

        _pipeName = pipeName;
        _clientId = clientId;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_pipeName))
        {
            LogSkippingNoPipe(_logger);
            return Task.CompletedTask;
        }

        _runTask = Task.Run(() => RunAsync(_cts.Token), cancellationToken);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _cts.CancelAsync().ConfigureAwait(false);

        if (_runTask is null)
        {
            return;
        }

        try
        {
            await _runTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Run task didn't observe cancellation in time — abandon it.
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);

        if (_runTask is not null)
        {
            try
            {
                await _runTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Abandon the run task.
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        if (_runTask is null || _runTask.IsCompleted)
        {
            _cts.Dispose();
        }
    }

    [SuppressMessage("Reliability", "CA2000",
        Justification = "The pipe is disposed in the finally block on every exit path.")]
    private async Task RunAsync(CancellationToken ct)
    {
        NamedPipeClientStream? pipe = null;

        try
        {
            pipe = new NamedPipeClientStream(
                serverName: ".",
                pipeName: _pipeName,
                direction: PipeDirection.Out,
                options: PipeOptions.Asynchronous);

            await pipe.ConnectAsync(ConnectTimeoutMs, ct).ConfigureAwait(false);

            var bytes = Utf8NoBom.GetBytes(_clientId);

            await pipe.WriteAsync(bytes, ct).ConfigureAwait(false);
            await pipe.FlushAsync(ct).ConfigureAwait(false);

            LogConnected(_logger, _clientId, _pipeName);

            // Hold the socket open for the host's lifetime. The
            // server's liveness check is "is at least one open
            // connection bound to this client id"; closing this
            // socket would flip the host's state to "not running"
            // while the process is still very much alive.
            await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // Health pipe is best-effort. Log once and stay silent.
            LogConnectFailed(_logger, _clientId, _pipeName, ex);
        }
        finally
        {
            if (pipe is not null)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Health-monitor pipe not configured; skipping liveness signal.")]
    private static partial void LogSkippingNoPipe(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Connected to health-monitor pipe '{PipeName}' as client '{ClientId}'.")]
    private static partial void LogConnected(ILogger logger, string clientId, string pipeName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Failed to connect to health-monitor pipe '{PipeName}' as client '{ClientId}'.")]
    private static partial void LogConnectFailed(ILogger logger, string clientId, string pipeName, Exception ex);
}
