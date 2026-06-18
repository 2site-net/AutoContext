namespace AutoContext.Engine.Tests.Integration;

using System.Text.Json;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages.Workspace;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.Diagnostics;
using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Engine.Tests.Support.Pipes;
using AutoContext.Framework.Pipes;

/// <summary>
/// End-to-end coverage for the <c>Workspace.*</c> RPC family
/// (Phase 4 row 9b). Spawns the <c>autocontext-engine</c> binary
/// against a populated <c>--workspace</c> and exercises
/// <c>Workspace.Detect</c> and <c>Workspace.Info</c> over the
/// <c>rpc</c> pipe, asserting the serialised wire shape across a real
/// process boundary — the cross-process companion to the in-process
/// <c>DispatchPolicy</c> handler tests.
/// </summary>
/// <remarks>
/// <para>
/// The engine runs its full workspace scan during host start, before
/// the dispatcher accepts connections, so files written into the
/// workspace before spawn are already reflected in the first
/// <c>Workspace.Detect</c> response — no watch settling delay is
/// needed.
/// </para>
/// <para>
/// Gated with the repository's <c>Category=Smoke</c> trait so it runs
/// under <c>.\scripts\test.ps1 -Smoke DotNet</c> and stays out of
/// the default unit-test pass.
/// </para>
/// </remarks>
[Trait("Category", "Smoke")]
public sealed class WorkspaceDetectionTests
{
    [Fact]
    public async Task Should_detect_workspace_technologies_over_rpc_without_overrides_field()
    {
        // Arrange — seed a workspace the startup scan will classify as
        // C# + Python, plus an override file under .github/instructions
        // that the Detect contract must remain blind to.
        var ct = TestContext.Current.CancellationToken;
        using var cache = IsolatedCacheRoot.Create();
        using var workspace = WorkspaceTestDirectoryFactory.Create();
        var workspacePath = workspace.Path;
        await File.WriteAllTextAsync(
            Path.Combine(workspacePath, "App.csproj"), "<Project />", ct);
        await File.WriteAllTextAsync(
            Path.Combine(workspacePath, "main.py"), "print('hi')", ct);
        var instructionsDir = Path.Combine(workspacePath, ".github", "instructions");
        Directory.CreateDirectory(instructionsDir);
        await File.WriteAllTextAsync(
            Path.Combine(instructionsDir, "sample.instructions.md"), "---\n---\n", ct);

        await using var engine = new EngineTestProcess
        {
            Options = new()
            {
                WorkspacePath = workspacePath,
                CacheRootOverride = cache.Path,
            },
        };
        await engine.SpawnAsync(ct);

        var rpc = await EngineWireTestClient.ConnectAsync(EndpointKind.Rpc, engine, ct);
        await using var rpcDisposer = rpc.ConfigureAwait(false);
        var codec = new LengthPrefixedFrameCodec(rpc);

        await EngineWireTestClient.SendHelloAsync(codec, ProtocolVersion.Current, ct);
        await EngineWireTestClient.ReadResponseAsync(codec, "Engine.Hello response", ct);

        // Act
        await EngineWireTestClient.SendRequestAsync(codec, id: 2, WorkspaceMethods.Detect, ct);
        var response = await EngineWireTestClient.ReadResponseAsync(codec, "Workspace.Detect response", ct);

        // Assert
        var envelope = response.Result!.Value;
        var detect = JsonSerializer.Deserialize(
            envelope, ProtocolJsonContext.Default.JsonWorkspaceDetectResult);
        Assert.NotNull(detect);
        Assert.Multiple(
            () => Assert.Null(response.Error),
            () => Assert.True(detect!.Flags.HasCSharp),
            () => Assert.True(detect!.Flags.HasPython),
            () => Assert.False(detect!.Flags.HasNodeJs),
            () => Assert.Contains("csproj", detect!.Extensions),
            () => Assert.Contains("py", detect!.Extensions),
            () => Assert.True(envelope.TryGetProperty("extensions", out _)),
            () => Assert.True(envelope.TryGetProperty("flags", out _)),
            () => Assert.False(envelope.TryGetProperty("overrides", out _)));
    }

    [Fact]
    public async Task Should_report_engine_process_metadata_over_rpc()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var cache = IsolatedCacheRoot.Create();
        var instanceId = Guid.NewGuid();

        await using var engine = new EngineTestProcess
        {
            Options = new()
            {
                InstanceId = instanceId,
                CacheRootOverride = cache.Path,
            },
        };
        await engine.SpawnAsync(ct);

        var rpc = await EngineWireTestClient.ConnectAsync(EndpointKind.Rpc, engine, ct);
        await using var rpcDisposer = rpc.ConfigureAwait(false);
        var codec = new LengthPrefixedFrameCodec(rpc);

        await EngineWireTestClient.SendHelloAsync(codec, ProtocolVersion.Current, ct);
        await EngineWireTestClient.ReadResponseAsync(codec, "Engine.Hello response", ct);

        // Act
        await EngineWireTestClient.SendRequestAsync(codec, id: 2, WorkspaceMethods.Info, ct);
        var response = await EngineWireTestClient.ReadResponseAsync(codec, "Workspace.Info response", ct);

        // Assert — Info carries engine-process identity distinct from
        // Detect's content shape: the spawned instance id, a non-empty
        // engine version, the idle-gate-disabled timeout the harness
        // pins, and a non-negative state revision.
        var info = JsonSerializer.Deserialize(
            response.Result!.Value, ProtocolJsonContext.Default.JsonWorkspaceInfoResult);
        Assert.NotNull(info);
        Assert.Multiple(
            () => Assert.Null(response.Error),
            () => Assert.NotEmpty(info!.EngineVersion),
            () => Assert.Equal(instanceId, info!.InstanceId),
            () => Assert.Equal(TimeSpan.Zero, info!.IdleTimeout),
            () => Assert.True(info!.Revision >= 0));
    }
}
