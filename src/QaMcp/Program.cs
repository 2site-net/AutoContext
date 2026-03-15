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
                typeof(CSharpAsyncPatternChecker),
                typeof(CSharpCodingStyleChecker),
                typeof(CSharpMemberOrderingChecker),
                typeof(CSharpNamingConventionsChecker),
                typeof(CSharpNullableContextChecker),
                typeof(CSharpProjectStructureChecker),
                typeof(CSharpTestStyleChecker),
                typeof(NuGetHygieneChecker),
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
