namespace AutoContext.Mcp.Server.Tests.Smoke;

using System.Text.Json;

using AutoContext.Mcp.Server.Tools.Results;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

/// <summary>
/// End-to-end smoke tests that spawn the real
/// <c>AutoContext.Mcp.Server.exe</c>, <c>AutoContext.Worker.DotNet.exe</c>,
/// and <c>AutoContext.Worker.Workspace.exe</c> processes and drive them
/// through an MCP client over stdio. Exercises a representative — not
/// exhaustive — selection of tools and tasks so every layer of the
/// architecture (manifest loading, MCP stdio transport, pipe dispatch,
/// composite task execution, EditorConfig cross-worker fan-out) is
/// validated in an integrated run.
/// </summary>
/// <remarks>
/// Gated by the <c>Category=Smoke</c> trait; excluded from the default
/// <c>.\build.ps1 Test DotNet</c> run and invoked explicitly via
/// <c>.\build.ps1 Test -Smoke DotNet</c>.
/// </remarks>
[Trait("Category", "Smoke")]
public sealed class EndToEndSmokeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task Should_invoke_tools_end_to_end_over_mcp_stdio()
    {
        using var timeoutCts = new CancellationTokenSource(TestTimeout);
        var ct = timeoutCts.Token;

        var suffix = "smoke-" + Guid.NewGuid().ToString("N")[..12];

        await using var dotnetWorker = await WorkerProcess.StartAsync(
            SmokePaths.WorkerDotNetExe,
            $"autocontext.worker-dotnet-{suffix}",
            "[AutoContext.Worker.DotNet] Ready.",
            ct);

        await using var workspaceWorker = await WorkerProcess.StartAsync(
            SmokePaths.WorkerWorkspaceExe,
            $"autocontext.worker-workspace-{suffix}",
            "[AutoContext.Worker.Workspace] Ready.",
            ct,
            extraArguments: ["--workspace-root", SmokePaths.WorkspaceRoot]);

        var transportOptions = new StdioClientTransportOptions
        {
            Name = "AutoContext.Mcp.Server (smoke)",
            Command = SmokePaths.McpToolsExe,
            Arguments = ["--endpoint-suffix", suffix],
        };

        var transport = new StdioClientTransport(transportOptions);
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: ct);

        // 1. tools/list — assert the manifest reached the MCP surface.
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        var toolNames = tools.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Multiple(
            () => Assert.Contains("analyze_csharp_code", toolNames),
            () => Assert.Contains("read_editorconfig_properties", toolNames));

        // 2. analyze_csharp_code — exercises Worker.DotNet (7 composite
        //    tasks) and transitively Worker.Workspace (editorconfig
        //    look-ups for the coding-style / project-structure tasks).
        var csharpEnvelope = await CallToolAsync(
            client,
            "analyze_csharp_code",
            new Dictionary<string, object?>
            {
                ["content"] = "namespace Demo;\n\npublic class Foo\n{\n}\n",
            },
            ct);

        Assert.Multiple(
            () => Assert.Equal("analyze_csharp_code", csharpEnvelope.Tool),
            () => Assert.Equal(7, csharpEnvelope.Summary.TaskCount),
            () => Assert.NotEqual(ToolResultEnvelope.StatusError, csharpEnvelope.Status));

        // 3. read_editorconfig_properties — exercises Worker.Workspace
        //    directly (single task, distinct tool).
        var editorConfigEnvelope = await CallToolAsync(
            client,
            "read_editorconfig_properties",
            new Dictionary<string, object?>
            {
                ["path"] = typeof(EndToEndSmokeTests).Assembly.Location,
            },
            ct);

        Assert.Multiple(
            () => Assert.Equal("read_editorconfig_properties", editorConfigEnvelope.Tool),
            () => Assert.Equal(1, editorConfigEnvelope.Summary.TaskCount),
            () => Assert.NotEqual(ToolResultEnvelope.StatusError, editorConfigEnvelope.Status));
    }

    private static async Task<ToolResultEnvelope> CallToolAsync(
        McpClient client,
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var result = await client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken);

        var textBlock = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        var envelope = JsonSerializer.Deserialize<ToolResultEnvelope>(textBlock.Text)
            ?? throw new InvalidOperationException(
                $"Tool '{toolName}' returned an empty envelope.");

        if (string.Equals(envelope.Status, ToolResultEnvelope.StatusError, StringComparison.Ordinal))
        {
            throw new Xunit.Sdk.XunitException(
                $"Tool '{toolName}' returned status='error'. Raw envelope:\n{textBlock.Text}");
        }

        return envelope;
    }
}
