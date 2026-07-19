namespace AutoContext.Engine;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.McpServer;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Composition root for the <c>--mcp-server with-stdio</c> role. Builds a
/// reduced host — the in-process instruction capabilities plus on-demand
/// worker dispatch, and nothing else — and runs it until stdio EOF. No
/// daemon pipes are bound, no <c>engine-registry.json</c> entry is
/// written, and no <c>engine.log</c> is opened. The worker-dispatch pipes
/// are namespaced by an ephemeral instance id minted here (never accepted
/// from argv), so the process coexists with any daemon on the same
/// workspace without collision.
/// </summary>
internal static class McpServerHostFactory
{
    public static async Task<int> RunAsync(EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // The role rejects --instance-id at argv (a daemon discovery
        // concern); the private worker pipes still need a scope, so mint a
        // fresh, process-lifetime id that is never advertised.
        var ephemeralInstanceId = Guid.NewGuid();

        var builder = Host.CreateApplicationBuilder();

        // stdout carries the MCP JSON-RPC transport; operational logs go to
        // stderr only. Clear the default providers Host.CreateApplicationBuilder
        // installs (they would corrupt the protocol stream on stdout).
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(consoleOptions => consoleOptions.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.AddAutoContextMcpStdioServer(target =>
        {
            target.WorkspacePath = options.WorkspacePath;
            target.McpServerMode = options.McpServerMode;
            target.InstanceId = ephemeralInstanceId;
            target.ResourcesRootOverride = options.ResourcesRootOverride;
            target.CorpusRootOverride = options.CorpusRootOverride;
            target.CacheRootOverride = options.CacheRootOverride;
        });

        using var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
        return 0;
    }
}
