namespace QaMcp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using QaMcp.Tools.DotNet;
using QaMcp.Tools.Git;

internal sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        var scope = builder.Configuration["scope"]
            ?? throw new ArgumentException("Missing required argument: --scope (dotnet|git)");

        Type[] toolTypes = scope switch
        {
            "dotnet" =>
            [
                typeof(DotNetQaChecker),
                typeof(AsyncPatternChecker),
                typeof(CodeStyleChecker),
                typeof(MemberOrderingChecker),
                typeof(NamingConventionsChecker),
                typeof(NuGetHygieneChecker),
                typeof(NullableContextChecker),
                typeof(ProjectStructureChecker),
                typeof(TestStyleChecker),
            ],
            "git" =>
            [
                typeof(GitQaChecker),
                typeof(CommitContentChecker),
                typeof(CommitFormatChecker),
            ],
            _ => throw new ArgumentException($"Unknown scope '{scope}'. Valid values: dotnet, git."),
        };

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools(toolTypes);

        await builder.Build().RunAsync().ConfigureAwait(false);
    }
}
