namespace AutoContext.Mcp.DotNet;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using AutoContext.Mcp.Shared;
using AutoContext.Mcp.Shared.WorkspaceServer;
using AutoContext.Mcp.DotNet.Tools.NuGet;
using AutoContext.Mcp.DotNet.Tools.CSharp;

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

        var scope = builder.Configuration["scope"]
            ?? throw new ArgumentException("Missing required argument: --scope dotnet");

        var workspaceFolder = builder.Configuration["workspace-folder"];

        var workspaceServer = builder.Configuration["workspace-server"];

        builder.Services.AddSingleton(new WorkspaceServerClient(workspaceServer, "DotNet"));

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

        var healthPipe = builder.Configuration["health-monitor"];
        HealthMonitorClient? healthClient = null;

        if (healthPipe is not null)
        {
            try
            {
                healthClient = new HealthMonitorClient();
                await healthClient.ConnectAsync(healthPipe, scope).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or IOException)
            {
                healthClient?.Dispose();
                healthClient = null;
            }
        }

        try
        {
            await builder.Build().RunAsync().ConfigureAwait(false);
        }
        finally
        {
            healthClient?.Dispose();
        }
    }
}
