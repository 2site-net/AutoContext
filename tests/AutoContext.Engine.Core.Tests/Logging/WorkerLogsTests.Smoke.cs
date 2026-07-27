namespace AutoContext.Engine.Core.Tests.Logging;

using System.Text.Json;
using System.Text.Json.Nodes;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.Diagnostics;
using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Engine.Tests.Support.Pipes;
using AutoContext.Framework.Pipes;

/// <summary>
/// End-to-end coverage for the worker-log read surface (Phase 8 row 4).
/// Spawns the <c>autocontext-engine</c> binary pointed — via
/// <c>--resources-root</c> — at a substitute side-car tree routing a tool
/// to the standalone <c>AutoContext.Worker.Test.Driver</c> worker, invokes
/// the tool to cold-spawn the worker, and proves the discriminated
/// <c>Logs.GetWorker</c> read across the process boundary: a worker the
/// engine has spawned resolves to the <c>ok</c> arm (even when its log is
/// empty — the driver emits nothing), while a worker it has never spawned
/// resolves to the <c>not-found</c> arm. The engine-side capture of a
/// worker's stderr into its per-worker log is covered by the in-process
/// unit tests (the test-driver deliberately emits no stderr).
/// </summary>
/// <remarks>
/// Gated with the repository's <c>Category=Smoke</c> trait so it runs under
/// <c>.\scripts\test.ps1 -Smoke DotNet</c> and stays out of the default
/// unit-test pass.
/// </remarks>
[Trait("Category", "Smoke")]
public sealed class WorkerLogsTests
{
    private const string WorkerId = "test-driver";
    private const string EchoTool = "test_echo";

    [Fact]
    public async Task Should_distinguish_spawned_from_never_spawned_over_Logs_GetWorker()
    {
        // Arrange — a substitute side-car tree routing one tool to the
        // standalone test-driver worker, plus a fresh workspace and cache
        // root so the engine detects nothing that mutes the tool.
        var ct = TestContext.Current.CancellationToken;

        var driverPath = TestDriverWorkerBinaryPath.Value;
        Assert.True(
            File.Exists(driverPath),
            $"Test-driver worker binary not found at '{driverPath}'. "
            + "Run '.\\build.ps1 DotNet' before running engine integration tests.");

        // Declared before the engine so they dispose after it exits.
        using var resourcesRoot = CreateResourcesOverlay(driverPath);
        using var cache = IsolatedCacheRoot.Create();
        using var workspace = WorkspaceTestDirectoryFactory.Create();

        await using var engine = new EngineTestProcess
        {
            Options = new()
            {
                WorkspacePath = workspace.Path,
                CacheRootOverride = cache.Path,
                ResourcesRootOverride = resourcesRoot.Path,
            },
        };
        await engine.SpawnAsync(ct);

        var rpc = await EngineWireTestClient.ConnectAsync(EndpointKind.Rpc, engine, ct);
        await using var rpcDisposer = rpc.ConfigureAwait(false);
        var codec = new LengthPrefixedFrameCodec(rpc);

        await EngineWireTestClient.SendHelloAsync(codec, ProtocolVersion.Current, ct);
        await EngineWireTestClient.ReadResponseAsync(codec, "Engine.Hello response", ct);

        // Act — cold-spawn the worker by invoking its tool, then read its
        // log surface over rpc. The test-driver emits nothing, so its log is
        // empty; the point is the discriminated arm: a spawned worker
        // resolves to ok (even when quiet), a never-spawned worker to
        // not-found.
        await WarmUpWorkerAsync(codec, ct);

        var spawned = await GetWorkerAsync(codec, id: 4000, WorkerId, ct);
        var missing = await GetWorkerAsync(codec, id: 5000, "never-spawned", ct);

        // Assert
        Assert.Multiple(
            () => Assert.IsType<JsonLogsGetWorkerOkResult>(spawned),
            () => Assert.IsType<JsonLogsGetWorkerNotFoundResult>(missing));

        static async Task<JsonLogsGetWorkerResult> GetWorkerAsync(
            LengthPrefixedFrameCodec codec, int id, string workerId, CancellationToken cancellationToken)
        {
            var parameters = JsonSerializer.SerializeToElement(
                new JsonLogsGetWorkerParams { WorkerId = workerId },
                ProtocolJsonContext.Default.JsonLogsGetWorkerParams);
            await EngineWireTestClient.SendRequestAsync(
                codec, id, LogsMethods.GetWorker, parameters, cancellationToken);
            var response = await EngineWireTestClient.ReadResponseAsync(
                codec, "Logs.GetWorker response", cancellationToken);
            Assert.Null(response.Error);
            var result = response.Result!.Value.Deserialize(
                ProtocolJsonContext.Default.JsonLogsGetWorkerResult);
            Assert.NotNull(result);
            return result!;
        }

        static async Task WarmUpWorkerAsync(
            LengthPrefixedFrameCodec codec, CancellationToken cancellationToken)
        {
            // The first Invoke routed to a worker lazily cold-spawns it; on a
            // cold machine that synchronous spawn can take several seconds, so
            // read with a deadline above the engine's own 30s worker-wait. The
            // spawn can also lose the worker's one-shot accept re-arm race and
            // surface the tool-error arm, so retry the happy path until it
            // round-trips.
            var readTimeout = TimeSpan.FromSeconds(35);
            const int MaxAttempts = 10;
            const int RetryDelayMilliseconds = 200;

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var paramsNode = new JsonObject
                {
                    ["name"] = EchoTool,
                    ["arguments"] = new JsonObject { ["payload"] = "warm-up" },
                };
                using var paramsDocument = JsonDocument.Parse(paramsNode.ToJsonString());
                await EngineWireTestClient.SendRequestAsync(
                    codec, 1000 + attempt, McpToolsMethods.Invoke, paramsDocument.RootElement, cancellationToken);
                var response = await EngineWireTestClient.ReadResponseAsync(
                    codec, readTimeout, "McpTools.Invoke response", cancellationToken);
                Assert.Null(response.Error);
                var result = response.Result!.Value.Deserialize(
                    ProtocolJsonContext.Default.JsonMcpToolsInvokeResult);

                if (result is JsonMcpToolsInvokeOkResult)
                {
                    return;
                }

                await Task.Delay(RetryDelayMilliseconds, cancellationToken);
            }

            Assert.Fail($"Worker did not become ready after {MaxAttempts} warm-up echo invocations.");
        }

        static TempDirectory CreateResourcesOverlay(string driverCommand)
        {
            var root = TempDirectory.CreateNew("autocontext-engine-tests-worker-logs");

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
                    new JsonObject
                    {
                        ["name"] = EchoTool,
                        ["workerId"] = WorkerId,
                        ["description"] = "Echoes its arguments back verbatim for dispatch round-trip testing.",
                        ["parameters"] = new JsonObject
                        {
                            ["payload"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = "Arbitrary string payload echoed back by the test driver.",
                            },
                        },
                    }),
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
                    new JsonObject
                    {
                        ["name"] = EchoTool,
                        ["description"] = "Echoes arguments back.",
                        ["category"] = "Test Driver",
                    }),
            };

            File.WriteAllText(Path.Combine(root.Path, "workers.json"), workers.ToJsonString());
            File.WriteAllText(Path.Combine(root.Path, "mcp-tools-registry.json"), registry.ToJsonString());
            File.WriteAllText(Path.Combine(root.Path, "mcp-tools-catalog.json"), catalog.ToJsonString());

            return root;
        }
    }
}
