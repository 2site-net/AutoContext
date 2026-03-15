namespace QaMcp.Tools.Git;

using System.ComponentModel;

using ModelContextProtocol.Server;

/// <summary>
/// Aggregate tool that runs all enabled Git commit validators and returns
/// a single combined report.
/// </summary>
[McpServerToolType]
public static class GitQaChecker
{
    /// <summary>
    /// Runs all enabled Git commit checks on the supplied commit message.
    /// </summary>
    [McpServerTool(Name = "check_git_commit", ReadOnly = true, Idempotent = true)]
    [Description(
        "Runs all enabled Git commit quality checks on a commit message and returns a combined report. " +
        "Covers commit format (Conventional Commits) and commit content best practices. " +
        "Prefer this over calling individual validate tools unless you only need a specific check.")]
    public static string Check(
        [Description("The full commit message to validate.")]
        string commitMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitMessage);

        var sections = new List<string>();

        if (ToolsStatusConfig.IsEnabled("validate_commit_format"))
        {
            sections.Add(CommitFormatValidator.Validate(commitMessage));
        }

        if (ToolsStatusConfig.IsEnabled("validate_commit_content"))
        {
            sections.Add(CommitContentValidator.Validate(commitMessage));
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
