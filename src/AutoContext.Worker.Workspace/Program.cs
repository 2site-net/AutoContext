namespace AutoContext.Worker.Workspace;

using AutoContext.Mcp.Abstractions;
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
    internal static Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
        {
            ["--pipe"] = nameof(WorkerOptions.Pipe),
            ["--workspace-root"] = nameof(WorkerOptions.WorkspaceRoot),
        });

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
