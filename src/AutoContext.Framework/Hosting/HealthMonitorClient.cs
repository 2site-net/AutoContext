namespace AutoContext.Framework.Hosting;

using System.Text;

using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
/// Composed over <see cref="PipeKeepAliveClient"/>. The connect runs
/// off the host's <c>StartAsync</c> path so a missing extension does
/// not delay host startup — failures are logged and swallowed.
/// </para>
/// </remarks>
public sealed class HealthMonitorClient : IHostedService, IAsyncDisposable
{
    private const int ConnectTimeoutMs = 2000;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _pipeName;
    private readonly string _clientId;
    private readonly PipeKeepAliveClient _keepAlive;
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
        _keepAlive = new PipeKeepAliveClient(
            new PipeTransport(NullLogger<PipeTransport>.Instance),
            NullLogger<PipeKeepAliveClient>.Instance);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_pipeName))
        {
            return Task.CompletedTask;
        }

        var handshake = Utf8NoBom.GetBytes(_clientId);
        _runTask = Task.Run(
            () => _keepAlive.StartAsync(_pipeName, handshake, ConnectTimeoutMs, _cts.Token),
            cancellationToken);

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

        await _keepAlive.DisposeAsync().ConfigureAwait(false);

        if (_runTask is null || _runTask.IsCompleted)
        {
            _cts.Dispose();
        }
    }
}
