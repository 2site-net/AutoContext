namespace AutoContext.Client.Core.Engine;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Engine.Subscriptions;
using AutoContext.Engine.Protocol;

/// <summary>
/// The engine's typed RPC surface for one launcher instance. Holds a
/// single shared, handshaked <c>rpc</c> connection that the unary
/// per-family clients dial through, and exposes subscription consumers
/// that each open their own dedicated connection so a long-lived stream
/// never interleaves with unary calls. Acquire one via
/// <see cref="ConnectAsync"/> and dispose it when done; the underlying
/// connection is released on <see cref="DisposeAsync"/>.
/// </summary>
public sealed class EngineClient : IAsyncDisposable
{
    private readonly EngineConnector _connector;
    private readonly EngineConnection _rpcConnection;

    private EngineClient(EngineConnector connector, EngineConnection rpcConnection)
    {
        _connector = connector;
        _rpcConnection = rpcConnection;
        Agent = new AgentRpcClient(rpcConnection);
        Config = new ConfigRpcClient(rpcConnection);
        Discovery = new DiscoveryRpcClient(rpcConnection);
        Engine = new EngineRpcClient(rpcConnection);
        Instructions = new InstructionsRpcClient(rpcConnection);
        Logs = new LogsRpcClient(rpcConnection);
        McpTools = new McpToolsRpcClient(rpcConnection);
        Workspace = new WorkspaceRpcClient(rpcConnection);
    }

    /// <summary>The <c>Agent.*</c> family — the fire-and-forget
    /// agent-loop notifications.</summary>
    public AgentRpcClient Agent { get; }

    /// <summary>The <c>Config.*</c> family — snapshot read and
    /// per-file / per-rule toggles.</summary>
    public ConfigRpcClient Config { get; }

    /// <summary>The <c>Discovery.*</c> family — prompt and tool
    /// routing.</summary>
    public DiscoveryRpcClient Discovery { get; }

    /// <summary>The <c>Engine.*</c> family — shutdown and the liveness
    /// registry read.</summary>
    public EngineRpcClient Engine { get; }

    /// <summary>The <c>Instructions.*</c> family — corpus listing,
    /// reads, and search.</summary>
    public InstructionsRpcClient Instructions { get; }

    /// <summary>The <c>Logs.*</c> family — bounded engine and worker
    /// log reads.</summary>
    public LogsRpcClient Logs { get; }

    /// <summary>The <c>McpTools.*</c> family — tool catalog listing and
    /// invocation.</summary>
    public McpToolsRpcClient McpTools { get; }

    /// <summary>The <c>Workspace.*</c> family — detection and
    /// engine-process info.</summary>
    public WorkspaceRpcClient Workspace { get; }

    /// <summary>
    /// Resolves an engine for the configured workspace and instance,
    /// establishes the shared <c>rpc</c> connection, and returns a
    /// ready client. Spawns an engine on cold start when the spawn
    /// policy allows it.
    /// </summary>
    /// <param name="connector">Find-or-spawn resolver. Must not be
    /// <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation for the connect.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connector"/> is <see langword="null"/>.</exception>
    public static async Task<EngineClient> ConnectAsync(
        EngineConnector connector, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connector);

        var rpcConnection = await connector
            .ConnectAsync(EndpointKind.Rpc, cancellationToken)
            .ConfigureAwait(false);

        return new EngineClient(connector, rpcConnection);
    }

    /// <summary>
    /// Returns a consumer for the <c>Agent.Events.Subscribe</c> stream.
    /// Each enumeration opens its own dedicated connection, independent
    /// of this client's shared unary connection.
    /// </summary>
    public AgentEventsSubscription AgentEvents()
        => new(_connector);

    /// <summary>
    /// Returns a consumer for the <c>Config.Subscribe</c> stream. Each
    /// enumeration opens its own dedicated connection, independent of
    /// this client's shared unary connection.
    /// </summary>
    public ConfigSubscription ConfigChanges()
        => new(_connector);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
        => _rpcConnection.DisposeAsync();

    /// <summary>
    /// Returns a consumer for the <c>Instructions.Subscribe</c> stream.
    /// Each enumeration opens its own dedicated connection, independent
    /// of this client's shared unary connection.
    /// </summary>
    public InstructionsSubscription InstructionsChanges()
        => new(_connector);

    /// <summary>
    /// Returns a consumer for the <c>Engine.Lifecycle</c> broadcast on
    /// the <c>events</c> pipe. Each enumeration opens its own dedicated
    /// connection.
    /// </summary>
    public EngineLifecycleSubscription LifecycleEvents()
        => new(_connector);

    /// <summary>
    /// Returns a consumer for the <c>Logs.TailEngine</c> stream. Each
    /// enumeration opens its own dedicated connection, independent of
    /// this client's shared unary connection.
    /// </summary>
    public LogsTailSubscription LogsTail()
        => new(_connector);
}
