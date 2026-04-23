namespace AutoContext.Mcp.Tools.EditorConfig;

using System.Collections.Frozen;
using System.Text.Json;

using AutoContext.Mcp.Tools.Pipe;
using AutoContext.Mcp.Tools.Registry;
using AutoContext.Mcp.Tools.Wire;

/// <summary>
/// Resolves EditorConfig values for one tool invocation in a single
/// batched pipe call to <c>Worker.Workspace</c>'s
/// <c>get_editorconfig_rules</c> task. Sends the union of declared keys
/// across the supplied tasks, then slices the result per task so each
/// task receives only the keys it asked for.
/// </summary>
public sealed class EditorConfigBatcher
{
    /// <summary>The pipe-name value used by Worker.Workspace.</summary>
    public const string DefaultWorkspaceEndpoint = "autocontext.worker-workspace";

    /// <summary>The MCP Task name on Worker.Workspace that resolves EditorConfig keys.</summary>
    public const string ResolveTaskName = "get_editorconfig_rules";

    private readonly WorkerPipeClient _pipeClient;
    private readonly string _workspaceEndpoint;

    public EditorConfigBatcher(WorkerPipeClient pipeClient)
        : this(pipeClient, DefaultWorkspaceEndpoint)
    {
    }

    public EditorConfigBatcher(WorkerPipeClient pipeClient, string workspaceEndpoint)
    {
        ArgumentNullException.ThrowIfNull(pipeClient);
        ArgumentException.ThrowIfNullOrEmpty(workspaceEndpoint);

        _pipeClient = pipeClient;
        _workspaceEndpoint = workspaceEndpoint;
    }

    /// <summary>
    /// Resolves the union of <see cref="McpTaskDefinition.EditorConfig"/> keys
    /// across <paramref name="tasks"/> for <paramref name="filePath"/>,
    /// then returns a per-task slice. Tasks that declared no keys (or
    /// whose declared keys all came back missing) map to an empty
    /// dictionary. On pipe failure every task maps to an empty dictionary
    /// — McpToolClient logs the warning and dispatches anyway, matching
    /// the contract in the architecture doc.
    /// </summary>
    public async Task<EditorConfigBatchResult> ResolveAsync(
        string filePath,
        IReadOnlyList<McpTaskDefinition> tasks,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(tasks);

        var union = CollectUnion(tasks);

        if (union.Length == 0)
        {
            return new EditorConfigBatchResult
            {
                Slices = BuildEmptySlices(tasks),
                ResolutionFailed = false,
                FailureMessage = null,
            };
        }

        var requestData = JsonSerializer.SerializeToElement(
            new { path = filePath, keys = union },
            WireJsonOptions.Instance);

        var request = new TaskWireRequest
        {
            McpTask = ResolveTaskName,
            Data = requestData,
            EditorConfig = FrozenDictionary<string, string>.Empty,
        };

        var response = await _pipeClient
            .InvokeAsync(_workspaceEndpoint, request, ct)
            .ConfigureAwait(false);

        if (!string.Equals(response.McpTask, ResolveTaskName, StringComparison.Ordinal))
        {
            return new EditorConfigBatchResult
            {
                Slices = BuildEmptySlices(tasks),
                ResolutionFailed = true,
                FailureMessage =
                    $"Worker returned response for unexpected task '{response.McpTask}' (expected '{ResolveTaskName}').",
            };
        }

        if (!string.Equals(response.Status, TaskWireResponse.StatusOk, StringComparison.Ordinal))
        {
            return new EditorConfigBatchResult
            {
                Slices = BuildEmptySlices(tasks),
                ResolutionFailed = true,
                FailureMessage = response.Error,
            };
        }

        var resolved = ParseResolvedMap(response.Output);

        return new EditorConfigBatchResult
        {
            Slices = BuildSlices(tasks, resolved),
            ResolutionFailed = false,
            FailureMessage = null,
        };
    }

    private static string[] CollectUnion(IReadOnlyList<McpTaskDefinition> tasks)
    {
        var union = new HashSet<string>(StringComparer.Ordinal);

        foreach (var task in tasks)
        {
            foreach (var key in task.EditorConfig)
            {
                union.Add(key);
            }
        }

        if (union.Count == 0)
        {
            return [];
        }

        // Stable order keeps the wire request deterministic and easier to
        // diff in logs. Ordinal sort matches the comparer above.
        var ordered = new string[union.Count];
        union.CopyTo(ordered);
        Array.Sort(ordered, StringComparer.Ordinal);

        return ordered;
    }

    private static FrozenDictionary<string, IReadOnlyDictionary<string, string>> BuildEmptySlices(
        IReadOnlyList<McpTaskDefinition> tasks)
    {
        var slices = new Dictionary<string, IReadOnlyDictionary<string, string>>(
            tasks.Count,
            StringComparer.Ordinal);

        foreach (var task in tasks)
        {
            slices[task.Name] = FrozenDictionary<string, string>.Empty;
        }

        return slices.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static FrozenDictionary<string, IReadOnlyDictionary<string, string>> BuildSlices(
        IReadOnlyList<McpTaskDefinition> tasks,
        IReadOnlyDictionary<string, string> resolved)
    {
        var slices = new Dictionary<string, IReadOnlyDictionary<string, string>>(
            tasks.Count,
            StringComparer.Ordinal);

        foreach (var task in tasks)
        {
            if (task.EditorConfig.Count == 0)
            {
                slices[task.Name] = FrozenDictionary<string, string>.Empty;
                continue;
            }

            var slice = new Dictionary<string, string>(
                task.EditorConfig.Count,
                StringComparer.Ordinal);

            foreach (var key in task.EditorConfig)
            {
                if (resolved.TryGetValue(key, out var value))
                {
                    slice[key] = value;
                }
            }

            slices[task.Name] = slice.ToFrozenDictionary(StringComparer.Ordinal);
        }

        return slices.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> ParseResolvedMap(JsonElement? output)
    {
        if (output is not { ValueKind: JsonValueKind.Object } element)
        {
            return FrozenDictionary<string, string>.Empty;
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                map[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return map;
    }
}
