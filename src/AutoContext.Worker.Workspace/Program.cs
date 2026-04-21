namespace AutoContext.Worker.Workspace;

using AutoContext.Mcp;
using AutoContext.Worker.Hosting;
using AutoContext.Worker.Workspace.Hosting;
using AutoContext.Worker.Workspace.Tasks.Config;
using AutoContext.Worker.Workspace.Tasks.EditorConfig;
using AutoContext.Worker.Workspace.Tasks.Git;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// <c>AutoContext.Worker.Workspace</c> entry point. Standalone process that
/// owns the workspace-side MCP Tasks (git checks, editorconfig resolution,
/// <c>.autocontext.json</c> reading) and serves them over a named pipe.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Stderr ready-marker used by the parent (extension) process to detect
    /// that this worker's pipe server is accepting connections.
    /// </summary>
    internal const string ReadyMarker = "[AutoContext.Worker.Workspace] Ready.";

    internal static Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args)
            .ConfigureWorkerHost(args, ReadyMarker, new Dictionary<string, string>
            {
                ["--workspace-root"] = nameof(WorkerOptions.WorkspaceRoot),
            });

        builder.Services.Configure<WorkerOptions>(builder.Configuration);

        builder.Services.AddSingleton<IMcpTask, AnalyzeGitCommitFormatTask>();
        builder.Services.AddSingleton<IMcpTask, AnalyzeGitCommitContentTask>();
        builder.Services.AddSingleton<IMcpTask, GetEditorConfigRulesTask>();
        builder.Services.AddSingleton<IMcpTask, GetAutoContextConfigFileTask>();

        builder.Services.AddHostedService<McpToolService>();

        return builder.Build().RunAsync();
    }
}

