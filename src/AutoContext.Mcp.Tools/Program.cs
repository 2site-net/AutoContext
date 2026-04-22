namespace AutoContext.Mcp.Tools;

using AutoContext.Mcp.Tools.Dispatch;
using AutoContext.Mcp.Tools.EditorConfig;
using AutoContext.Mcp.Tools.Hosting;
using AutoContext.Mcp.Tools.Manifest;
using AutoContext.Mcp.Tools.Mcp;
using AutoContext.Mcp.Tools.Pipe;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// <c>AutoContext.Mcp.Tools</c> entry point. Standalone process that
/// loads the embedded <c>.mcp-tools.json</c> manifest, exposes every
/// declared MCP Tool over MCP/stdio (dispatching each call to the
/// appropriate <c>AutoContext.Worker.*</c> over a named pipe), and serves
/// the manifest itself over the
/// <see cref="ManifestPipeService.PipeName"/> named pipe so other
/// processes (the VS Code extension, CLI, tests) can read it.
/// </summary>
internal static class Program
{
    /// <summary>Stderr ready-marker written once the host has started.</summary>
    internal const string ReadyMarker = "[AutoContext.Mcp.Tools] Ready.";

    internal static async Task<int> Main(string[] args)
    {
        var manifestSource = new EmbeddedManifestSource();
        var manifest = ManifestLoader.Parse(manifestSource.Json);
        var validation = ManifestValidator.Validate(manifestSource.Json, manifest);

        if (!validation.IsValid)
        {
            await Console.Error.WriteLineAsync("[AutoContext.Mcp.Tools] Manifest validation failed:")
                .ConfigureAwait(false);

            foreach (var error in validation.Errors)
            {
                await Console.Error.WriteLineAsync($"  - {error}").ConfigureAwait(false);
            }

            return 1;
        }

        var builder = Host.CreateApplicationBuilder(args);

        // MCP uses stdio for JSON-RPC; route every log to stderr only.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Core graph wired through DI so the host disposes anything
        // that becomes IDisposable in the future and tests can swap
        // implementations without rewriting Main.
        builder.Services.AddSingleton<IManifestSource>(manifestSource);
        builder.Services.AddSingleton(manifest);
        builder.Services.AddSingleton<WorkerPipeClient>();
        builder.Services.AddSingleton<EditorConfigBatcher>();
        builder.Services.AddSingleton<ToolInvoker>();
        builder.Services.AddSingleton<McpToolRegistry>();

        builder.Services.AddHostedService<ManifestPipeService>();
        builder.Services.AddHostedService(_ => new ReadyMarkerService(ReadyMarker));

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithListToolsHandler((ctx, ct) =>
                ctx.Server.Services!.GetRequiredService<McpToolRegistry>().HandleListToolsAsync(ctx, ct))
            .WithCallToolHandler((ctx, ct) =>
                ctx.Server.Services!.GetRequiredService<McpToolRegistry>().HandleCallToolAsync(ctx, ct));

        await builder.Build().RunAsync().ConfigureAwait(false);

        return 0;
    }
}
