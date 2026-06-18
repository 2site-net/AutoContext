namespace AutoContext.Engine.Tests.Integration;

using System.Text.Json;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.Diagnostics;
using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Engine.Tests.Support.Pipes;
using AutoContext.Framework.Pipes;

/// <summary>
/// Integration coverage for cross-instance config reload coalescing
/// (Phase 3 row 10). Spawns two <c>autocontext-engine</c> instances
/// against one workspace — a writer and a subscriber — and proves
/// that a peer write to <c>.autocontext.json</c> reaches the
/// subscriber through its file watcher as exactly one
/// <c>Config.Subscribe</c> fan-out frame, even though the writer's
/// atomic save produces a burst of raw filesystem events.
/// </summary>
/// <remarks>
/// <para>
/// The subscriber's trailing-edge debounce collapses each peer
/// write's event burst into a single reconcile, so each logical
/// write yields one snapshot frame. The test verifies this with a
/// sentinel: a second peer write is issued only after the first
/// reload frame is observed, and the next frame the subscriber
/// reads must carry the second write's state — never a stray
/// duplicate of the first. A broken debounce would desynchronise
/// this one-frame-per-write correspondence.
/// </para>
/// <para>
/// Gated with the repository's <c>Category=Smoke</c> trait so it
/// runs under <c>.\build.ps1 Compile -Smoke DotNet</c> and stays out
/// of the default unit-test pass.
/// </para>
/// </remarks>
[Trait("Category", "Smoke")]
public sealed class CrossInstanceConfigReloadTests
{
    private static readonly string[] FirstWriteDisabled = ["lang-csharp"];
    private static readonly string[] BothWritesDisabled = ["lang-csharp", "lang-fsharp"];

    [Fact]
    public async Task Should_reload_config_once_per_coalesced_peer_write()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var cache = IsolatedCacheRoot.Create();
        using var workspace = WorkspaceTestDirectoryFactory.Create();
        var workspacePath = workspace.Path;
        var writerInstanceId = Guid.NewGuid();
        var subscriberInstanceId = Guid.NewGuid();

        await using var writer = new EngineTestProcess
        {
            Options = new()
            {
                WorkspacePath = workspacePath,
                InstanceId = writerInstanceId,
                CacheRootOverride = cache.Path,
            },
        };
        await writer.SpawnAsync(ct);

        await using var subscriber = new EngineTestProcess
        {
            Options = new()
            {
                WorkspacePath = workspacePath,
                InstanceId = subscriberInstanceId,
                CacheRootOverride = cache.Path,
            },
        };
        await subscriber.SpawnAsync(ct);

        // Subscriber: open a Config.Subscribe stream and drain the
        // snapshot-on-subscribe seed frame so the watcher is armed
        // before the writer touches the file.
        var subscriberRpc = await EngineWireTestClient.ConnectAsync(
            EndpointKind.Rpc, subscriber, ct);
        await using var subscriberDisposer = subscriberRpc.ConfigureAwait(false);
        var subscriberCodec = new LengthPrefixedFrameCodec(subscriberRpc);

        await EngineWireTestClient.SendHelloAsync(subscriberCodec, ProtocolVersion.Current, ct);
        await EngineWireTestClient.ReadResponseAsync(subscriberCodec, ct);
        await EngineWireTestClient.SendRequestAsync(
            subscriberCodec, id: 2, ConfigMethods.Subscribe, ct);
        var seed = await ReadSnapshotFrameAsync(subscriberCodec, ct);

        // Writer: complete the handshake so it can serve toggles.
        var writerRpc = await EngineWireTestClient.ConnectAsync(
            EndpointKind.Rpc, writer, ct);
        await using var writerDisposer = writerRpc.ConfigureAwait(false);
        var writerCodec = new LengthPrefixedFrameCodec(writerRpc);

        await EngineWireTestClient.SendHelloAsync(writerCodec, ProtocolVersion.Current, ct);
        await EngineWireTestClient.ReadResponseAsync(writerCodec, ct);

        // Act — first peer write. The writer toggles one file; the
        // resulting atomic-rename burst on disk must collapse to a
        // single fan-out frame on the subscriber's stream.
        await ToggleFileAsync(writerCodec, id: 2, "lang-csharp", ct);
        var firstReload = await ReadSnapshotFrameAsync(subscriberCodec, ct);

        // Act — second peer write, issued only after the first
        // reload was observed so the two writes cannot coalesce into
        // one reconcile. Acts as a sentinel: a leaked duplicate of
        // the first write would surface here instead of the second
        // write's state.
        await ToggleFileAsync(writerCodec, id: 3, "lang-fsharp", ct);
        var secondReload = await ReadSnapshotFrameAsync(subscriberCodec, ct);

        // Assert
        var firstDisabled = DisabledFileNames(firstReload);
        var secondDisabled = DisabledFileNames(secondReload);
        Assert.Multiple(
            () => Assert.Empty(seed.Instructions),
            () => Assert.Equal(FirstWriteDisabled, firstDisabled),
            () => Assert.Equal(BothWritesDisabled, secondDisabled));

        static string[] DisabledFileNames(JsonConfigSnapshot snapshot)
        {
            return
            [
                .. snapshot.Instructions
                    .Where(file => file.Disabled == true)
                    .Select(file => file.Name!)
                    .Order(),
            ];
        }

        static async Task ToggleFileAsync(
            LengthPrefixedFrameCodec codec, int id, string name, CancellationToken cancellationToken)
        {
            var parameters = JsonSerializer.SerializeToElement(
                new JsonConfigToggleFileParams { Name = name },
                ProtocolJsonContext.Default.JsonConfigToggleFileParams);
            await EngineWireTestClient.SendRequestAsync(
                codec, id, ConfigMethods.ToggleFile, parameters, cancellationToken);
            var response = await EngineWireTestClient.ReadResponseAsync(codec, cancellationToken);
            Assert.Null(response.Error);
        }

        static async Task<JsonConfigSnapshot> ReadSnapshotFrameAsync(
            LengthPrefixedFrameCodec codec, CancellationToken cancellationToken)
        {
            var frame = await EngineWireTestClient.ReadStreamFrameAsync(codec, cancellationToken);
            var next = Assert.IsType<JsonRpcStreamNext>(frame);
            var payload = next.Result.Deserialize(
                ProtocolJsonContext.Default.JsonConfigStreamFrame);
            return Assert.IsType<JsonConfigSnapshotFrame>(payload).Snapshot;
        }
    }
}
