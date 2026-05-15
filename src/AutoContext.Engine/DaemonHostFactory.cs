namespace AutoContext.Engine;

using AutoContext.Engine.Core;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Composes and runs the daemon-role <see cref="IHost"/> for the
/// engine binary. Wires
/// <see cref="EngineHostBuilderExtensions.AddAutoContextEngine"/>
/// with the argv-parsed options, then runs the host until the OS
/// signals shutdown.
/// </summary>
/// <remarks>
/// At this point in the rollout
/// (<c>docs/autocontext-engine-implementation-plan.md</c> Phase 1
/// commit #4) the host has no hosted services beyond options
/// validation — pipe accept loops, watchdogs, and RPC handlers
/// land in the subsequent Phase 1 commits. The host therefore
/// blocks on its root cancellation token until SIGTERM, exactly
/// as a fully-wired engine would.
/// </remarks>
internal static class DaemonHostFactory
{
    public static async Task<int> RunAsync(EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = Host.CreateApplicationBuilder();
        builder.AddAutoContextEngine(target =>
        {
            target.CorpusRootOverride = options.CorpusRootOverride;
            target.IdleTimeout = options.IdleTimeout;
            target.InstanceId = options.InstanceId;
            target.InstanceLabel = options.InstanceLabel;
            target.Logging = options.Logging;
            target.McpServerMode = options.McpServerMode;
            target.ParentProcessId = options.ParentProcessId;
            target.Retention = options.Retention;
            target.WorkspacePath = options.WorkspacePath;
        });

        using var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
        return 0;
    }
}
