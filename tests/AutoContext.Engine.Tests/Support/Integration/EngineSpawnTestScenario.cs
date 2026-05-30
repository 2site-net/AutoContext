namespace AutoContext.Engine.Tests.Support.Integration;

using System.Text.Json;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

internal static class EngineSpawnTestScenario
{
    public static async Task RunAsync(
        EngineTestProcess engine,
        string workspacePath,
        Guid instanceId,
        CancellationToken ct)
    {
        // Act — every endpoint must be reachable once the engine has
        // signalled readiness via the rpc-pipe probe.
        var kinds = new[]
        {
            EndpointKind.Rpc,
            EndpointKind.Events,
            EndpointKind.Health,
            EndpointKind.Logs,
        };

        foreach (var kind in kinds)
        {
            var probe = await EngineWireTestClient.ConnectAsync(
                kind, workspacePath, instanceId, ct).ConfigureAwait(false);
            await using var _ = probe.ConfigureAwait(false);
            Assert.True(
                probe.IsConnected,
                $"Expected the engine's '{kind}' pipe to be connected after StartAsync returned.");
        }

        // Act — complete the Engine.Hello handshake on rpc.
        var rpc = await EngineWireTestClient.ConnectAsync(
            EndpointKind.Rpc, workspacePath, instanceId, ct).ConfigureAwait(false);
        await using var rpcDisposer = rpc.ConfigureAwait(false);
        var codec = new LengthPrefixedFrameCodec(rpc);

        await EngineWireTestClient.SendHelloAsync(codec, ProtocolVersion.Current, ct).ConfigureAwait(false);
        var helloResponse = await EngineWireTestClient.ReadResponseAsync(codec, ct).ConfigureAwait(false);

        // Act — request shutdown and read the acknowledgement.
        await EngineWireTestClient.SendRequestAsync(
            codec, id: 42, method: ProtocolMethods.Shutdown, ct).ConfigureAwait(false);
        var shutdownResponse = await EngineWireTestClient.ReadResponseAsync(codec, ct).ConfigureAwait(false);
        var shutdownResult = shutdownResponse.Result!.Value.Deserialize(
            ProtocolJsonContext.Default.JsonShutdownResult);

        // Wait for the binary to drain and exit on its own.
        await engine.Process
            .WaitForExitAsync(ct)
            .WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);

        // Assert
        Assert.Multiple(
            () => Assert.Null(helloResponse.Error),
            () => Assert.Null(shutdownResponse.Error),
            () => Assert.Equal(42, shutdownResponse.Id.GetInt32()),
            () => Assert.NotNull(shutdownResult),
            () => Assert.True(shutdownResult!.Accepted),
            () => Assert.Equal(0, engine.Process.ExitCode));
    }
}
