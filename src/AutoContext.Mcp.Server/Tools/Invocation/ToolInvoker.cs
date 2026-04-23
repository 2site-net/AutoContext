namespace AutoContext.Mcp.Server.Tools.Invocation;

using System.Collections.Frozen;
using System.Diagnostics;
using System.Text.Json;

using AutoContext.Mcp.Server.EditorConfig;
using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tools.Results;
using AutoContext.Mcp.Server.Workers;
using AutoContext.Mcp.Server.Workers.Protocol;

/// <summary>
/// Orchestrates one MCP-Tool invocation. Resolves EditorConfig values for
/// the tool's tasks in a single batched call, runs every task
/// concurrently over a named pipe, and composes the per-task responses
/// into a uniform <see cref="ToolResultEnvelope"/>. Pure orchestration —
/// no MCP SDK.
/// </summary>
/// <remarks>
/// All tasks in a tool invocation run concurrently via
/// <see cref="Task.WhenAll(IEnumerable{Task})"/>. The order of entries in
/// the resulting <see cref="ToolResultEnvelope.Result"/> matches the
/// declared order in <see cref="McpToolDefinition.Tasks"/>, regardless of
/// completion order.
/// </remarks>
public sealed class ToolInvoker
{
    /// <summary>The <c>data</c> property name probed when resolving EditorConfig.</summary>
    public const string PathParameterName = "originalPath";

    private static readonly IReadOnlyDictionary<string, string> EmptyEditorConfig =
        FrozenDictionary<string, string>.Empty;

    private readonly WorkerClient _workerClient;
    private readonly EditorConfigBatcher _editorConfigBatcher;

    public ToolInvoker(WorkerClient workerClient, EditorConfigBatcher editorConfigBatcher)
    {
        ArgumentNullException.ThrowIfNull(workerClient);
        ArgumentNullException.ThrowIfNull(editorConfigBatcher);

        _workerClient = workerClient;
        _editorConfigBatcher = editorConfigBatcher;
    }

    /// <summary>
    /// Dispatches every task in <paramref name="tool"/> to
    /// <paramref name="worker"/> concurrently and composes the result
    /// envelope.
    /// </summary>
    public async Task<ToolResultEnvelope> InvokeAsync(
        McpWorker worker,
        McpToolDefinition tool,
        JsonElement data,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(tool);

        var startTimestamp = Stopwatch.GetTimestamp();

        var tasks = tool.Tasks;
        var slices = await ResolveEditorConfigAsync(data, tasks, ct).ConfigureAwait(false);

        var entries = new ToolResultComposerInput[tasks.Count];

        if (tasks.Count == 1)
        {
            await DispatchOneAsync(worker.Endpoint, tasks, data, slices, entries, 0, ct)
                .ConfigureAwait(false);
        }
        else if (tasks.Count > 1)
        {
            var pending = new Task[tasks.Count];

            for (var i = 0; i < tasks.Count; i++)
            {
                pending[i] = DispatchOneAsync(worker.Endpoint, tasks, data, slices, entries, i, ct);
            }

            await Task.WhenAll(pending).ConfigureAwait(false);
        }

        return ToolResultComposer.Compose(
            tool.Name,
            entries,
            ElapsedMs(startTimestamp));
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> ResolveEditorConfigAsync(
        JsonElement data,
        IReadOnlyList<McpTaskDefinition> tasks,
        CancellationToken ct)
    {
        var anyKeys = false;

        for (var i = 0; i < tasks.Count; i++)
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

    private async Task DispatchOneAsync(
        string endpoint,
        IReadOnlyList<McpTaskDefinition> tasks,
        JsonElement data,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> slices,
        ToolResultComposerInput[] entries,
        int index,
        CancellationToken ct)
    {
        var task = tasks[index];

        if (!slices.TryGetValue(task.Name, out var editorConfig))
        {
            editorConfig = EmptyEditorConfig;
        }

        var request = new TaskRequest
        {
            McpTask = task.Name,
            Data = data,
            EditorConfig = editorConfig,
        };

        var taskStart = Stopwatch.GetTimestamp();

        var response = await _workerClient
            .InvokeAsync(endpoint, request, ct)
            .ConfigureAwait(false);

        entries[index] = new ToolResultComposerInput
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
        IReadOnlyList<McpTaskDefinition> tasks)
    {
        var dict = new Dictionary<string, IReadOnlyDictionary<string, string>>(
            tasks.Count,
            StringComparer.Ordinal);

        for (var i = 0; i < tasks.Count; i++)
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
