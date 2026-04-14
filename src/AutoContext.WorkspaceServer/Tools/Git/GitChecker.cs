namespace AutoContext.WorkspaceServer.Tools.Git;

using System.ComponentModel;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol.Server;

using AutoContext.Mcp.Shared.WorkspaceServer;
using AutoContext.Mcp.Shared.Checkers;

/// <summary>
/// Aggregate tool that runs all enabled Git commit validators and returns
/// a single combined report.
/// </summary>
[McpServerToolType]
public sealed partial class GitChecker(WorkspaceServerClient workspaceServerClient, ILogger<GitChecker>? logger = null)
    : CompositeChecker(workspaceServerClient, logger ?? NullLogger<GitChecker>.Instance)
{
    /// <inheritdoc />
    public override string ToolName
        => "check_git_all";

    /// <inheritdoc />
    protected override string ToolLabel
        => "Git";

    /// <inheritdoc />
    protected override IChecker[] CreateCheckers() =>
    [
        new CommitFormatChecker(),
        new CommitContentChecker(),
    ];

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
        => await CheckAsync(content, data: null).ConfigureAwait(false);
}
