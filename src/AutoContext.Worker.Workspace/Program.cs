namespace AutoContext.Worker.Workspace;

using AutoContext.Mcp;
using AutoContext.Framework.Workers;
using AutoContext.Worker.Hosting;
using AutoContext.Worker.Workspace.Hosting;
using AutoContext.Worker.Workspace.Tasks.Config;
using AutoContext.Worker.Workspace.Tasks.EditorConfig;
using AutoContext.Worker.Workspace.Tasks.Git;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// <c>AutoContext.Worker.Workspace</c> entry point. Standalone process that
/// owns the workspace-side MCP Tasks (git checks, editorconfig resolution,
/// <c>.autocontext.json</c> reading) and serves them over a named pipe.
/// </summary>
internal static partial class Program
{
    /// <summary>
    /// Stderr ready-marker used by the parent (extension) process to detect
    /// that this worker's pipe server is accepting connections.
    /// </summary>
    internal const string ReadyMarker = "[AutoContext.Worker.Workspace] Ready.";

    /// <summary>
    /// Stable identifier this worker uses to announce itself to the
    /// extension's <c>HealthMonitorServer</c>. Must match the
    /// <c>workerId</c> referenced by the extension's MCP-tools manifest.
    /// </summary>
    internal const string WorkerId = "workspace";

    internal static Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args)
            .ConfigureWorkerHost(args, ReadyMarker, WorkerId, new Dictionary<string, string>
            {
                ["--workspace-root"] = nameof(WorkerOptions.WorkspaceRoot),
            });

        builder.Services.Configure<WorkerOptions>(builder.Configuration);

        builder.Services.AddSingleton<IMcpTask, AnalyzeGitCommitFormatTask>();
        builder.Services.AddSingleton<IMcpTask, AnalyzeGitCommitContentTask>();
        builder.Services.AddSingleton<IMcpTask, GetEditorConfigRulesTask>();
        builder.Services.AddSingleton<IMcpTask, GetAutoContextConfigFileTask>();

        builder.Services.AddHostedService<WorkerTaskDispatcherService>();

        var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Program).FullName!);
        LogRegisteredTasks(
            logger,
            string.Join(", ",
                nameof(AnalyzeGitCommitFormatTask),
                nameof(AnalyzeGitCommitContentTask),
                nameof(GetEditorConfigRulesTask),
                nameof(GetAutoContextConfigFileTask)));

        return host.RunAsync();
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Registered MCP tasks: {Tasks}")]
    private static partial void LogRegisteredTasks(ILogger logger, string tasks);
}

