namespace AutoContext.Mcp.Server;

using AutoContext.Mcp.Server.EditorConfig;
using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tools;
using AutoContext.Mcp.Server.Tools.Invocation;
using AutoContext.Mcp.Server.Workers;
using AutoContext.Mcp.Server.Workers.Transport;
using AutoContext.Framework.Hosting;
using AutoContext.Framework.Logging;

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
/// Optional command-line switches:
/// <list type="bullet">
///   <item><c>--endpoint-suffix &lt;value&gt;</c>: appends <c>-&lt;value&gt;</c>
///     to every pipe name the process opens or connects to (used by the
///     end-to-end smoke test to isolate its pipes from any other running
///     instance).</item>
///   <item><c>--log-pipe &lt;name&gt;</c>: streams structured log records
///     to the extension-side LogServer over the given named pipe so they
///     surface in the AutoContext Output channel. When omitted, log
///     records fall back to stderr.</item>
///   <item><c>--health-monitor &lt;name&gt;</c>: connects to the
///     extension-side HealthMonitorServer over the given named pipe and
///     announces this process as the <see cref="HealthClientId"/>
///     ("mcp-server") so the UI can flip its "running" indicator on
///     and off as the host starts/exits. Best-effort; skipped when
///     omitted.</item>
/// </list>
/// </remarks>
internal static partial class Program
{
    /// <summary>Logging client name reported to the extension's LogServer.</summary>
    internal const string LogClientName = "AutoContext.Mcp.Server";

    /// <summary>
    /// Stable identifier this process announces over the
    /// health-monitor pipe. Must match the <c>id</c> the extension's
    /// servers manifest assigns to <c>AutoContext.Mcp.Server</c>.
    /// </summary>
    internal const string HealthClientId = "mcp-server";

    [LoggerMessage(EventId = 1, Level = LogLevel.Critical, Message = "Registry validation failed:")]
    private static partial void LogRegistryValidationFailed(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Critical, Message = "  - {Error}")]
    private static partial void LogRegistryValidationError(ILogger logger, string error);

    internal static async Task<int> Main(string[] args)
    {
        var endpoints = new EndpointOptions { Suffix = ParseSwitch(args, "--endpoint-suffix") };
        var logPipeName = ParseSwitch(args, "--log-pipe");
        var healthMonitorPipeName = ParseSwitch(args, "--health-monitor");

        var registrySource = new RegistryEmbeddedResource();

        using var bootstrapLoggerFactory = LoggerFactory.Create(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("AutoContext.Mcp.Server.Startup");

        var registry = RegistryLoader.Parse(registrySource.Json, "embedded resource", bootstrapLogger);
        var validation = RegistrySchemeValidator.Validate(registrySource.Json, registry, bootstrapLogger);

        var builder = Host.CreateApplicationBuilder(args);

        // Stream structured log records over the extension's LogServer
        // pipe when --log-pipe is supplied (so Mcp.Server logs land in
        // their own AutoContext Output channel alongside the workers).
        // LoggingClient automatically falls back to stderr when the pipe
        // name is null/empty (standalone / smoke runs), so this wiring is
        // safe regardless of how the process is launched.
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Services.AddSingleton(_ => new LoggingClient(logPipeName, LogClientName));
        builder.Services.AddSingleton<ILoggerProvider, PipeLoggerProvider>();

        // Liveness signal to the extension's HealthMonitorServer. The
        // hosted-service contract handles startup/shutdown wiring; the
        // client is a no-op when --health-monitor was not supplied
        // (standalone / smoke runs).
        builder.Services.AddHostedService(sp => new HealthMonitorClient(
            healthMonitorPipeName ?? string.Empty,
            HealthClientId,
            sp.GetRequiredService<ILogger<HealthMonitorClient>>()));

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

    private static string? ParseSwitch(string[] args, string switchName)
    {
        string? parsed = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], switchName, StringComparison.Ordinal))
            {
                continue;
            }

            if (parsed is not null)
            {
                throw new ArgumentException(
                    $"'{switchName}' can only be supplied once.",
                    nameof(args));
            }

            if (i + 1 >= args.Length)
            {
                throw new ArgumentException(
                    $"'{switchName}' was supplied without a value.",
                    nameof(args));
            }

            var value = args[i + 1];
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"'{switchName}' requires a non-empty value.",
                    nameof(args));
            }

            parsed = value.Trim();
            i++;
        }

        return parsed;
    }
}
