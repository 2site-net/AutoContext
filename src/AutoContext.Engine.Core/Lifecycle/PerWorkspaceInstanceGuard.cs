namespace AutoContext.Engine.Core.Lifecycle;

using System.IO.Pipes;

using AutoContext.Engine.Core.Infrastructure;
using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Protocol;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Production <see cref="IUniqueInstanceGuard"/> that scopes
/// uniqueness per workspace: probes the would-be <c>rpc</c>
/// endpoint name derived from
/// <c>WorkspaceHash(EngineOptions.WorkspacePath)</c> ×
/// <c>EngineOptions.InstanceId</c> and throws when a peer answers.
/// </summary>
/// <remarks>
/// <para>
/// The guard probes only the <c>rpc</c> endpoint. The four engine
/// pipes (<c>rpc</c>, <c>events</c>, <c>health</c>, <c>logs</c>)
/// are bound atomically by <see cref="LifecycleService"/>, so a
/// live peer at the <c>rpc</c> name necessarily implies the other
/// three are also occupied.
/// </para>
/// <para>
/// The probe is best-effort: <see cref="TimeoutException"/> (no
/// listener within the probe window) and
/// <see cref="System.IO.IOException"/> from the connect itself
/// (no pipe at this name) are both treated as "address is free".
/// <see cref="UnauthorizedAccessException"/> from an ACL denial
/// makes the probe inconclusive; the guard logs a warning and
/// returns so the actual bind in <see cref="LifecycleService"/>
/// runs as the authoritative check.
/// </para>
/// <para>
/// A TOCTOU window exists between this probe and the real bind:
/// a peer can race up after the probe clears. That race is still
/// caught by <see cref="LifecycleService"/>'s bind, which fails
/// the host with the underlying OS error. The guard's value is
/// turning the common-case collision (a peer is already alive
/// when this engine starts) into a clear, actionable diagnostic
/// instead of an opaque pipe-bind error.
/// </para>
/// </remarks>
internal sealed partial class PerWorkspaceInstanceGuard : IUniqueInstanceGuard
{
    /// <summary>
    /// Connect timeout for the probe, in milliseconds. Sized
    /// short enough that a clear startup pays a sub-100 ms tax
    /// in the common no-peer case, and long enough that a
    /// genuinely-live peer answers well within the window.
    /// </summary>
    internal const int ProbeConnectTimeoutMs = 100;

    private readonly ILogger<PerWorkspaceInstanceGuard> _logger;
    private readonly EngineOptions _options;
    private readonly PipeTransport _transport;

    /// <summary>
    /// Creates a new <see cref="PerWorkspaceInstanceGuard"/>.
    /// </summary>
    /// <param name="options">Engine options carrying the
    /// workspace path and instance id the probe composes its
    /// target endpoint name from.</param>
    /// <param name="transport">Connect primitive used to dial
    /// the probe target. The guard does not retain the connected
    /// stream beyond the probe.</param>
    /// <param name="logger">Diagnostic sink for collision /
    /// inconclusive-probe transitions in the
    /// <c>engine.lifecycle</c> log category.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public PerWorkspaceInstanceGuard(
        IOptions<EngineOptions> options,
        PipeTransport transport,
        ILogger<PerWorkspaceInstanceGuard> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _transport = transport;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task EnsureUniqueAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var workspaceHash = WorkspaceHash.Compute(_options.WorkspacePath);
        var endpoint = new Endpoint(EndpointKind.Rpc, workspaceHash.Value, _options.InstanceId);
        var pipeName = endpoint.ToString();

        Stream probe;
        try
        {
            probe = await _transport
                .ConnectAsync(pipeName, ProbeConnectTimeoutMs, PipeDirection.In, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // No listener answered within the probe window.
            // Address is free; let the bind proceed.
            return;
        }
        catch (IOException)
        {
            // Connect failed because no pipe exists at this name.
            // Address is free.
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            // ACL denial: a pipe exists but we cannot dial it.
            // Probe is inconclusive; warn and let the bind run
            // as the authoritative check.
            LogProbeInconclusive(_logger, pipeName, ex);
            return;
        }

        // A live peer answered. Dispose the probe stream eagerly
        // so the peer sees EOF promptly, then surface the
        // launcher-bug diagnostic.
        await probe.DisposeAsync().ConfigureAwait(false);

        LogCollisionDetected(_logger, workspaceHash, _options.InstanceId, pipeName);

        throw new IOException(
            $"Detected a live engine already bound to '{pipeName}' (workspace '{workspaceHash}', instance {_options.InstanceId:D}). "
            + "Per the per-launch-UUID contract (P4) every launcher spawn must mint a fresh --instance-id; reusing an id while another engine is alive is a launcher bug.");
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Critical,
        Message = "Detected duplicate engine at endpoint '{PipeName}' for workspace '{WorkspaceHash}' instance {InstanceId:D}; this is a launcher bug under the per-launch-UUID contract (P4).")]
    private static partial void LogCollisionDetected(
        ILogger logger,
        WorkspaceHash workspaceHash,
        Guid instanceId,
        string pipeName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Unique-instance probe for '{PipeName}' was inconclusive (ACL or transport denial); proceeding to bind as the authoritative check.")]
    private static partial void LogProbeInconclusive(
        ILogger logger,
        string pipeName,
        Exception exception);
}
