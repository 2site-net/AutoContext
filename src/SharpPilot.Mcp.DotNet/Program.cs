namespace SharpPilot.Mcp.DotNet;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SharpPilot.Mcp.DotNet.Tools.Checkers.DotNet;
using SharpPilot.Mcp.DotNet.Tools.Checkers.DotNet.CSharp;
using SharpPilot.Mcp.DotNet.Tools.Checkers.Git;
using SharpPilot.Mcp.DotNet.Tools.EditorConfig;

internal sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.Logging.SetMinimumLevel(LogLevel.None);
        builder.Logging.AddFilter("SharpPilot", LogLevel.Information);

        var scope = builder.Configuration["scope"]
            ?? throw new ArgumentException("Missing required argument: --scope (dotnet|git|editorconfig)");

        var workspace = builder.Configuration["workspace"];

        var workspacePipe = builder.Configuration["workspace-server"];
        if (workspacePipe is not null)
        {
            EditorConfigReader.Configure(workspacePipe, workspace);
        }

        Type[] toolTypes = scope switch
        {
            "dotnet" =>
            [
                typeof(CSharpChecker),
                typeof(NuGetHygieneChecker),
            ],
            "git" =>
            [
                typeof(GitChecker),
            ],
            "editorconfig" =>
            [
                typeof(EditorConfigReader),
            ],
            _ => throw new ArgumentException($"Unknown scope '{scope}'. Valid values: dotnet, git, editorconfig."),
        };

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools(toolTypes);

        await builder.Build().RunAsync().ConfigureAwait(false);
    }
}
