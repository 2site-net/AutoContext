namespace AutoContext.Client.Core.Tests.Support.Engine.Rpc;

using AutoContext.Engine.Protocol;

/// <summary>
/// Fake spawner that stands a <see cref="FakeEnginePipeServer"/> up on the
/// address derived from each spawn request and owns its disposal, so
/// cold-start tests can drive the connector's retry loop without managing
/// server lifetime by hand.
/// </summary>
internal sealed class ColdStartEngineTestHarness : IAsyncDisposable
{
    private readonly List<FakeEnginePipeServer> _servers = [];

    public ColdStartEngineTestHarness()
    {
        Spawner = new FakeEngineSpawner((request, _) =>
        {
            var address = ConnectorTestHarness.Address(
                EndpointKind.Rpc, request.WorkspacePath, request.InstanceId);

            _servers.Add(new FakeEnginePipeServer(address, ProtocolVersion.Current));

            return Task.CompletedTask;
        });
    }

    public FakeEngineSpawner Spawner { get; }

    public async ValueTask DisposeAsync()
    {
        foreach (var server in _servers)
        {
            await server.DisposeAsync().ConfigureAwait(false);
        }

        _servers.Clear();
    }
}
