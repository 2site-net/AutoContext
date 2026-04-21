namespace AutoContext.Worker.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
/// <c>McpToolService</c> hosted service, and run the host.
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
    /// <param name="additionalSwitchMappings">Optional extra command-line → configuration-key mappings (e.g. <c>--workspace-root</c>).</param>
    public static HostApplicationBuilder ConfigureWorkerHost(
        this HostApplicationBuilder builder,
        string[] args,
        string readyMarker,
        IReadOnlyDictionary<string, string>? additionalSwitchMappings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(readyMarker);

        var switchMappings = new Dictionary<string, string>
        {
            ["--pipe"] = nameof(WorkerHostOptions.Pipe),
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

        return builder;
    }
}
