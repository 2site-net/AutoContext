namespace AutoContext.Mcp.Server;

using AutoContext.Mcp.Server.Config;
using AutoContext.Mcp.Server.EditorConfig;
using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tools;
using AutoContext.Mcp.Server.Tools.Invocation;
using AutoContext.Mcp.Server.Workers;
using AutoContext.Mcp.Server.Workers.Control;
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
///   <item><c>--instance-id &lt;id&gt;</c>: per-window identifier
///     (12-hex by convention) the orchestrator uses to format every
///     pipe address it dials. Shared with the extension and every
///     spawned worker so processes in one VS Code window agree on
///     pipe addresses while a second window stays isolated. When
///     omitted, addresses fall back to the un-suffixed
///     <c>autocontext.&lt;role&gt;</c> form (standalone runs and
///     smoke-test isolation are achieved by passing a unique id).</item>
///   <item><c>--service &lt;role&gt;=&lt;address&gt;</c>: repeatable.
///     Names an extension-hosted service the orchestrator should
///     dial. Recognised roles:
///     <list type="bullet">
///       <item><c>log</c> — extension-side <c>LogServer</c>;
///         structured log records are streamed there so they surface
///         in the AutoContext Output channel. Falls back to stderr
///         when omitted.</item>
///       <item><c>health-monitor</c> — extension-side
///         <c>HealthMonitorServer</c>; the process announces itself
///         as <see cref="HealthClientId"/> ("mcp-server") so the UI
///         can flip its "running" indicator on and off as the host
///         starts/exits. Best-effort; skipped when omitted.</item>
///       <item><c>worker-control</c> — extension-side
///         <c>WorkerControlServer</c>; the orchestrator asks it to
///         ensure the target worker is running before each tool
///         dispatch. Best-effort; skipped when omitted (standalone
///         runs and smoke tests fall back to the legacy
///         assume-running behaviour).</item>
///       <item><c>extension-config</c> — extension-side
///         <c>AutoContextConfigServer</c>; pushes the latest
///         <c>.autocontext.json</c> disabled-state snapshot so
///         disabled tools are filtered out of <c>tools/list</c> and
///         disabled tasks are skipped during dispatch. Best-effort;
///         skipped when omitted (standalone / smoke runs see no
///         disabled state).</item>
///     </list>
///     Duplicate roles, missing <c>=</c> separators, and empty
///     keys/values throw. Unknown roles are logged at warn level
///     and ignored (forward compatibility).
///   </item>
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

    private const string LogServiceRole = "log";
    private const string HealthMonitorServiceRole = "health-monitor";
    private const string WorkerControlServiceRole = "worker-control";
    private const string ExtensionConfigServiceRole = "extension-config";

    [LoggerMessage(EventId = 1, Level = LogLevel.Critical, Message = "Registry validation failed:")]
    private static partial void LogRegistryValidationFailed(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Critical, Message = "  - {Error}")]
    private static partial void LogRegistryValidationError(ILogger logger, string error);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Ignoring unknown --service role '{Role}'.")]
    private static partial void LogUnknownServiceRole(ILogger logger, string role);

    internal static async Task<int> Main(string[] args)
    {
        var instanceId = ParseSwitch(args, "--instance-id");
        var addresses = new ServiceAddressOptions { InstanceId = instanceId };
        var services = ParseServiceSwitches(args);

        var logServiceAddress = services.TryGetValue(LogServiceRole, out var logAddr) ? logAddr : null;
        var healthMonitorServiceAddress = services.TryGetValue(HealthMonitorServiceRole, out var healthAddr) ? healthAddr : null;
        var workerControlServiceAddress = services.TryGetValue(WorkerControlServiceRole, out var ctrlAddr) ? ctrlAddr : null;
        var extensionConfigServiceAddress = services.TryGetValue(ExtensionConfigServiceRole, out var cfgAddr) ? cfgAddr : null;

        var registrySource = new RegistryEmbeddedResource();

        using var bootstrapLoggerFactory = LoggerFactory.Create(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("AutoContext.Mcp.Server.Startup");

        foreach (var role in services.Keys)
        {
            if (role is not LogServiceRole and not HealthMonitorServiceRole and not WorkerControlServiceRole and not ExtensionConfigServiceRole)
            {
                LogUnknownServiceRole(bootstrapLogger, role);
            }
        }

        var registry = RegistryLoader.Parse(registrySource.Json, "embedded resource", bootstrapLogger);
        var validation = RegistrySchemeValidator.Validate(registrySource.Json, registry, bootstrapLogger);

        var builder = Host.CreateApplicationBuilder(args);

        // Stream structured log records over the extension's LogServer
        // pipe when --service log=<address> is supplied (so Mcp.Server
        // logs land in their own AutoContext Output channel alongside
        // the workers). LoggingClient automatically falls back to
        // stderr when the address is null/empty (standalone / smoke
        // runs), so this wiring is safe regardless of how the process
        // is launched.
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Services.AddSingleton(_ => new LoggingClient(logServiceAddress, LogClientName));
        builder.Services.AddSingleton<ILoggerProvider, PipeLoggerProvider>();

        // Liveness signal to the extension's HealthMonitorServer. The
        // hosted-service contract handles startup/shutdown wiring; the
        // client is a no-op when --service health-monitor=<address>
        // was not supplied (standalone / smoke runs).
        builder.Services.AddHostedService(sp => new HealthMonitorClient(
            healthMonitorServiceAddress ?? string.Empty,
            HealthClientId,
            sp.GetRequiredService<ILogger<HealthMonitorClient>>()));

        // Core graph wired through DI so the host disposes anything
        // that becomes IDisposable in the future and tests can swap
        // implementations without rewriting Main.
        builder.Services.AddSingleton<IRegistrySource>(registrySource);
        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton(addresses);
        builder.Services.AddSingleton(sp => new WorkerControlClient(
            workerControlServiceAddress,
            sp.GetRequiredService<ILogger<WorkerControlClient>>()));
        builder.Services.AddSingleton<WorkerClient>();
        builder.Services.AddSingleton<EditorConfigBatcher>();
        builder.Services.AddSingleton(sp => new ToolInvoker(
            sp.GetRequiredService<WorkerClient>(),
            sp.GetRequiredService<EditorConfigBatcher>(),
            sp.GetRequiredService<AutoContextConfigSnapshot>(),
            sp.GetRequiredService<ILogger<ToolInvoker>>()));
        builder.Services.AddSingleton<McpSdkAdapter>();

        // Subscribes to the extension-side AutoContextConfigServer
        // push channel and keeps AutoContextConfigSnapshot in sync
        // with .autocontext.json. The snapshot drives both the
        // tools/list filter (McpSdkAdapter) and the per-task dispatch
        // filter. Best-effort: when --service extension-config=...
        // was not supplied (standalone / smoke runs) the client is a
        // no-op and the snapshot stays at its default "nothing
        // disabled" value.
        builder.Services.AddSingleton<AutoContextConfigSnapshot>();
        builder.Services.AddHostedService(sp => new AutoContextConfigClient(
            extensionConfigServiceAddress ?? string.Empty,
            sp.GetRequiredService<AutoContextConfigSnapshot>(),
            sp,
            sp.GetRequiredService<ILogger<AutoContextConfigClient>>()));

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

    /// <summary>
    /// Parses every <c>--service &lt;role&gt;=&lt;address&gt;</c>
    /// occurrence into a dictionary keyed by role. Throws on duplicate
    /// roles, missing values, missing <c>=</c> separators, or empty
    /// keys/values.
    /// </summary>
    private static Dictionary<string, string> ParseServiceSwitches(string[] args)
    {
        const string SwitchName = "--service";
        var services = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], SwitchName, StringComparison.Ordinal))
            {
                continue;
            }

            if (i + 1 >= args.Length)
            {
                throw new ArgumentException(
                    $"'{SwitchName}' was supplied without a value.",
                    nameof(args));
            }

            var value = args[i + 1];
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"'{SwitchName}' requires a non-empty value.",
                    nameof(args));
            }

            var separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
            {
                throw new ArgumentException(
                    $"'{SwitchName}' value '{value}' must be in '<role>=<address>' form.",
                    nameof(args));
            }

            var role = value[..separatorIndex].Trim();
            var address = value[(separatorIndex + 1)..].Trim();
            if (role.Length == 0 || address.Length == 0)
            {
                throw new ArgumentException(
                    $"'{SwitchName}' value '{value}' must have a non-empty role and address.",
                    nameof(args));
            }

            if (!services.TryAdd(role, address))
            {
                throw new ArgumentException(
                    $"'{SwitchName} {role}=...' was supplied more than once.",
                    nameof(args));
            }

            i++;
        }

        return services;
    }
}
