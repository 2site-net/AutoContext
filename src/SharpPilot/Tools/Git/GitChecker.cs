namespace SharpPilot.Tools.Git;

using System.ComponentModel;
using System.Text.Json.Nodes;

using ModelContextProtocol.Server;

/// <summary>
/// Aggregate tool that runs all enabled Git commit validators and returns
/// a single combined report.
/// </summary>
[McpServerToolType]
public sealed class GitChecker : IChecker
{
    /// <inheritdoc />
    public string ToolName
        => "check_git_commit";

    /// <summary>
    /// Runs all enabled Git commit checks on the supplied commit message.
    /// </summary>
    [McpServerTool(Name = "check_git_commit", ReadOnly = true, Idempotent = true)]
    [Description(
        "Runs all enabled Git commit quality checks on a commit message and returns a combined report. " +
        "Covers commit format (Conventional Commits) and commit content best practices. " +
        "Prefer this over calling individual validate tools unless you only need a specific check.")]
    public string Check(
        [Description("The full commit message to validate.")]
        string content,
        JsonObject? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

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
                sections.Add(checker.Check(content));
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
}
