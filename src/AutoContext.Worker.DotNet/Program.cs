namespace AutoContext.Worker.DotNet;

using AutoContext.Mcp;
using AutoContext.Worker.DotNet.Tasks.CSharp;
using AutoContext.Worker.DotNet.Tasks.NuGet;
using AutoContext.Worker.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// <c>AutoContext.Worker.DotNet</c> entry point. Standalone process that
/// owns the .NET-side MCP Tasks (NuGet hygiene, C# code-style checks) and
/// serves them over a named pipe.
/// </summary>
internal static partial class Program
{
    /// <summary>
    /// Stderr ready-marker used by the parent (extension) process to detect
    /// that this worker's pipe server is accepting connections.
    /// </summary>
    internal const string ReadyMarker = "[AutoContext.Worker.DotNet] Ready.";

    internal static Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args)
            .ConfigureWorkerHost(args, ReadyMarker);

        builder.Services.AddSingleton<IMcpTask, AnalyzeNuGetHygieneTask>();
        builder.Services.AddSingleton<IMcpTask, AnalyzeCSharpAsyncPatternsTask>();
        builder.Services.AddSingleton<IMcpTask, AnalyzeCSharpCodingStyleTask>();
        builder.Services.AddSingleton<IMcpTask, AnalyzeCSharpMemberOrderingTask>();
        builder.Services.AddSingleton<IMcpTask, AnalyzeCSharpNamingConventionsTask>();
        builder.Services.AddSingleton<IMcpTask, AnalyzeCSharpNullableContextTask>();
        builder.Services.AddSingleton<IMcpTask, AnalyzeCSharpProjectStructureTask>();
        builder.Services.AddSingleton<IMcpTask, AnalyzeCSharpTestStyleTask>();

        builder.Services.AddHostedService<McpToolService>();

        var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Program).FullName!);
        LogRegisteredTasks(
            logger,
            string.Join(", ",
                nameof(AnalyzeNuGetHygieneTask),
                nameof(AnalyzeCSharpAsyncPatternsTask),
                nameof(AnalyzeCSharpCodingStyleTask),
                nameof(AnalyzeCSharpMemberOrderingTask),
                nameof(AnalyzeCSharpNamingConventionsTask),
                nameof(AnalyzeCSharpNullableContextTask),
                nameof(AnalyzeCSharpProjectStructureTask),
                nameof(AnalyzeCSharpTestStyleTask)));

        return host.RunAsync();
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Registered MCP tasks: {Tasks}")]
    private static partial void LogRegisteredTasks(ILogger logger, string tasks);
}

