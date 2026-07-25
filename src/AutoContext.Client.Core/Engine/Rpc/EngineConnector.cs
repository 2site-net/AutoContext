namespace AutoContext.Client.Core.Engine.Rpc;

using System.Diagnostics;
using System.IO.Pipes;

using AutoContext.Client.Core.Engine;
using AutoContext.Engine.Protocol;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// The find-or-spawn resolver: turns a <see cref="ClientOptions"/> and
/// an <see cref="EndpointKind"/> into a live <see cref="EngineConnection"/>.
/// It dials the derived endpoint once (warm), and on failure — unless
/// spawning is disabled — asks the <see cref="IEngineSpawner"/> to start
/// an engine, then retries with exponential backoff against the cold
/// budget. Endpoints that require the <c>Engine.Hello</c> handshake
/// (<c>rpc</c>, <c>events</c>) are handshaked before the connection is
/// returned; the passive <c>health</c> and <c>logs</c> endpoints are
/// returned raw.
/// </summary>
public sealed partial class EngineConnector
{
    private readonly EngineConnectBudget _budget;
    private readonly ILogger<EngineConnector> _logger;
    private readonly IOptions<ClientOptions> _options;
    private readonly IEngineSpawner _spawner;
    private readonly PipeTransport _transport;

    /// <summary>
    /// Creates a new <see cref="EngineConnector"/>. All arguments are
    /// required.
    /// </summary>
    public EngineConnector(
        IOptions<ClientOptions> options,
        PipeTransport transport,
        IEngineSpawner spawner,
        EngineConnectBudget budget,
        ILogger<EngineConnector> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(spawner);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _transport = transport;
        _spawner = spawner;
        _budget = budget;
        _logger = logger;
    }

    /// <summary>
    /// Resolves a connection to the <paramref name="kind"/> endpoint of
    /// the engine identified by the configured workspace and instance
    /// id, spawning one if none is listening and spawning is allowed.
    /// </summary>
    /// <param name="kind">Which of the engine's four endpoints to
    /// dial.</param>
    /// <param name="cancellationToken">Cancellation for the whole
    /// find-or-spawn flow.</param>
    /// <returns>A live connection; handshaked for <c>rpc</c> and
    /// <c>events</c>.</returns>
    /// <exception cref="EngineUnavailableException">No engine was
    /// listening and either spawning is disabled or a spawned engine
    /// did not begin accepting within the cold budget.</exception>
    /// <exception cref="EngineProtocolException">An engine answered but
    /// reported an incompatible protocol version.</exception>
    public async Task<EngineConnection> ConnectAsync(
        EndpointKind kind, CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var address = ResolveAddress(kind, options);

        var warm = await TryOpenAsync(address, kind, _budget.WarmConnectTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (warm is not null)
        {
            return warm;
        }

        if (options.SpawnDisabled)
        {
            throw new EngineUnavailableException(
                $"No engine is listening on '{address}' and spawning is disabled.");
        }

        var binaryPath = EngineLocator.Resolve(options.EngineBinaryPath);
        var request = new EngineSpawnRequest(
            options.WorkspacePath,
            options.InstanceId,
            options.InstanceLabel,
            options.IdleTimeout,
            binaryPath);
        await _spawner.SpawnAsync(request, cancellationToken).ConfigureAwait(false);
        LogSpawnRequested(_logger, address);

        var cold = await RetryOpenAsync(address, kind, cancellationToken).ConfigureAwait(false);
        if (cold is not null)
        {
            return cold;
        }

        throw new EngineUnavailableException(
            $"Spawned an engine but it did not begin accepting connections on '{address}' within "
            + $"{_budget.ColdConnectBudget.TotalSeconds:0}s.");
    }

    private static bool RequiresHandshake(EndpointKind kind)
        => kind is EndpointKind.Rpc or EndpointKind.Events;

    private static string ResolveAddress(EndpointKind kind, ClientOptions options)
    {
        var workspaceHash = WorkspaceHash.Compute(options.WorkspacePath);
        return new Endpoint(kind, workspaceHash.Value, options.InstanceId).ToString();
    }

    private async Task<EngineConnection?> RetryOpenAsync(
        string address, EndpointKind kind, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        var delay = TimeSpan.Zero;

        while (Stopwatch.GetElapsedTime(start) < _budget.ColdConnectBudget)
        {
            delay = _budget.NextRetryDelay(delay);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

            var connection = await TryOpenAsync(
                    address, kind, _budget.ColdConnectAttemptTimeout, cancellationToken)
                .ConfigureAwait(false);
            if (connection is not null)
            {
                return connection;
            }
        }

        return null;
    }

    private async Task<EngineConnection?> TryOpenAsync(
        string address, EndpointKind kind, TimeSpan connectTimeout, CancellationToken cancellationToken)
    {
        EngineConnection? connection = null;
        try
        {
            var stream = await _transport
                .ConnectAsync(
                    address,
                    (int)connectTimeout.TotalMilliseconds,
                    PipeDirection.InOut,
                    cancellationToken)
                .ConfigureAwait(false);

            connection = new EngineConnection(stream);

            if (RequiresHandshake(kind))
            {
                await connection.HandshakeAsync(cancellationToken).ConfigureAwait(false);
            }

            var opened = connection;
            connection = null;
            return opened;
        }
        catch (EngineProtocolException)
        {
            // A protocol mismatch means an engine *was* reached; surface
            // it rather than treating the endpoint as absent and retrying.
            if (connection is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            if (connection is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            LogConnectAttemptFailed(_logger, address, ex);
            return null;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Requested an engine spawn for endpoint '{Address}'.")]
    private static partial void LogSpawnRequested(ILogger logger, string address);

    [LoggerMessage(EventId = 2, Level = LogLevel.Trace,
        Message = "Connect attempt to '{Address}' failed; treating the engine as absent.")]
    private static partial void LogConnectAttemptFailed(ILogger logger, string address, Exception exception);
}
