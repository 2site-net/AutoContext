using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

using SharpPilot.Mcp.Shared.EditorConfig;
using SharpPilot.WorkspaceServer.Features.EditorConfig;
using SharpPilot.WorkspaceServer.Features.Git;
using SharpPilot.WorkspaceServer.Features.McpTools;
using SharpPilot.WorkspaceServer.Services;

var builder = Host.CreateApplicationBuilder(args);

// stdout is reserved for the ready-signal / MCP protocol — send all log
// output to stderr, which the extension captures and forwards to the
// output channel.  Suppress the host's "Application started" banners.
builder.Services.Configure<ConsoleLoggerOptions>(o =>
    o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.Configure<ConsoleLifetimeOptions>(o =>
    o.SuppressStatusMessages = true);

var scope = builder.Configuration["scope"];

if (scope == "editorconfig")
{
    // MCP stdio mode — registers the get_editorconfig MCP tool.
    builder.Services.AddSingleton<EditorConfigResolver>();

    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools([typeof(EditorConfigTool)]);

    await builder.Build().RunAsync().ConfigureAwait(false);
}
else if (scope == "git")
{
    // MCP stdio mode — registers Git commit quality check tools.
    var workspace = builder.Configuration["workspace"];
    var workspacePipe = builder.Configuration["workspace-server"];

    if (workspacePipe is not null)
    {
        EditorConfigReader.Configure(workspacePipe, workspace);
    }

    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools([typeof(GitChecker)]);

    await builder.Build().RunAsync().ConfigureAwait(false);
}
else
{
    // Named pipe mode — workspace service for EditorConfig resolution
    // and MCP tool orchestration.
    builder.Services.AddSingleton<EditorConfigResolver>();
    builder.Services.AddSingleton<McpToolsConfig>();
    builder.Services.AddSingleton<IRequestHandler, EditorConfigRequestHandler>();
    builder.Services.AddSingleton<IRequestHandler, McpToolsRequestHandler>();
    builder.Services.AddHostedService<WorkspaceService>();

    var host = builder.Build();

    // Signal readiness — the extension reads this to know the pipe is active.
    var pipeName = builder.Configuration["pipe"]
        ?? throw new InvalidOperationException("Missing required argument: --pipe");

    Console.WriteLine(JsonSerializer.Serialize(
        new { pipe = pipeName },
        WorkspaceService.JsonOptions));

    await host.RunAsync().ConfigureAwait(false);
}
