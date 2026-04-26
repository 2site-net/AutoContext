namespace AutoContext.Mcp.Server;

using AutoContext.Mcp.Server.EditorConfig;
using AutoContext.Mcp.Server.Hosting;
using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tools;
using AutoContext.Mcp.Server.Tools.Invocation;
using AutoContext.Mcp.Server.Workers;
using AutoContext.Mcp.Server.Workers.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// <c>AutoContext.Mcp.Server</c> entry point. Standalone process that
/// loads the embedded <c>mcp-workers-registry.json</c> registry and exposes
/// every declared MCP Tool over MCP/stdio (dispatching each call to the
/// appropriate <c>AutoContext.Worker.*</c> over a named pipe).
/// </summary>
/// <remarks>
/// Accepts one optional switch: <c>--endpoint-suffix &lt;value&gt;</c>.
/// When supplied, every pipe name the process opens or connects to has
/// <c>-&lt;value&gt;</c> appended — this is how the end-to-end smoke
/// test isolates its pipes from any other running instance.
/// </remarks>
internal static partial class Program
{
    /// <summary>Stderr ready-marker written once the host has started.</summary>
    internal const string ReadyMarker = "[AutoContext.Mcp.Server] Ready.";

    [LoggerMessage(EventId = 1, Level = LogLevel.Critical, Message = "Registry validation failed:")]
    private static partial void LogRegistryValidationFailed(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Critical, Message = "  - {Error}")]
    private static partial void LogRegistryValidationError(ILogger logger, string error);

    internal static async Task<int> Main(string[] args)
    {
        var endpoints = new EndpointOptions { Suffix = ParseEndpointSuffix(args) };

        var registrySource = new RegistryEmbeddedResource();
        var registry = RegistryLoader.Parse(registrySource.Json);
        var validation = RegistrySchemeValidator.Validate(registrySource.Json, registry);

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
        builder.Services.AddSingleton<IRegistrySource>(registrySource);
        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton(endpoints);
        builder.Services.AddSingleton<WorkerClient>();
        builder.Services.AddSingleton<EditorConfigBatcher>();
        builder.Services.AddSingleton<ToolInvoker>();
        builder.Services.AddSingleton<McpSdkAdapter>();

        builder.Services.AddHostedService(_ => new ReadyMarkerService(ReadyMarker));

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithListToolsHandler((ctx, ct) =>
                ctx.Server.Services!.GetRequiredService<McpSdkAdapter>().HandleListToolsAsync(ctx, ct))
            .WithCallToolHandler((ctx, ct) =>
                ctx.Server.Services!.GetRequiredService<McpSdkAdapter>().HandleCallToolAsync(ctx, ct));

        var host = builder.Build();

        if (!validation.IsValid)
        {
            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AutoContext.Mcp.Server");

            LogRegistryValidationFailed(logger);

            foreach (var error in validation.Errors)
            {
                LogRegistryValidationError(logger, error);
            }

            await ((IAsyncDisposable)host).DisposeAsync().ConfigureAwait(false);

            return 1;
        }

        await host.RunAsync().ConfigureAwait(false);

        return 0;
    }

    private static string? ParseEndpointSuffix(string[] args)
    {
        string? parsedSuffix = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--endpoint-suffix", StringComparison.Ordinal))
            {
                continue;
            }

            if (parsedSuffix is not null)
            {
                throw new ArgumentException(
                    "'--endpoint-suffix' can only be supplied once.",
                    nameof(args));
            }

            if (i + 1 >= args.Length)
            {
                throw new ArgumentException(
                    "'--endpoint-suffix' was supplied without a value.",
                    nameof(args));
            }

            var value = args[i + 1];
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "'--endpoint-suffix' requires a non-empty value.",
                    nameof(args));
            }

            parsedSuffix = value.Trim();
            i++;
        }

        return parsedSuffix;
    }
}
