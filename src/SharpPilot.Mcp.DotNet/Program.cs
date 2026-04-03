namespace SharpPilot.Mcp.DotNet;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SharpPilot.Mcp.Shared.EditorConfig;
using SharpPilot.Mcp.DotNet.Tools.Checkers;
using SharpPilot.Mcp.DotNet.Tools.Checkers.CSharp;

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
            ?? throw new ArgumentException("Missing required argument: --scope dotnet");

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
            _ => throw new ArgumentException($"Unknown scope '{scope}'. Valid value: dotnet."),
        };

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools(toolTypes);

        await builder.Build().RunAsync().ConfigureAwait(false);
    }
}
