namespace AutoContext.Mcp.Tools.Dispatch;

using System.Collections.Frozen;
using System.Diagnostics;
using System.Text.Json;

using AutoContext.Mcp.Tools.EditorConfig;
using AutoContext.Mcp.Tools.Envelope;
using AutoContext.Mcp.Tools.Manifest;
using AutoContext.Mcp.Tools.Pipe;
using AutoContext.Mcp.Tools.Wire;

/// <summary>
/// Orchestrates one MCP-Tool invocation. Resolves EditorConfig values for
/// the tool's tasks in a single batched call, dispatches each task by
/// <see cref="ManifestTask.Priority"/> over a named pipe, and composes
/// the per-task wire responses into a uniform
/// <see cref="ToolResultEnvelope"/>. Pure orchestration — no MCP SDK.
/// </summary>
/// <remarks>
/// Dispatch ordering follows the architecture-doc rule:
/// nonzero <see cref="ManifestTask.Priority"/> values are grouped, sorted
/// ascending, and run concurrently within each group;
/// <c>0</c>/omitted-priority tasks run sequentially after every prioritized
/// group. The order of entries in the resulting
/// <see cref="ToolResultEnvelope.Result"/> always matches the declared
/// order in <see cref="ManifestTool.Tasks"/>, regardless of execution order.
/// </remarks>
public sealed class ToolInvoker
{
    /// <summary>The <c>data</c> property name probed when resolving EditorConfig.</summary>
    public const string PathParameterName = "originalPath";

    private static readonly IReadOnlyDictionary<string, string> EmptyEditorConfig =
        FrozenDictionary<string, string>.Empty;

    private readonly WorkerPipeClient _pipeClient;
    private readonly EditorConfigBatcher _editorConfigBatcher;

    public ToolInvoker(WorkerPipeClient pipeClient, EditorConfigBatcher editorConfigBatcher)
    {
        ArgumentNullException.ThrowIfNull(pipeClient);
        ArgumentNullException.ThrowIfNull(editorConfigBatcher);

        _pipeClient = pipeClient;
        _editorConfigBatcher = editorConfigBatcher;
    }

    /// <summary>
    /// Dispatches every task referenced by <paramref name="tool"/> and
    /// composes the result envelope. Resolves the tasks from
    /// <paramref name="group"/> by name. Throws when <paramref name="tool"/>
    /// references an unknown task — the manifest validator should have
    /// caught this at startup.
    /// </summary>
    public async Task<ToolResultEnvelope> InvokeAsync(
        ManifestGroup group,
        ManifestTool tool,
        JsonElement data,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(tool);

        var startTimestamp = Stopwatch.GetTimestamp();

        var tasks = ResolveTasks(group, tool);

        var slices = await ResolveEditorConfigAsync(data, tasks, ct).ConfigureAwait(false);

        // Allocate the result slots in declared order; concurrent dispatch
        // writes into its own slot, so the final order matches the
        // manifest's tool.Tasks order regardless of execution order.
        var entries = new ToolEnvelopeComposerInput[tasks.Length];

        await DispatchAsync(group.Endpoint, tasks, data, slices, entries, ct).ConfigureAwait(false);

        return ToolEnvelopeComposer.Compose(
            tool.Definition.Name,
            entries,
            ElapsedMs(startTimestamp));
    }

    private static ManifestTask[] ResolveTasks(ManifestGroup group, ManifestTool tool)
    {
        if (tool.Tasks.Count == 0)
        {
            return [];
        }

        var byName = new Dictionary<string, ManifestTask>(group.Tasks.Count, StringComparer.Ordinal);

        foreach (var task in group.Tasks)
        {
            byName[task.Name] = task;
        }

        var resolved = new ManifestTask[tool.Tasks.Count];

        for (var i = 0; i < tool.Tasks.Count; i++)
        {
            var name = tool.Tasks[i];

            if (!byName.TryGetValue(name, out var task))
            {
                throw new InvalidOperationException(
                    $"Tool '{tool.Definition.Name}' references task '{name}' which does not exist in group '{group.Tag}'.");
            }

            resolved[i] = task;
        }

        return resolved;
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> ResolveEditorConfigAsync(
        JsonElement data,
        ManifestTask[] tasks,
        CancellationToken ct)
    {
        var anyKeys = false;

        for (var i = 0; i < tasks.Length; i++)
        {
            if (tasks[i].EditorConfig.Count > 0)
            {
                anyKeys = true;
                break;
            }
        }

        if (!anyKeys)
        {
            return BuildEmptySlices(tasks);
        }

        if (!TryGetPath(data, out var path))
        {
            return BuildEmptySlices(tasks);
        }

        var result = await _editorConfigBatcher
            .ResolveAsync(path, tasks, ct)
            .ConfigureAwait(false);

        return result.Slices;
    }

    private async Task DispatchAsync(
        string endpoint,
        ManifestTask[] tasks,
        JsonElement data,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> slices,
        ToolEnvelopeComposerInput[] entries,
        CancellationToken ct)
    {
        // Build (originalIndex, task) pairs grouped by priority. Nonzero
        // priorities run as concurrent groups in ascending order; zero/
        // omitted priority tasks run sequentially after every prioritized
        // group.
        var prioritized = new SortedDictionary<int, List<int>>();
        var trailing = new List<int>(tasks.Length);

        for (var i = 0; i < tasks.Length; i++)
        {
            var priority = tasks[i].Priority;

            if (priority == 0)
            {
                trailing.Add(i);
                continue;
            }

            if (!prioritized.TryGetValue(priority, out var bucket))
            {
                bucket = [];
                prioritized[priority] = bucket;
            }

            bucket.Add(i);
        }

        foreach (var bucket in prioritized.Values)
        {
            if (bucket.Count == 1)
            {
                await DispatchOneAsync(endpoint, tasks, data, slices, entries, bucket[0], ct)
                    .ConfigureAwait(false);
                continue;
            }

            var pending = new Task[bucket.Count];

            for (var i = 0; i < bucket.Count; i++)
            {
                pending[i] = DispatchOneAsync(endpoint, tasks, data, slices, entries, bucket[i], ct);
            }

            await Task.WhenAll(pending).ConfigureAwait(false);
        }

        foreach (var index in trailing)
        {
            await DispatchOneAsync(endpoint, tasks, data, slices, entries, index, ct)
                .ConfigureAwait(false);
        }
    }

    private async Task DispatchOneAsync(
        string endpoint,
        ManifestTask[] tasks,
        JsonElement data,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> slices,
        ToolEnvelopeComposerInput[] entries,
        int index,
        CancellationToken ct)
    {
        var task = tasks[index];

        if (!slices.TryGetValue(task.Name, out var editorConfig))
        {
            editorConfig = EmptyEditorConfig;
        }

        var request = new TaskWireRequest
        {
            McpTask = task.Name,
            Data = data,
            EditorConfig = editorConfig,
        };

        var taskStart = Stopwatch.GetTimestamp();

        var response = await _pipeClient
            .InvokeAsync(endpoint, request, ct)
            .ConfigureAwait(false);

        entries[index] = new ToolEnvelopeComposerInput
        {
            Response = response,
            ElapsedMs = ElapsedMs(taskStart),
        };
    }

    private static bool TryGetPath(JsonElement data, out string path)
    {
        if (data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty(PathParameterName, out var pathElement)
            && pathElement.ValueKind == JsonValueKind.String)
        {
            var value = pathElement.GetString();

            if (!string.IsNullOrEmpty(value))
            {
                path = value;
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string>> BuildEmptySlices(
        ManifestTask[] tasks)
    {
        var dict = new Dictionary<string, IReadOnlyDictionary<string, string>>(
            tasks.Length,
            StringComparer.Ordinal);

        for (var i = 0; i < tasks.Length; i++)
        {
            dict[tasks[i].Name] = EmptyEditorConfig;
        }

        return dict;
    }

    private static int ElapsedMs(long startTimestamp)
    {
        var elapsed = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        if (elapsed <= 0)
        {
            return 0;
        }

        if (elapsed >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)elapsed;
    }
}
