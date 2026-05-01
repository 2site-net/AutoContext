namespace AutoContext.Mcp.Server.Config;

using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Framework.Workers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>
/// Hosted service that subscribes to the extension-side
/// <c>AutoContextConfigServer</c> push channel and keeps
/// <see cref="AutoContextConfigSnapshot"/> in sync with
/// <c>.autocontext.json</c>. On every snapshot change the client
/// emits an MCP <c>notifications/tools/list_changed</c> notification
/// so VS Code's Quick Pick refreshes without a server restart.
/// </summary>
/// <remarks>
/// Wire format: 4-byte little-endian payload length followed by that
/// many UTF-8 JSON bytes — the same framing used by
/// <see cref="WorkerProtocolChannel"/>. The first frame is the
/// handshake; every subsequent frame replaces the snapshot wholesale.
/// <para>
/// When constructed without a pipe name (standalone runs of
/// <c>Mcp.Server</c>, smoke tests not driven by the extension) the
/// service is a no-op so callers can wire it unconditionally.
/// </para>
/// <para>
/// Failures on the channel (connect refused, EOF, parse error) tear
/// down the connection but do not crash the host: tool registration
/// continues to work against the last known snapshot (or the empty
/// default when no snapshot was ever received).
/// </para>
/// </remarks>
public sealed partial class AutoContextConfigClient : IHostedService, IAsyncDisposable
{
    private const int ConnectTimeoutMs = 5000;

    private readonly string _pipeName;
    private readonly AutoContextConfigSnapshot _snapshot;
    private readonly IServiceProvider _services;
    private readonly ILogger<AutoContextConfigClient> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _runTask;

    public AutoContextConfigClient(
        string pipeName,
        AutoContextConfigSnapshot snapshot,
        IServiceProvider services,
        ILogger<AutoContextConfigClient> logger)
    {
        ArgumentNullException.ThrowIfNull(pipeName);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        _pipeName = pipeName;
        _snapshot = snapshot;
        _services = services;
        _logger = logger;

        _snapshot.Changed += OnSnapshotChanged;
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
        _snapshot.Changed -= OnSnapshotChanged;

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

        _cts.Dispose();
    }

    [SuppressMessage("Reliability", "CA2000",
        Justification = "The pipe is disposed via the using statement on every exit path.")]
    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                serverName: ".",
                pipeName: _pipeName,
                direction: PipeDirection.In,
                options: PipeOptions.Asynchronous);

            await pipe.ConnectAsync(ConnectTimeoutMs, ct).ConfigureAwait(false);

            LogConnected(_logger, _pipeName);

            var channel = new WorkerProtocolChannel(pipe);

            while (!ct.IsCancellationRequested)
            {
                var payload = await channel.ReadAsync(ct).ConfigureAwait(false);

                if (payload is null)
                {
                    LogPipeClosed(_logger, _pipeName);
                    return;
                }

                if (payload.Length == 0)
                {
                    continue;
                }

                AutoContextConfigSnapshotDto? dto;
                try
                {
                    dto = JsonSerializer.Deserialize<AutoContextConfigSnapshotDto>(payload);
                }
                catch (JsonException ex)
                {
                    LogParseFailed(_logger, ex);
                    continue;
                }

                if (dto is null)
                {
                    continue;
                }

                _snapshot.Update(dto);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            LogChannelFailed(_logger, _pipeName, ex);
        }
    }

    private void OnSnapshotChanged(object? sender, EventArgs e)
    {
        // Fire-and-forget: best-effort notification. The MCP server
        // singleton is only resolvable after the host finishes
        // building, and may not be present at all in unit tests that
        // exercise the snapshot path directly.
        var server = _services.GetService<McpServer>();
        if (server is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await server.SendNotificationAsync(
                    NotificationMethods.ToolListChangedNotification,
                    _cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException or IOException)
            {
                LogNotifyFailed(_logger, ex);
            }
        });
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Extension-config pipe not configured; skipping config subscription.")]
    private static partial void LogSkippingNoPipe(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Connected to extension-config pipe '{PipeName}'.")]
    private static partial void LogConnected(ILogger logger, string pipeName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Extension-config pipe '{PipeName}' closed by the extension.")]
    private static partial void LogPipeClosed(ILogger logger, string pipeName);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "Failed to parse extension-config snapshot frame.")]
    private static partial void LogParseFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning,
        Message = "Extension-config channel '{PipeName}' failed.")]
    private static partial void LogChannelFailed(ILogger logger, string pipeName, Exception ex);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug,
        Message = "Failed to send tools/list_changed notification.")]
    private static partial void LogNotifyFailed(ILogger logger, Exception ex);
}
