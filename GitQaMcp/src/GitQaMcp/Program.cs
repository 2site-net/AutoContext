namespace GitQaMcp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync().ConfigureAwait(false);
    }
}
