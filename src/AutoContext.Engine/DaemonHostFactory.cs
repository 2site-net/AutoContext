namespace AutoContext.Engine;

using System.Diagnostics.CodeAnalysis;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Machine;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Composes and runs the daemon-role <see cref="IHost"/> for the
/// engine binary. Wires
/// <see cref="EngineHostBuilderExtensions.AddAutoContextEngine"/>
/// with the argv-parsed options, installs the three
/// unhandled-exception sinks that feed
/// <see cref="EngineCrashWriter"/>, then runs the host until the
/// OS signals shutdown.
/// </summary>
/// <remarks>
/// The crash sinks are scoped to <see cref="RunAsync"/>'s
/// lifetime: process-wide handlers are subscribed before the
/// host runs and unsubscribed in a <c>finally</c> so test hosts
/// and embedders that compose the engine in-process do not
/// accumulate stale subscriptions. The MCP-stdio role
/// (<see cref="McpServerHostFactory"/>) deliberately does not
/// install crash sinks — the per-instance subtree is a
/// daemon-mode artefact, and the lightweight MCP stub has no
/// pinned workspace state worth tombstoning. A future iteration
/// may introduce a shared <c>crash-mcp.log</c> for that role.
/// </remarks>
internal static class DaemonHostFactory
{
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Top-level catch funnels every unhandled host exception through EngineCrashWriter before re-throwing; the original fault is preserved verbatim by the bare 'throw;' so the process still exits with its native non-zero code.")]
    public static async Task<int> RunAsync(EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Compose the same per-instance layout DI hands the
        // in-host EngineCrashWriter. We can't reach into the host
        // service provider here because the writer must exist
        // before the host is built, in order to tombstone faults
        // raised during construction itself.
        var cacheLayout = new EngineCacheLayout(new CacheRoot(Options.Create(options)));
        var crashWriter = new EngineCrashWriter(cacheLayout);

        void OnAppDomainUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception
                ?? new InvalidOperationException(
                    $"Non-CLR exception object on AppDomain.UnhandledException: '{e.ExceptionObject}'.");
            crashWriter.TryWrite(exception, "AppDomain.UnhandledException");
        }

        void OnUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            crashWriter.TryWrite(e.Exception, "TaskScheduler.UnobservedTaskException");
        }

        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandled;
        TaskScheduler.UnobservedTaskException += OnUnobservedTask;

        try
        {
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
        catch (Exception ex)
        {
            crashWriter.TryWrite(ex, "DaemonHostFactory.RunAsync");
            throw;
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandled;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTask;
        }
    }
}
