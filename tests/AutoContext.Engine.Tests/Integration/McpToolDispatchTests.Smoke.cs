namespace AutoContext.Engine.Tests.Integration;

using System.Text.Json;
using System.Text.Json.Nodes;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.Diagnostics;
using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Engine.Tests.Support.Pipes;
using AutoContext.Framework.Pipes;

/// <summary>
/// End-to-end coverage for <c>McpTools.*</c> dispatch over the <c>rpc</c>
/// pipe (Phase 7 row 9). Spawns the <c>autocontext-engine</c> binary
/// pointed — via <c>--resources-root</c> — at a substitute side-car tree
/// whose <c>workers.json</c> + <c>mcp-tools-registry.json</c> +
/// <c>mcp-tools-catalog.json</c> route three tools to the standalone
/// <c>AutoContext.Worker.Test.Driver</c> worker, and proves the full
/// cross-process dispatch path: <c>McpTools.List</c> projects the
/// substitute registry, a <c>McpTools.Invoke</c> happy path lazily spawns
/// the worker and round-trips the arguments through the engine→worker
/// request/response envelope, and a deliberately failing tool collapses to
/// the <c>tool-error</c> arm.
/// </summary>
/// <remarks>
/// <para>
/// The engine loads its MCP-tools registry and worker manifest during host
/// start, before the dispatcher accepts connections, so the first
/// <c>McpTools.List</c> already reflects the substitute tree. The worker
/// itself is spawned lazily on the first <c>Invoke</c> routed to it — the
/// engine dials its named pipe, exchanges one length-prefixed frame, and
/// marshals the worker's <c>{ status, output, error }</c> envelope into the
/// discriminated <c>McpTools.Invoke</c> result.
/// </para>
/// <para>
/// The substitute category carries no activation flags, so all three tools
/// stay active regardless of the empty workspace's detected capabilities,
/// and the fresh workspace ships no <c>.autocontext.json</c>, so none are
/// muted. The <c>test_hang</c> tool is registered (and asserted present in
/// the listing) but not invoked here: its only purpose is the 30s
/// wait-deadline arm, which would dominate the test budget.
/// </para>
/// <para>
/// Gated with the repository's <c>Category=Smoke</c> trait so it runs under
/// <c>.\build.ps1 Compile -Smoke DotNet</c> and stays out of the default
/// unit-test pass.
/// </para>
/// </remarks>
[Trait("Category", "Smoke")]
public sealed class McpToolDispatchTests
{
    private const string WorkerId = "test-driver";
    private const string EchoTool = "test_echo";
    private const string FailTool = "test_fail";
    private const string HangTool = "test_hang";

    [Fact]
    public async Task Should_dispatch_mcp_tool_calls_to_the_worker_over_rpc()
    {
        // Arrange — a substitute side-car tree routing three tools to the
        // standalone test-driver worker, plus a fresh workspace and cache
        // root so nothing the engine detects mutes or filters the tools.
        var ct = TestContext.Current.CancellationToken;

        var driverPath = TestDriverWorkerBinaryPath.Value;
        Assert.True(
            File.Exists(driverPath),
            $"Test-driver worker binary not found at '{driverPath}'. "
            + "Run '.\\build.ps1 Compile DotNet' before running engine integration tests.");

        var resourcesRoot = CreateResourcesOverlay(driverPath);
        var cache = IsolatedCacheRoot.Create();
        var workspacePath = WorkspaceTestDirectoryFactory.Create();

        var expectedPayload = $"round-trip-{Guid.NewGuid():N}";

        await using var engine = new EngineTestProcess
        {
            Options = new()
            {
                WorkspacePath = workspacePath,
                CacheRootOverride = cache.Path,
                ResourcesRootOverride = resourcesRoot,
            },
        };
        await engine.SpawnAsync(ct);

        var rpc = await EngineWireTestClient.ConnectAsync(EndpointKind.Rpc, engine, ct);
        await using var rpcDisposer = rpc.ConfigureAwait(false);
        var codec = new LengthPrefixedFrameCodec(rpc);

        await EngineWireTestClient.SendHelloAsync(codec, ProtocolVersion.Current, ct);
        await EngineWireTestClient.ReadResponseAsync(codec, ct);

        // Act — list the projected catalog, dispatch the echo happy path
        // (which lazily spawns the worker), then dispatch the failing tool.
        var listing = await ListAsync(codec, id: 2, ct);
        var echoResult = await InvokeAsync(
            codec, id: 3, EchoTool, new JsonObject { ["payload"] = expectedPayload }, ct);
        var failResult = await InvokeAsync(codec, id: 4, FailTool, arguments: null, ct);

        // Assert
        var echoRow = SelectRow(listing, EchoTool);
        var failRow = SelectRow(listing, FailTool);
        var hangRow = SelectRow(listing, HangTool);

        var echoOk = Assert.IsType<JsonMcpToolsInvokeOkResult>(echoResult);
        var failError = Assert.IsType<JsonMcpToolsInvokeToolErrorResult>(failResult);

        Assert.Multiple(
            // Listing: all three substitute tools project through, routed to
            // the test-driver worker and active (not muted).
            () => Assert.NotNull(echoRow),
            () => Assert.Equal(WorkerId, echoRow!.WorkerId),
            () => Assert.False(echoRow!.Disabled),
            () => Assert.NotNull(failRow),
            () => Assert.Equal(WorkerId, failRow!.WorkerId),
            () => Assert.False(failRow!.Disabled),
            () => Assert.NotNull(hangRow),
            () => Assert.Equal(WorkerId, hangRow!.WorkerId),
            () => Assert.False(hangRow!.Disabled),
            // Happy path: the worker echoed the arguments back, proving the
            // engine→worker request/response envelope round-trips intact.
            () => Assert.Equal(EchoTool, echoOk.Name),
            () => Assert.Equal(expectedPayload, ReadEchoedPayload(echoOk)),
            // Failure path: the tool ran and reported failure, so the engine
            // surfaces the tool-error arm carrying the worker's message.
            () => Assert.Equal(FailTool, failError.Name),
            () => Assert.True(failError.IsError),
            () => Assert.Contains(
                "deliberately failed", ReadText(failError.Content), StringComparison.Ordinal));

        static JsonMcpToolsListRow? SelectRow(JsonMcpToolsListResult listing, string name)
        {
            return listing.Tools.FirstOrDefault(row => row.Name == name);
        }

        static string ReadEchoedPayload(JsonMcpToolsInvokeOkResult result)
        {
            var text = ReadText(result.Content);
            using var document = JsonDocument.Parse(text);
            return document.RootElement.GetProperty("payload").GetString()
                ?? throw new InvalidOperationException("Echoed payload was null.");
        }

        static string ReadText(IReadOnlyList<JsonElement> content)
        {
            var block = Assert.Single(content);
            return block.GetProperty("text").GetString()
                ?? throw new InvalidOperationException("Content block carried a null 'text'.");
        }

        static async Task<JsonMcpToolsListResult> ListAsync(
            LengthPrefixedFrameCodec codec, int id, CancellationToken cancellationToken)
        {
            await EngineWireTestClient.SendRequestAsync(codec, id, McpToolsMethods.List, cancellationToken);
            var response = await EngineWireTestClient.ReadResponseAsync(codec, cancellationToken);
            Assert.Null(response.Error);
            var result = response.Result!.Value.Deserialize(
                ProtocolJsonContext.Default.JsonMcpToolsListResult);
            Assert.NotNull(result);
            return result!;
        }

        static async Task<JsonMcpToolsInvokeResult> InvokeAsync(
            LengthPrefixedFrameCodec codec,
            int id,
            string name,
            JsonObject? arguments,
            CancellationToken cancellationToken)
        {
            var paramsNode = new JsonObject { ["name"] = name };

            if (arguments is not null)
            {
                paramsNode["arguments"] = arguments;
            }

            using var paramsDocument = JsonDocument.Parse(paramsNode.ToJsonString());
            await EngineWireTestClient.SendRequestAsync(
                codec, id, McpToolsMethods.Invoke, paramsDocument.RootElement, cancellationToken);
            var response = await EngineWireTestClient.ReadResponseAsync(codec, cancellationToken);
            Assert.Null(response.Error);
            var result = response.Result!.Value.Deserialize(
                ProtocolJsonContext.Default.JsonMcpToolsInvokeResult);
            Assert.NotNull(result);
            return result!;
        }

        static string CreateResourcesOverlay(string driverCommand)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "autocontext-engine-tests-resources",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var workers = new JsonObject
            {
                ["workers"] = new JsonArray(
                    new JsonObject
                    {
                        ["id"] = WorkerId,
                        ["type"] = "executable",
                        ["command"] = driverCommand,
                    }),
            };

            var registry = new JsonObject
            {
                ["schemaVersion"] = "1",
                ["tools"] = new JsonArray(
                    RegistryTool(EchoTool, "Echoes its arguments back verbatim for dispatch round-trip testing."),
                    RegistryTool(FailTool, "Always reports failure, exercising the tool-error arm."),
                    RegistryTool(HangTool, "Blocks until cancelled, exercising the wait-deadline arm.")),
            };

            var catalog = new JsonObject
            {
                ["schemaVersion"] = "1",
                ["categories"] = new JsonArray(
                    new JsonObject
                    {
                        ["name"] = "Test Driver",
                        ["description"] = "Deterministic stand-in worker for engine dispatch integration tests.",
                        ["workerId"] = WorkerId,
                    }),
                ["tools"] = new JsonArray(
                    CatalogTool(EchoTool, "Echoes arguments back."),
                    CatalogTool(FailTool, "Always fails."),
                    CatalogTool(HangTool, "Blocks until cancelled.")),
            };

            File.WriteAllText(Path.Combine(root, "workers.json"), workers.ToJsonString());
            File.WriteAllText(Path.Combine(root, "mcp-tools-registry.json"), registry.ToJsonString());
            File.WriteAllText(Path.Combine(root, "mcp-tools-catalog.json"), catalog.ToJsonString());

            return root;
        }

        static JsonObject RegistryTool(string name, string description)
        {
            return new JsonObject
            {
                ["name"] = name,
                ["workerId"] = WorkerId,
                ["description"] = description,
                ["parameters"] = new JsonObject
                {
                    ["payload"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Arbitrary string payload echoed back by the test driver.",
                    },
                },
            };
        }

        static JsonObject CatalogTool(string name, string description)
        {
            return new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["category"] = "Test Driver",
            };
        }
    }
}
