namespace AutoContext.Mcp.Shared.Checkers;

using AutoContext.Mcp.Shared.WorkspaceServer;
using AutoContext.Mcp.Shared.WorkspaceServer.McpTools;

/// <summary>
/// Base class for aggregate tools that run multiple sub-checkers and return
/// a single combined report. Handles tool-mode resolution via the workspace
/// service, EditorConfig key aggregation, and data merging.
/// </summary>
public abstract class CompositeChecker(WorkspaceServerClient workspaceServerClient) : IChecker
{
    /// <inheritdoc />
    public abstract string ToolName { get; }

    /// <summary>
    /// Label used in summary messages (e.g. "C#", "Git", "TypeScript").
    /// </summary>
    protected abstract string ToolLabel { get; }

    /// <summary>
    /// Creates the ordered list of sub-checkers to run.
    /// </summary>
    protected abstract IChecker[] CreateCheckers();

    /// <inheritdoc />
    public async Task<string> CheckAsync(
        string content, IReadOnlyDictionary<string, string>? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        await workspaceServerClient.SendLogAsync(
            "Information",
            $"Tool invoked: {ToolName} | content length: {content.Length}").ConfigureAwait(false);

        var checkers = CreateCheckers();
        var filePath = data?.GetValueOrDefault("filePath");

        // Strip filePath and forward the rest as explicit params.
        var extraParams = data?
            .Where(kv => kv.Key != "filePath")
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        if (extraParams?.Count == 0)
        {
            extraParams = null;
        }

        var toolNames = checkers.Select(c => c.ToolName).ToArray();
        var editorConfigKeys = checkers
            .OfType<IEditorConfigFilter>()
            .SelectMany(f => f.EditorConfigKeys)
            .Distinct()
            .ToArray();

        var resolved = await workspaceServerClient
            .ResolveToolsAsync(new McpToolsRequest(
                toolNames,
                filePath,
                editorConfigKeys.Length > 0 ? editorConfigKeys : null))
            .ConfigureAwait(false);

        var sections = new List<string>();

        foreach (var checker in checkers)
        {
            var enabled = resolved?.Tools.GetValueOrDefault(checker.ToolName, true) ?? true;

            if (enabled)
            {
                await workspaceServerClient.SendLogAsync(
                    "Information", $"  Running: {checker.ToolName}").ConfigureAwait(false);
                sections.Add(await checker.CheckAsync(content, MergeData(extraParams, resolved?.EditorConfig)).ConfigureAwait(false));
            }
            else if (checker is IEditorConfigFilter)
            {
                await workspaceServerClient.SendLogAsync(
                    "Information", $"  Running (editorconfig-only): {checker.ToolName}").ConfigureAwait(false);
                var merged = MergeData(extraParams, resolved?.EditorConfig);
                merged["__disabled"] = "true";
                sections.Add(await checker.CheckAsync(content, merged).ConfigureAwait(false));
            }
            else
            {
                await workspaceServerClient.SendLogAsync(
                    "Information", $"  Skipped: {checker.ToolName}").ConfigureAwait(false);
            }
        }

        if (sections.Count == 0)
        {
            return $"⚠️ All {ToolLabel} checks are disabled.";
        }

        var failures = sections.Where(s => s.StartsWith('❌')).ToList();

        if (failures.Count == 0)
        {
            return $"✅ All enabled {ToolLabel} checks passed.";
        }

        return string.Join("\n\n", failures);
    }

    private static Dictionary<string, string> MergeData(
        Dictionary<string, string>? explicitParams,
        Dictionary<string, string>? editorConfigData)
    {
        var merged = new Dictionary<string, string>();

        if (explicitParams is not null)
        {
            foreach (var kv in explicitParams)
            {
                merged[kv.Key] = kv.Value;
            }
        }

        if (editorConfigData is not null)
        {
            foreach (var kv in editorConfigData)
            {
                merged[kv.Key] = kv.Value;
            }
        }

        return merged;
    }
}
