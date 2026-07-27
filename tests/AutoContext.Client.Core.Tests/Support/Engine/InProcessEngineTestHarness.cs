namespace AutoContext.Client.Core.Tests.Support.Engine;

using AutoContext.Client.Core;
using AutoContext.Client.Core.Engine;
using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Engine.Core;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Engine.Tests.Support.Mcp;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// A live engine composed in-process through
/// <see cref="EngineHostBuilderExtensions.AddAutoContextEngine"/>, bound to a
/// throwaway workspace and cache root, paired with a real
/// <see cref="EngineConnector"/> that dials it over the same named pipes the
/// shipped client uses. This is the conformance seam for the round-trip tests:
/// nothing between the typed client and the engine handlers is faked, so a
/// marshalling mistake on either side surfaces as a failing assertion rather
/// than agreeing fakes.
/// </summary>
/// <remarks>
/// Spawning is disabled on the client so a dial failure fails the test
/// immediately instead of launching a stray engine process. Disposal is
/// idempotent, stops the host under a deadline before deleting the temporary
/// directories it writes into, and a failed startup disposes everything already
/// created rather than orphaning it.
/// </remarks>
internal sealed class InProcessEngineTestHarness : IAsyncDisposable
{
    /// <summary>Upper bound on the graceful host stop before disposal forces through.</summary>
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);

    private readonly TempDirectory _cacheRoot;
    private readonly IHost _host;
    private readonly TempDirectory? _resourcesOverlay;
    private readonly TempDirectory _workspace;
    private int _disposed;

    private InProcessEngineTestHarness(
        IHost host,
        TempDirectory workspace,
        TempDirectory cacheRoot,
        TempDirectory? resourcesOverlay,
        EngineConnector connector,
        Guid instanceId)
    {
        _host = host;
        _workspace = workspace;
        _cacheRoot = cacheRoot;
        _resourcesOverlay = resourcesOverlay;
        Connector = connector;
        InstanceId = instanceId;
    }

    /// <summary>Find-or-spawn resolver aimed at this engine, with spawning disabled.</summary>
    public EngineConnector Connector { get; }

    /// <summary>The engine's launcher instance id — the endpoint's instance segment.</summary>
    public Guid InstanceId { get; }

    /// <summary>Absolute path of the throwaway workspace the engine was pointed at.</summary>
    public string WorkspacePath => _workspace.Path;

    /// <summary>
    /// Stands an engine up and waits for it to finish binding its endpoints.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for the startup.</param>
    /// <param name="withTestDriverWorker">When <see langword="true"/>, overlays a
    /// resources tree whose tool registry routes to the stand-in test-driver
    /// worker, so a tool invocation dispatches to a real worker process.</param>
    public static async Task<InProcessEngineTestHarness> StartAsync(
        CancellationToken cancellationToken, bool withTestDriverWorker = false)
    {
        var workspace = TempDirectory.CreateNew("ac-client-roundtrip-workspace");
        var cacheRoot = TempDirectory.CreateNew("ac-client-roundtrip-cache");
        var resourcesOverlay = withTestDriverWorker ? TestDriverResourcesOverlay.Create() : null;
        var instanceId = Guid.NewGuid();

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.AddAutoContextEngine(options =>
        {
            options.WorkspacePath = workspace.Path;
            options.InstanceId = instanceId;
            options.CacheRootOverride = cacheRoot.Path;
            options.ResourcesRootOverride = resourcesOverlay?.Path;
            options.IdleTimeout = TimeSpan.Zero;
        });

        var host = builder.Build();
        var connector = ConnectorTestHarness.CreateConnector(
            new ClientOptions
            {
                WorkspacePath = workspace.Path,
                InstanceId = instanceId,
                SpawnDisabled = true,
            },
            new FakeEngineSpawner());

        var harness = new InProcessEngineTestHarness(
            host, workspace, cacheRoot, resourcesOverlay, connector, instanceId);

        try
        {
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await harness.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return harness;
    }

    /// <summary>Opens a typed client over a fresh handshaked connection.</summary>
    public Task<EngineClient> ConnectAsync(CancellationToken cancellationToken)
        => EngineClient.ConnectAsync(Connector, cancellationToken);

    /// <summary>
    /// Dials the engine and drops the connection, running one
    /// <c>Engine.Hello</c> handshake.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for the dial.</param>
    public async Task HandshakeAsync(CancellationToken cancellationToken)
    {
        var connection = await Connector
            .ConnectAsync(EndpointKind.Rpc, cancellationToken)
            .ConfigureAwait(false);

        await connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        using var stop = new CancellationTokenSource(StopTimeout);

        try
        {
            await _host.StopAsync(stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stop deadline elapsed; the host and temp trees are still released below.
        }

        _host.Dispose();

        _resourcesOverlay?.Dispose();
        _cacheRoot.Dispose();
        _workspace.Dispose();
    }
}
