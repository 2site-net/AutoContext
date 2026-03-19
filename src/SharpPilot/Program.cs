namespace SharpPilot;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SharpPilot.Tools.DotNet;
using SharpPilot.Tools.EditorConfig;
using SharpPilot.Tools.Git;

internal sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        var scope = builder.Configuration["scope"]
            ?? throw new ArgumentException("Missing required argument: --scope (dotnet|git|editorconfig)");

        Type[] toolTypes = scope switch
        {
            "dotnet" =>
            [
                typeof(DotNetChecker),
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
                typeof(GitChecker),
                typeof(CommitContentChecker),
                typeof(CommitFormatChecker),
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
