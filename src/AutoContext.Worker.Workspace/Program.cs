namespace AutoContext.Worker.Workspace;

using AutoContext.Mcp;
using AutoContext.Worker.Hosting;
using AutoContext.Worker.Workspace.Hosting;
using AutoContext.Worker.Workspace.Tasks.Config;
using AutoContext.Worker.Workspace.Tasks.EditorConfig;
using AutoContext.Worker.Workspace.Tasks.Git;

using Microsoft.Extensions.Configuration;
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
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
        {
            ["--pipe"] = nameof(WorkerHostOptions.Pipe),
            ["--workspace-root"] = nameof(WorkerOptions.WorkspaceRoot),
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Seed the ready marker via configuration so the binder can populate
            // WorkerHostOptions.ReadyMarker (an init-only property). A
            // Configure(action) lambda would fail with CS8852 because init-only
            // setters can't be invoked from outside an object initializer.
            [nameof(WorkerHostOptions.ReadyMarker)] = ReadyMarker,
        });

        builder.Services.Configure<WorkerHostOptions>(builder.Configuration);
        builder.Services.Configure<WorkerOptions>(builder.Configuration);

        builder.Services.AddSingleton<IMcpTask, AnalyzeGitCommitFormatTask>();
        builder.Services.AddSingleton<IMcpTask, AnalyzeGitCommitContentTask>();
        builder.Services.AddSingleton<IMcpTask, GetEditorConfigRulesTask>();
        builder.Services.AddSingleton<IMcpTask, GetAutoContextConfigFileTask>();

        builder.Services.AddHostedService<McpToolService>();

        var host = builder.Build();

        return host.RunAsync();
    }
}
