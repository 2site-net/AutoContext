namespace SharpPilot.Mcp.DotNet.Tools.Checkers.Git;

using System.ComponentModel;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

using SharpPilot.Mcp.DotNet.Configuration;
using SharpPilot.Mcp.DotNet.Tools.Checkers;

/// <summary>
/// Aggregate tool that runs all enabled Git commit validators and returns
/// a single combined report.
/// </summary>
[McpServerToolType]
public sealed partial class GitChecker(ILogger<GitChecker> logger) : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "check_git_all";

    Task<string> IChecker.CheckAsync(string content, IReadOnlyDictionary<string, string>? data)
        => CheckAsync(content);

    /// <summary>
    /// Runs all enabled Git commit checks on the supplied commit message.
    /// </summary>
    [McpServerTool(Name = "check_git_all", ReadOnly = true, Idempotent = true)]
    [Description(
        "Runs all enabled Git quality checks and returns a combined report. " +
        "Currently covers commit format (Conventional Commits) and commit content best practices. " +
        "Prefer this over calling individual check tools unless you only need a specific check.")]
    public async Task<string> CheckAsync(
        [Description("The full commit message to validate.")]
        string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        LogToolInvoked(logger, ToolName, content.Length);

        var sections = new List<string>();

        IChecker[] checkers =
        [
            new CommitFormatChecker(),
            new CommitContentChecker(),
        ];

        foreach (var checker in checkers)
        {
            if (ToolsStatusConfig.IsEnabled(checker.ToolName))
            {
                sections.Add(await checker.CheckAsync(content).ConfigureAwait(false));
            }
        }

        if (sections.Count == 0)
        {
            return "⚠️ All Git checks are disabled.";
        }

        var failures = sections.Where(s => s.StartsWith('❌')).ToList();

        if (failures.Count == 0)
        {
            return "✅ All enabled Git checks passed.";
        }

        return string.Join("\n\n", failures);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Tool invoked: {ToolName} | content length: {ContentLength}")]
    private static partial void LogToolInvoked(ILogger logger, string toolName, int contentLength);
}
