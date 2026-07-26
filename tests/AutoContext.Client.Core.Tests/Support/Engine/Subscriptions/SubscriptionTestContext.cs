namespace AutoContext.Client.Core.Tests.Support.Engine.Subscriptions;

using System.Diagnostics.CodeAnalysis;

using AutoContext.Client.Core;
using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Engine;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Engine.Protocol;

/// <summary>
/// Stands a scripted engine pipe up at a spawn-disabled endpoint and
/// wires a real <see cref="EngineConnector"/> to it, so a subscription
/// consumer can be exercised end-to-end through the find-or-spawn
/// resolver against a caller-supplied server script. Dispose it to tear
/// the scripted server down.
/// </summary>
internal sealed class SubscriptionTestContext : IAsyncDisposable
{
    private readonly ScriptedEnginePipeServer _server;

    private SubscriptionTestContext(ScriptedEnginePipeServer server, EngineConnector connector)
    {
        _server = server;
        Connector = connector;
    }

    /// <summary>The resolver the subscription consumer dials through.</summary>
    public EngineConnector Connector { get; }

    /// <summary>Scripts the engine's <c>rpc</c> endpoint (subscribe stream).</summary>
    public static SubscriptionTestContext ForRpc(Func<ScriptedPeer, CancellationToken, Task> script)
        => Create(EndpointKind.Rpc, script);

    /// <summary>Scripts the engine's <c>events</c> endpoint (notification push).</summary>
    public static SubscriptionTestContext ForEvents(Func<ScriptedPeer, CancellationToken, Task> script)
        => Create(EndpointKind.Events, script);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
        => _server.DisposeAsync();

    [SuppressMessage("Reliability", "CA2000",
        Justification = "Ownership of the server transfers to the returned context, whose DisposeAsync disposes it under the test's await using.")]
    private static SubscriptionTestContext Create(
        EndpointKind kind, Func<ScriptedPeer, CancellationToken, Task> script)
    {
        var workspacePath = ConnectorTestHarness.NewWorkspacePath();
        var instanceId = Guid.NewGuid();
        var address = ConnectorTestHarness.Address(kind, workspacePath, instanceId);
        var server = new ScriptedEnginePipeServer(address, ProtocolVersion.Current, script);
        var connector = ConnectorTestHarness.CreateConnector(
            new ClientOptions
            {
                WorkspacePath = workspacePath,
                InstanceId = instanceId,
                SpawnDisabled = true,
            },
            new FakeEngineSpawner());

        return new SubscriptionTestContext(server, connector);
    }
}
