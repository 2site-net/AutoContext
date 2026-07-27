namespace AutoContext.Engine;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Machine;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Composition root for the <c>--mcp-server with-stdio</c> role. Builds a
/// reduced host — the in-process instruction capabilities plus on-demand
/// worker dispatch, and nothing else — and runs it until stdio EOF. No
/// daemon pipes are bound, no <c>engine-registry.json</c> entry is
/// written, and no <c>engine.log</c> is opened; a fault that escapes the
/// host writes a <c>crash.log</c> tombstone, the role's only on-disk
/// artefact. The worker-dispatch pipes
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

        // Tombstone target for faults that escape the host. stdout carries
        // the protocol and stderr is discarded with the process, so without
        // this a crash in this role leaves nothing behind to diagnose.
        var crashOptions = new EngineOptions
        {
            WorkspacePath = options.WorkspacePath,
            InstanceId = ephemeralInstanceId,
            CacheRootOverride = options.CacheRootOverride,
        };
        var crashWriter = new EngineCrashWriter(
            new EngineCacheLayout(new CacheRoot(Options.Create(crashOptions))));

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

            // stdout carries the MCP JSON-RPC transport; operational logs go to
            // stderr only. Clear the default providers Host.CreateApplicationBuilder
            // installs (they would corrupt the protocol stream on stdout).
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(consoleOptions => consoleOptions.LogToStandardErrorThreshold = LogLevel.Trace);
            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            // Runs after the quiet default above, so an explicit --log-level
            // raises stderr detail while stdout stays protocol-only.
            builder.AddMcpServer(target =>
            {
                target.WorkspacePath = options.WorkspacePath;
                target.McpServerMode = options.McpServerMode;
                target.InstanceId = ephemeralInstanceId;
                target.LogLevel = options.LogLevel;
                target.ResourcesRootOverride = options.ResourcesRootOverride;
                target.CorpusRootOverride = options.CorpusRootOverride;
                target.CacheRootOverride = options.CacheRootOverride;
            });

            using var host = builder.Build();
            await host.RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            crashWriter.TryWrite(ex, "McpServerHostFactory.RunAsync");
            throw;
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandled;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTask;
        }
    }
}
