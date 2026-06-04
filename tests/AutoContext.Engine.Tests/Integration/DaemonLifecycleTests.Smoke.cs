namespace AutoContext.Engine.Tests.Integration;

using System.Globalization;
using System.Text.Json;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.Diagnostics;
using AutoContext.Engine.Tests.Support.Pipes;
using AutoContext.Framework.Pipes;

using Xunit.Sdk;

/// <summary>
/// Integration coverage for the <c>autocontext-engine</c> binary in
/// daemon role — spawns the published executable, dials each of the
/// four pipes to confirm the atomic bind, completes the
/// <c>Engine.Hello</c> handshake on <c>rpc</c>, and confirms that
/// <c>Engine.Shutdown</c> drains the host cleanly and produces a
/// zero exit code.
/// </summary>
/// <remarks>
/// Phase 1 (row 15) of the engine implementation plan stands this
/// harness up; subsequent phases extend it with workers and
/// per-pipe payload assertions. Gated with the repository's
/// <c>Category=Smoke</c> trait so it runs under
/// <c>.\build.ps1 Compile -Smoke DotNet</c> and stays out of the
/// default unit-test pass.
/// </remarks>
[Trait("Category", "Smoke")]
public sealed class DaemonLifecycleTests
{
    [Fact]
    public async Task Should_bind_pipes_complete_handshake_and_exit_cleanly_on_shutdown()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        await using var engine = new EngineTestProcess();
        await engine.SpawnAsync(ct);

        try
        {
            // Act — every endpoint must be reachable once the engine
            // has signalled readiness via the rpc-pipe probe.
            EndpointKind[] kinds =
            [
                EndpointKind.Rpc,
                EndpointKind.Events,
                EndpointKind.Health,
                EndpointKind.Logs,
            ];

            foreach (var kind in kinds)
            {
                var probe = await EngineWireTestClient.ConnectAsync(kind, engine, ct);
                await using var _ = probe.ConfigureAwait(false);
                Assert.True(
                    probe.IsConnected,
                    $"Expected the engine's '{kind}' pipe to be connected after SpawnAsync returned.");
            }

            // Act — complete the Engine.Hello handshake on rpc.
            var rpc = await EngineWireTestClient.ConnectAsync(EndpointKind.Rpc, engine, ct);
            await using var rpcDisposer = rpc.ConfigureAwait(false);
            var codec = new LengthPrefixedFrameCodec(rpc);

            await EngineWireTestClient.SendHelloAsync(codec, ProtocolVersion.Current, ct);
            var helloResponse = await EngineWireTestClient.ReadResponseAsync(codec, ct);

            // Act — request shutdown and read the acknowledgement.
            await EngineWireTestClient.SendRequestAsync(
                codec, id: 42, method: ProtocolMethods.Shutdown, ct);
            var shutdownResponse = await EngineWireTestClient.ReadResponseAsync(codec, ct);
            var shutdownResult = shutdownResponse.Result!.Value.Deserialize(
                ProtocolJsonContext.Default.JsonShutdownResult);

            // Wait for the binary to drain and exit on its own.
            await engine.Process
                .WaitForExitAsync(ct)
                .WaitAsync(TimeSpan.FromSeconds(10), ct);

            // Assert
            Assert.Multiple(
                () => Assert.Null(helloResponse.Error),
                () => Assert.Null(shutdownResponse.Error),
                () => Assert.Equal(42, shutdownResponse.Id.GetInt32()),
                () => Assert.NotNull(shutdownResult),
                () => Assert.True(shutdownResult!.Accepted),
                () => Assert.Equal(0, engine.Process.ExitCode));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var exitCode = engine.Process.HasExited
                ? engine.Process.ExitCode.ToString(CultureInfo.InvariantCulture)
                : "<running>";
            var stderr = string.Join(Environment.NewLine, engine.StandardErrorLines);

            throw new XunitException(
                $"Scenario failed. Engine HasExited={engine.Process.HasExited}, ExitCode={exitCode}."
                + $"{Environment.NewLine}Stderr:{Environment.NewLine}{(stderr.Length == 0 ? "(no stderr)" : stderr)}",
                ex);
        }
    }
}
