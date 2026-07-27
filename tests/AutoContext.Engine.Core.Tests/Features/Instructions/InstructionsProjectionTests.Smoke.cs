namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using System.Text.Json;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.Diagnostics;
using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Engine.Tests.Support.Pipes;
using AutoContext.Framework.Pipes;

/// <summary>
/// End-to-end coverage for <c>Instructions.*</c> projection and
/// config-driven invalidation over the <c>rpc</c> pipe (Phase 6
/// row 14). Spawns the <c>autocontext-engine</c> binary against a
/// fresh workspace and proves three contracts across a real process
/// boundary: the bundled corpus projects into
/// <c>Instructions.Subscribe</c> snapshot rows and an
/// <c>Instructions.Get</c> body, a <c>Config.ToggleFile</c> write
/// rebroadcasts the listing with the targeted file's <c>disabled</c>
/// flag flipped without a corpus reload, and a subsequent
/// <c>Instructions.Get</c> for the now-disabled file collapses to the
/// identity-only <c>disabled</c> envelope.
/// </summary>
/// <remarks>
/// <para>
/// The engine loads its bundled corpus and override inventory during
/// host start, before the dispatcher accepts connections, so the
/// first <c>Instructions.Subscribe</c> snapshot already reflects the
/// full corpus — no watch settling delay is needed. The subscription
/// occupies its own connection (server-streaming is terminal, one
/// stream per connection); the unary <c>Get</c> and <c>ToggleFile</c>
/// requests run on a second connection.
/// </para>
/// <para>
/// The invalidation arm exercises the row-12 bridge: a config change
/// re-projects the listing from the live snapshot and republishes it
/// on <c>Instructions.Subscribe</c>, so the disabled flag flips
/// without reloading the immutable bundled corpus.
/// </para>
/// <para>
/// Gated with the repository's <c>Category=Smoke</c> trait so it runs
/// under <c>.\scripts\test.ps1 -Smoke DotNet</c> and stays out of
/// the default unit-test pass.
/// </para>
/// </remarks>
[Trait("Category", "Smoke")]
public sealed class InstructionsProjectionTests
{
    private const string TargetKey = "lang-csharp";

    [Fact]
    public async Task Should_project_corpus_and_invalidate_on_config_toggle_over_rpc()
    {
        // Arrange — a fresh workspace with no overrides, so every
        // listing row resolves to the engine's bundled corpus.
        var ct = TestContext.Current.CancellationToken;
        using var cache = IsolatedCacheRoot.Create();
        using var workspace = WorkspaceTestDirectoryFactory.Create();
        var workspacePath = workspace.Path;

        await using var engine = new EngineTestProcess
        {
            Options = new()
            {
                WorkspacePath = workspacePath,
                CacheRootOverride = cache.Path,
            },
        };
        await engine.SpawnAsync(ct);

        // Subscriber connection: open the Instructions.Subscribe stream
        // and drain the snapshot-on-subscribe seed frame so the bridge
        // is primed before the config toggle lands.
        var subscriberRpc = await EngineWireTestClient.ConnectAsync(
            EndpointKind.Rpc, engine, ct);
        await using var subscriberDisposer = subscriberRpc.ConfigureAwait(false);
        var subscriberCodec = new LengthPrefixedFrameCodec(subscriberRpc);

        await EngineWireTestClient.SendHelloAsync(subscriberCodec, ProtocolVersion.Current, ct);
        await EngineWireTestClient.ReadResponseAsync(subscriberCodec, "subscriber Engine.Hello response", ct);
        await EngineWireTestClient.SendRequestAsync(
            subscriberCodec, id: 2, InstructionsMethods.Subscribe, ct);
        var seed = await ReadSnapshotFrameAsync(subscriberCodec, ct);

        // Client connection: unary Get + ToggleFile requests.
        var clientRpc = await EngineWireTestClient.ConnectAsync(EndpointKind.Rpc, engine, ct);
        await using var clientDisposer = clientRpc.ConfigureAwait(false);
        var clientCodec = new LengthPrefixedFrameCodec(clientRpc);

        await EngineWireTestClient.SendHelloAsync(clientCodec, ProtocolVersion.Current, ct);
        await EngineWireTestClient.ReadResponseAsync(clientCodec, "client Engine.Hello response", ct);

        // Act — read the projected body before the toggle (active),
        // flip the file disabled, observe the rebroadcast listing, then
        // read the body again (now disabled).
        var bodyBeforeToggle = await GetAsync(clientCodec, id: 2, TargetKey, ct);
        await ToggleFileAsync(clientCodec, id: 3, TargetKey, ct);
        var reload = await ReadSnapshotFrameAsync(subscriberCodec, ct);
        var bodyAfterToggle = await GetAsync(clientCodec, id: 4, TargetKey, ct);

        // Assert
        var seedRow = SelectRow(seed, TargetKey);
        var reloadRow = SelectRow(reload, TargetKey);
        var okBeforeToggle = Assert.IsType<JsonInstructionsGetOkResult>(bodyBeforeToggle);

        Assert.Multiple(
            // Projection at subscribe time: the bundled file is present,
            // active, and resolves to the bundled corpus.
            () => Assert.NotNull(seedRow),
            () => Assert.False(seedRow!.Disabled),
            () => Assert.Equal(InstructionsSource.Bundled, seedRow!.Source),
            // Projection over rpc: Get returns the projected body.
            () => Assert.Equal(TargetKey, okBeforeToggle.Key),
            () => Assert.False(string.IsNullOrWhiteSpace(okBeforeToggle.Content)),
            // Invalidation: the config toggle rebroadcasts the listing
            // with the disabled flag flipped, without a corpus reload.
            () => Assert.NotNull(reloadRow),
            () => Assert.True(reloadRow!.Disabled),
            () => Assert.Equal(InstructionsSource.Bundled, reloadRow!.Source),
            // Projection reflects the new state: Get now collapses to
            // the identity-only disabled envelope.
            () => Assert.IsType<JsonInstructionsGetDisabledResult>(bodyAfterToggle));

        static JsonInstructionsListRow? SelectRow(
            JsonInstructionsSnapshotFrame frame, string key)
        {
            return frame.Files.FirstOrDefault(file => file.Key == key);
        }

        static async Task<JsonInstructionsGetResult> GetAsync(
            LengthPrefixedFrameCodec codec, int id, string name, CancellationToken cancellationToken)
        {
            var parameters = JsonSerializer.SerializeToElement(
                new JsonInstructionsGetParams { Name = name },
                ProtocolJsonContext.Default.JsonInstructionsGetParams);
            await EngineWireTestClient.SendRequestAsync(
                codec, id, InstructionsMethods.Get, parameters, cancellationToken);
            var response = await EngineWireTestClient.ReadResponseAsync(codec, "Instructions.Get response", cancellationToken);
            Assert.Null(response.Error);
            var result = response.Result!.Value.Deserialize(
                ProtocolJsonContext.Default.JsonInstructionsGetResult);
            Assert.NotNull(result);
            return result!;
        }

        static async Task ToggleFileAsync(
            LengthPrefixedFrameCodec codec, int id, string name, CancellationToken cancellationToken)
        {
            var parameters = JsonSerializer.SerializeToElement(
                new JsonConfigToggleFileParams { Name = name },
                ProtocolJsonContext.Default.JsonConfigToggleFileParams);
            await EngineWireTestClient.SendRequestAsync(
                codec, id, ConfigMethods.ToggleFile, parameters, cancellationToken);
            var response = await EngineWireTestClient.ReadResponseAsync(codec, "Config.ToggleFile response", cancellationToken);
            Assert.Null(response.Error);
        }

        static async Task<JsonInstructionsSnapshotFrame> ReadSnapshotFrameAsync(
            LengthPrefixedFrameCodec codec, CancellationToken cancellationToken)
        {
            var frame = await EngineWireTestClient.ReadStreamFrameAsync(codec, "Instructions snapshot stream frame", cancellationToken);
            var next = Assert.IsType<JsonRpcStreamNext>(frame);
            var payload = next.Result.Deserialize(
                ProtocolJsonContext.Default.JsonInstructionsStreamFrame);
            return Assert.IsType<JsonInstructionsSnapshotFrame>(payload);
        }
    }
}
