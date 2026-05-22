namespace AutoContext.Engine.Tests.Integration;

using System.Globalization;
using System.Text.Json;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.Integration;
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
public sealed class EngineSpawnTests
{
    [Fact]
    public async Task Should_bind_each_pipe_complete_handshake_and_exit_zero_on_shutdown()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var workspacePath = CreateTempWorkspace();
        var instanceId = Guid.NewGuid();

        await using var engine = await EngineTestProcess.StartAsync(
            workspacePath, instanceId, ct);

        try
        {
            await RunScenarioAsync(engine, workspacePath, instanceId, ct);
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

    private static async Task RunScenarioAsync(
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
            await using var probe = await EngineWireTestClient.ConnectAsync(
                kind, workspacePath, instanceId, ct);
            Assert.True(
                probe.IsConnected,
                $"Expected the engine's '{kind}' pipe to be connected after StartAsync returned.");
        }

        // Act — complete the Engine.Hello handshake on rpc.
        await using var rpc = await EngineWireTestClient.ConnectAsync(
            EndpointKind.Rpc, workspacePath, instanceId, ct);
        var codec = new LengthPrefixedFrameCodec(rpc);

        await EngineWireTestClient.SendHelloAsync(codec, ProtocolVersion.Current, ct);
        var helloResponse = await EngineWireTestClient.ReadResponseAsync(codec, ct);

        // Act — request shutdown and read the acknowledgement.
        await EngineWireTestClient.SendRequestAsync(
            codec, id: 42, method: ProtocolMethods.Shutdown, ct);
        var shutdownResponse = await EngineWireTestClient.ReadResponseAsync(codec, ct);
        var shutdownResult = shutdownResponse.Result!.Value.Deserialize(
            ProtocolJsonContext.Default.ShutdownResult);

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

    private static string CreateTempWorkspace()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "autocontext-engine-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
