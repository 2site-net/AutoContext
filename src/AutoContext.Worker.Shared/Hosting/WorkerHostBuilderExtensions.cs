namespace AutoContext.Worker.Hosting;

using AutoContext.Framework.Hosting;
using AutoContext.Framework.Logging;
using AutoContext.Framework.Workers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Extension methods that apply the standard worker-host bootstrap shared by
/// every <c>AutoContext.Worker.*</c> process. Parses the unified
/// <c>--instance-id</c> + <c>--service</c> CLI surface, computes the
/// worker's listen address from its <c>workerId</c> + the parsed
/// <c>--instance-id</c>, and binds <see cref="WorkerHostOptions"/> from
/// the merged configuration.
/// </summary>
/// <remarks>
/// Workers still own their entry point — they create the
/// <see cref="HostApplicationBuilder"/>, call
/// <see cref="ConfigureWorkerHost"/>, register their own
/// <see cref="Mcp.IMcpTask"/> implementations, add the
/// <c>WorkerTaskDispatcherService</c> hosted service, and run the host.
/// </remarks>
public static class WorkerHostBuilderExtensions
{
    private const string LogServiceRole = "log";
    private const string HealthMonitorServiceRole = "health-monitor";

    /// <summary>
    /// Wires the standard worker-host configuration onto
    /// <paramref name="builder"/> and returns it for fluent chaining.
    /// </summary>
    /// <param name="builder">The host builder being configured.</param>
    /// <param name="args">Process command-line arguments.</param>
    /// <param name="readyMarker">Stderr ready-marker emitted once the pipe server is accepting connections.</param>
    /// <param name="workerId">
    /// Stable identifier this worker uses to announce itself to the
    /// extension's <c>HealthMonitorServer</c> (e.g. <c>"dotnet"</c>,
    /// <c>"workspace"</c>). Hard-coded by each worker's entry point and
    /// must match the <c>workerId</c> referenced by the extension's
    /// MCP-tools manifest. Also used to format the worker's listen
    /// address (<c>autocontext.worker-&lt;workerId&gt;#&lt;instance-id&gt;</c>).
    /// </param>
    /// <param name="additionalSwitchMappings">Optional extra command-line → configuration-key mappings (e.g. <c>--workspace-root</c>).</param>
    public static HostApplicationBuilder ConfigureWorkerHost(
        this HostApplicationBuilder builder,
        string[] args,
        string readyMarker,
        string workerId,
        IReadOnlyDictionary<string, string>? additionalSwitchMappings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(readyMarker);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        // Parse the unified switch surface (--instance-id + --service
        // role=address pairs). Anything else is left for the optional
        // additionalSwitchMappings pass below.
        var instanceId = ParseSingleValueSwitch(args, "--instance-id");
        var services = ParseServiceSwitches(args);

        services.TryGetValue(LogServiceRole, out var logServiceAddress);
        services.TryGetValue(HealthMonitorServiceRole, out var healthMonitorServiceAddress);

        // Worker self-formats its listen address from its compile-time
        // workerId + the parsed instance-id. Single source of truth:
        // the extension generates instance-id, every process derives
        // every address from it via ServiceAddressFormatter.
        var listenAddress = ServiceAddressFormatter.Format($"worker-{workerId}", instanceId);

        if (additionalSwitchMappings is not null && additionalSwitchMappings.Count > 0)
        {
            builder.Configuration.AddCommandLine(args, new Dictionary<string, string>(additionalSwitchMappings));
        }

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [nameof(WorkerHostOptions.Pipe)] = listenAddress,
            [nameof(WorkerHostOptions.ReadyMarker)] = readyMarker,
            [nameof(WorkerHostOptions.LogServiceAddress)] = logServiceAddress ?? string.Empty,
            [nameof(WorkerHostOptions.HealthMonitorServiceAddress)] = healthMonitorServiceAddress ?? string.Empty,
        });

        builder.Services.Configure<WorkerHostOptions>(builder.Configuration);

        // Replace the default logging providers (which target stdout — never
        // read by the parent process and capable of blocking the worker once
        // the OS pipe buffer fills) with a single PipeLoggerProvider.
        // The provider streams structured records over the LogServer's
        // service address when one was supplied via --service log=...,
        // and falls back to writing to stderr (where the parent's stderr
        // line-handler picks them up) when it isn't — so workers stay
        // diagnosable in standalone runs too.
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<WorkerHostOptions>>().Value;
            var env = sp.GetRequiredService<IHostEnvironment>();
            return new LoggingClient(options.LogServiceAddress, env.ApplicationName);
        });
        builder.Services.AddSingleton<ILoggerProvider, PipeLoggerProvider>();

        // Liveness signal to the extension's HealthMonitorServer. The
        // hosted-service contract handles startup/shutdown wiring; the
        // client is a no-op when --service health-monitor=... was not
        // supplied.
        builder.Services.AddHostedService(sp => new HealthMonitorClient(
            sp.GetRequiredService<IOptions<WorkerHostOptions>>().Value.HealthMonitorServiceAddress,
            workerId,
            sp.GetRequiredService<ILogger<HealthMonitorClient>>()));

        return builder;
    }

    private static string? ParseSingleValueSwitch(string[] args, string switchName)
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
