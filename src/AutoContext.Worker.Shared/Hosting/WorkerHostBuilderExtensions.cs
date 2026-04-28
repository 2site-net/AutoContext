namespace AutoContext.Worker.Hosting;

using AutoContext.Framework.Hosting;
using AutoContext.Framework.Logging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Extension methods that apply the standard worker-host bootstrap shared by
/// every <c>AutoContext.Worker.*</c> process. Maps <c>--pipe</c> (plus any
/// caller-supplied switches) onto <see cref="WorkerHostOptions"/>, seeds the
/// ready marker via in-memory configuration, and binds
/// <see cref="WorkerHostOptions"/> from the merged configuration.
/// </summary>
/// <remarks>
/// Workers still own their entry point — they create the
/// <see cref="HostApplicationBuilder"/>, call
/// <see cref="ConfigureWorkerHost"/>, register their own
/// <see cref="Mcp.IMcpTask"/> implementations, add the
/// <c>McpTaskDispatcherService</c> hosted service, and run the host.
/// <para>
/// The ready marker is seeded via <c>AddInMemoryCollection</c> rather than
/// <c>services.Configure(action)</c> because <see cref="WorkerHostOptions.ReadyMarker"/>
/// is init-only — assigning it from a configure-action lambda would fail
/// with CS8852.
/// </para>
/// </remarks>
public static class WorkerHostBuilderExtensions
{
    /// <summary>
    /// Wires the standard worker-host configuration onto
    /// <paramref name="builder"/> and returns it for fluent chaining.
    /// </summary>
    /// <param name="builder">The host builder being configured.</param>
    /// <param name="args">Process command-line arguments (passed through to <c>AddCommandLine</c>).</param>
    /// <param name="readyMarker">Stderr ready-marker emitted once the pipe server is accepting connections.</param>
    /// <param name="workerId">
    /// Stable identifier this worker uses to announce itself to the
    /// extension's <c>HealthMonitorServer</c> (e.g. <c>"dotnet"</c>,
    /// <c>"workspace"</c>). Hard-coded by each worker's entry point and
    /// must match the <c>workerId</c> referenced by the extension's
    /// MCP-tools manifest.
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

        var switchMappings = new Dictionary<string, string>
        {
            ["--pipe"] = nameof(WorkerHostOptions.Pipe),
            ["--log-pipe"] = nameof(WorkerHostOptions.LogPipe),
            ["--health-monitor"] = nameof(WorkerHostOptions.HealthMonitor),
        };

        if (additionalSwitchMappings is not null)
        {
            foreach (var (key, value) in additionalSwitchMappings)
            {
                switchMappings[key] = value;
            }
        }

        builder.Configuration.AddCommandLine(args, switchMappings);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [nameof(WorkerHostOptions.ReadyMarker)] = readyMarker,
        });

        builder.Services.Configure<WorkerHostOptions>(builder.Configuration);

        // Replace the default logging providers (which target stdout — never
        // read by the parent process and capable of blocking the worker once
        // the OS pipe buffer fills) with a single PipeLoggerProvider.
        // The provider streams structured records over the --log-pipe named
        // pipe when one is supplied, and falls back to writing to stderr
        // (where the parent's stderr line-handler picks them up) when it
        // isn't — so workers stay diagnosable in standalone runs too.
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<WorkerHostOptions>>().Value;
            var env = sp.GetRequiredService<IHostEnvironment>();
            return new LoggingClient(options.LogPipe, env.ApplicationName);
        });
        builder.Services.AddSingleton<ILoggerProvider, PipeLoggerProvider>();

        // Liveness signal to the extension's HealthMonitorServer. The
        // hosted-service contract handles startup/shutdown wiring; the
        // client is a no-op when --health-monitor was not supplied.
        builder.Services.AddHostedService(sp => new HealthMonitorClient(
            sp.GetRequiredService<IOptions<WorkerHostOptions>>().Value.HealthMonitor,
            workerId,
            sp.GetRequiredService<ILogger<HealthMonitorClient>>()));

        return builder;
    }
}
