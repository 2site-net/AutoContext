namespace AutoContext.Engine.Tests.Integration;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.Diagnostics;
using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Engine.Tests.Support.Mcp;
using AutoContext.Engine.Tests.Support.Pipes;
using AutoContext.Framework.Pipes;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

/// <summary>
/// End-to-end coverage for the <c>--mcp-server with-stdio</c> role
/// (Phase 11 row 3). Spawns the real <c>autocontext-engine</c> binary in the
/// stdio MCP-server role and drives <c>tools/list</c> / <c>tools/call</c>
/// over an actual <see cref="McpClient"/>, proving the process-boundary
/// behaviour no in-process test can: that a worker-backed <c>tools/call</c>
/// returns <em>byte-identical</em> content to the daemon's pipe
/// <c>McpTools.Invoke</c> (P1 — one handler, two transports), that the role
/// coexists with a parallel daemon without binding its pipes or adding an
/// <c>engine-registry.json</c> row, that it re-reads <c>.autocontext.json</c>
/// on every request, and that stdin EOF exits it cleanly.
/// </summary>
/// <remarks>
/// Gated with the repository's <c>Category=Smoke</c> trait so it runs under
/// <c>.\scripts\test.ps1 -Smoke DotNet</c> and stays out of the default
/// unit-test pass.
/// </remarks>
[Trait("Category", "Smoke")]
public sealed class StdioMcpServerTests
{
    [Fact]
    public async Task Should_serve_tools_over_stdio_matching_the_pipe_and_write_no_registry_entry()
    {
        // Arrange — a substitute side-car overlay routing worker-backed tools
        // to the test-driver worker, shared by a daemon and the stdio server
        // over the same workspace + cache root so their coexistence is real.
        var ct = TestContext.Current.CancellationToken;

        // Declared before the engines so they dispose after both exit.
        using var resourcesRoot = TestDriverResourcesOverlay.Create();
        using var cache = IsolatedCacheRoot.Create();
        using var workspace = WorkspaceTestDirectoryFactory.Create();

        var payload = $"round-trip-{Guid.NewGuid():N}";

        await using var daemon = new EngineTestProcess
        {
            Options = new()
            {
                WorkspacePath = workspace.Path,
                CacheRootOverride = cache.Path,
                ResourcesRootOverride = resourcesRoot.Path,
            },
        };
        await daemon.SpawnAsync(ct);

        var rpc = await EngineWireTestClient.ConnectAsync(EndpointKind.Rpc, daemon, ct);
        await using var rpcDisposer = rpc.ConfigureAwait(false);
        var codec = new LengthPrefixedFrameCodec(rpc);
        await EngineWireTestClient.SendHelloAsync(codec, ProtocolVersion.Current, ct);
        await EngineWireTestClient.ReadResponseAsync(codec, "Engine.Hello response", ct);

        // The stdio MCP server on the same workspace / cache / resources. Its
        // McpClient.CreateAsync completing the initialize handshake is proof
        // the role started and speaks MCP over real stdio.
        await using var client = await StdioMcpServerClient.CreateAsync(
            workspace.Path, resourcesRoot.Path, cache.Path, ct);

        // Act — list the stdio tool surface, drive the same worker-backed tool
        // with the same arguments over both transports, and read the daemon's
        // registry view while both engines are live.
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        var stdioText = await CallEchoOverStdioAsync(client, payload, ct);
        var pipeText = await InvokeEchoOverPipeAsync(codec, payload, ct);
        var entries = await ReadRegistryEntriesAsync(codec, ct);

        // Assert
        var toolNames = tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        var entry = Assert.Single(entries.Entries);

        Assert.Multiple(
            // The stdio surface carries both a worker-backed tool and an
            // intrinsic instruction tool.
            () => Assert.Contains(TestDriverResourcesOverlay.EchoTool, toolNames),
            () => Assert.Contains("list_instructions", toolNames),
            // P1: the same handler produces byte-identical content regardless
            // of transport.
            () => Assert.Equal(pipeText, stdioText),
            // Coexistence: the stdio role added no registry row — the single
            // entry is the daemon's — and thus bound none of the daemon pipes.
            () => Assert.Equal(daemon.InstanceId, entry.InstanceId));

        static async Task<string> CallEchoOverStdioAsync(
            McpClient client, string payload, CancellationToken cancellationToken)
        {
            // The stdio role spawns its own test-driver worker lazily on the
            // first call. That cold spawn can lose the worker's one-shot accept
            // re-arm race and surface the tool-error arm, so retry the echo
            // until it round-trips; each attempt is a complete response, so
            // retrying is safe.
            const int MaxAttempts = 10;
            var arguments = new Dictionary<string, object?> { ["payload"] = payload };

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var result = await client.CallToolAsync(
                    TestDriverResourcesOverlay.EchoTool, arguments, cancellationToken: cancellationToken);
                var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

                var parsed = JsonSerializer.Deserialize(
                    text, ProtocolJsonContext.Default.JsonMcpToolsInvokeResult);
                if (parsed is JsonMcpToolsInvokeOkResult)
                {
                    return text;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }

            Assert.Fail("The worker did not become ready over stdio after repeated echo tool calls.");
            return null!; // Unreachable: Assert.Fail always throws.
        }

        static async Task<string> InvokeEchoOverPipeAsync(
            LengthPrefixedFrameCodec codec, string payload, CancellationToken cancellationToken)
        {
            // Read with a deadline above the engine's own 30s worker-wait so a
            // cold spawn completes inside one bounded read; never retry a
            // timed-out read on the same pipe (a late frame would desync it).
            var readTimeout = TimeSpan.FromSeconds(35);
            const int MaxAttempts = 10;

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var paramsNode = new JsonObject
                {
                    ["name"] = TestDriverResourcesOverlay.EchoTool,
                    ["arguments"] = new JsonObject { ["payload"] = payload },
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
                    return response.Result!.Value.GetRawText();
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }

            Assert.Fail("The worker did not become ready over the pipe after repeated echo invocations.");
            return null!; // Unreachable: Assert.Fail always throws.
        }

        static async Task<JsonRegistryEntriesResult> ReadRegistryEntriesAsync(
            LengthPrefixedFrameCodec codec, CancellationToken cancellationToken)
        {
            await EngineWireTestClient.SendRequestAsync(
                codec, id: 900, RegistryMethods.RegistryEntries, cancellationToken);
            var response = await EngineWireTestClient.ReadResponseAsync(
                codec, "Engine.RegistryEntries response", cancellationToken);
            Assert.Null(response.Error);

            var result = response.Result!.Value.Deserialize(
                ProtocolJsonContext.Default.JsonRegistryEntriesResult);
            Assert.NotNull(result);
            return result!;
        }
    }

    [Fact]
    public async Task Should_reread_autocontext_json_between_tools_list_requests()
    {
        // Arrange — the stdio server over a fresh workspace with the
        // test-driver overlay, so a disable-able worker-backed tool is present.
        var ct = TestContext.Current.CancellationToken;

        using var resourcesRoot = TestDriverResourcesOverlay.Create();
        using var cache = IsolatedCacheRoot.Create();
        using var workspace = WorkspaceTestDirectoryFactory.Create();

        await using var client = await StdioMcpServerClient.CreateAsync(
            workspace.Path, resourcesRoot.Path, cache.Path, ct);

        // Act — list once (tool visible), disable it on disk, list again. No
        // restart between the two requests.
        var before = await client.ListToolsAsync(cancellationToken: ct);

        var disableEcho = new JsonObject
        {
            ["mcpTools"] = new JsonObject
            {
                [TestDriverResourcesOverlay.EchoTool] = new JsonObject { ["disabled"] = true },
            },
        };
        await File.WriteAllTextAsync(
            Path.Combine(workspace.Path, ".autocontext.json"), disableEcho.ToJsonString(), ct);

        var after = await client.ListToolsAsync(cancellationToken: ct);

        // Assert — the newly-disabled tool disappears without a restart,
        // proving the adapter re-reads .autocontext.json on every request,
        // while an untouched intrinsic tool stays visible.
        var beforeNames = before.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        var afterNames = after.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Multiple(
            () => Assert.Contains(TestDriverResourcesOverlay.EchoTool, beforeNames),
            () => Assert.DoesNotContain(TestDriverResourcesOverlay.EchoTool, afterNames),
            () => Assert.Contains("list_instructions", afterNames));
    }

    [Fact]
    public async Task Should_exit_cleanly_when_stdin_reaches_eof()
    {
        // Arrange — spawn the role directly (not through the MCP client) so the
        // child's exit code is observable.
        var ct = TestContext.Current.CancellationToken;
        using var workspace = WorkspaceTestDirectoryFactory.Create();

        var executablePath = EngineBinaryPath.Value;
        Assert.True(
            File.Exists(executablePath),
            $"autocontext-engine binary not found at '{executablePath}'. "
            + "Run '.\\build.ps1 DotNet' before running engine integration tests.");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        foreach (var argument in StdioMcpServerClient.BuildArguments(workspace.Path))
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        // Drain stdout/stderr so a shutdown write can never block the child on
        // a full pipe buffer.
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };

        Assert.True(process.Start(), $"Failed to start '{executablePath}'.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Act — closing stdin signals EOF to the MCP stdio transport, which
        // completes its read loop and stops the host.
        process.StandardInput.Close();
        await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(30), ct);

        // Assert — a clean, zero-code exit (no crash, no hang).
        Assert.Multiple(
            () => Assert.True(process.HasExited),
            () => Assert.Equal(0, process.ExitCode));
    }
}
