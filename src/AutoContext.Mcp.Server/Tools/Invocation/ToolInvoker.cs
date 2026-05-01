namespace AutoContext.Mcp.Server.Tools.Invocation;

using System.Collections.Frozen;
using System.Diagnostics;
using System.Text.Json;

using AutoContext.Mcp.Server.Config;
using AutoContext.Mcp.Server.EditorConfig;
using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tools.Results;
using AutoContext.Mcp.Server.Workers;
using AutoContext.Mcp.Server.Workers.Protocol;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Orchestrates one MCP-Tool invocation. Resolves EditorConfig values for
/// the tool's enabled tasks in a single batched call, runs every task
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
public sealed partial class ToolInvoker
{
    /// <summary>The <c>data</c> property name probed when resolving EditorConfig.</summary>
    public const string PathParameterName = "originalPath";

    private static readonly IReadOnlyDictionary<string, string> EmptyEditorConfig =
        FrozenDictionary<string, string>.Empty;

    private readonly WorkerClient _workerClient;
    private readonly EditorConfigBatcher _editorConfigBatcher;
    private readonly AutoContextConfigSnapshot? _configSnapshot;
    private readonly ILogger<ToolInvoker> _logger;

    public ToolInvoker(WorkerClient workerClient, EditorConfigBatcher editorConfigBatcher)
        : this(workerClient, editorConfigBatcher, configSnapshot: null, NullLogger<ToolInvoker>.Instance)
    {
    }

    public ToolInvoker(
        WorkerClient workerClient,
        EditorConfigBatcher editorConfigBatcher,
        ILogger<ToolInvoker> logger)
        : this(workerClient, editorConfigBatcher, configSnapshot: null, logger)
    {
    }

    public ToolInvoker(
        WorkerClient workerClient,
        EditorConfigBatcher editorConfigBatcher,
        AutoContextConfigSnapshot? configSnapshot,
        ILogger<ToolInvoker> logger)
    {
        ArgumentNullException.ThrowIfNull(workerClient);
        ArgumentNullException.ThrowIfNull(editorConfigBatcher);
        ArgumentNullException.ThrowIfNull(logger);

        _workerClient = workerClient;
        _editorConfigBatcher = editorConfigBatcher;
        _configSnapshot = configSnapshot;
        _logger = logger;
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
        string correlationId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentException.ThrowIfNullOrEmpty(correlationId);

        var startTimestamp = Stopwatch.GetTimestamp();
        var tasks = ResolveEnabledTasks(tool);
        LogToolDispatchStarted(_logger, tool.Name, worker.Role, tasks.Count, correlationId);

        if (tasks.Count == 0)
        {
            var elapsedMsForDisabled = ElapsedMs(startTimestamp);
            LogToolDispatchCompleted(_logger, tool.Name, elapsedMsForDisabled, tasks.Count, correlationId);
            return ToolResultComposer.ComposeFailure(
                tool.Name,
                [
                    new ToolResultError
                    {
                        Code = ToolResultErrorCodes.AllTasksDisabled,
                        Message = $"All tasks for tool '{tool.Name}' are disabled by extension config.",
                    },
                ],
                elapsedMsForDisabled);
        }

        var slices = await ResolveEditorConfigAsync(data, tasks, correlationId, ct).ConfigureAwait(false);

        var entries = new ToolResultComposerInput[tasks.Count];

        if (tasks.Count == 1)
        {
            await DispatchOneAsync(worker.Role, tasks, data, slices, entries, 0, correlationId, ct)
                .ConfigureAwait(false);
        }
        else if (tasks.Count > 1)
        {
            var pending = new Task[tasks.Count];

            for (var i = 0; i < tasks.Count; i++)
            {
                pending[i] = DispatchOneAsync(worker.Role, tasks, data, slices, entries, i, correlationId, ct);
            }

            await Task.WhenAll(pending).ConfigureAwait(false);
        }

        var elapsedMs = ElapsedMs(startTimestamp);

        LogToolDispatchCompleted(_logger, tool.Name, elapsedMs, tasks.Count, correlationId);

        return ToolResultComposer.Compose(
            tool.Name,
            entries,
            elapsedMs);
    }

    private IReadOnlyList<McpTaskDefinition> ResolveEnabledTasks(McpToolDefinition tool)
    {
        if (_configSnapshot is null)
        {
            return tool.Tasks;
        }

        if (!_configSnapshot.DisabledTasks.TryGetValue(tool.Name, out var disabled) || disabled.Count == 0)
        {
            return tool.Tasks;
        }

        var enabled = new List<McpTaskDefinition>(tool.Tasks.Count);

        for (var i = 0; i < tool.Tasks.Count; i++)
        {
            var task = tool.Tasks[i];

            if (!disabled.Contains(task.Name))
            {
                enabled.Add(task);
            }
        }

        return enabled;
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> ResolveEditorConfigAsync(
        JsonElement data,
        IReadOnlyList<McpTaskDefinition> tasks,
        string correlationId,
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
            .ResolveAsync(path, tasks, correlationId, ct)
            .ConfigureAwait(false);

        return result.Slices;
    }

    private async Task DispatchOneAsync(
        string role,
        IReadOnlyList<McpTaskDefinition> tasks,
        JsonElement data,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> slices,
        ToolResultComposerInput[] entries,
        int index,
        string correlationId,
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
            CorrelationId = correlationId,
        };

        var taskStart = Stopwatch.GetTimestamp();

        LogTaskDispatchStarted(_logger, task.Name, role, correlationId);

        var response = await _workerClient
            .InvokeAsync(role, request, ct)
            .ConfigureAwait(false);

        var elapsedMs = ElapsedMs(taskStart);
        LogTaskDispatchCompleted(_logger, task.Name, role, response.Status, elapsedMs, correlationId);

        entries[index] = new ToolResultComposerInput
        {
            Response = response,
            ElapsedMs = elapsedMs,
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Dispatching tool '{ToolName}' to role '{Role}' with {TaskCount} task(s). CorrelationId={CorrelationId}")]
    private static partial void LogToolDispatchStarted(
        ILogger logger,
        string toolName,
        string role,
        int taskCount,
        string correlationId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Completed tool '{ToolName}' in {ElapsedMs}ms ({TaskCount} task(s)). CorrelationId={CorrelationId}")]
    private static partial void LogToolDispatchCompleted(
        ILogger logger,
        string toolName,
        int elapsedMs,
        int taskCount,
        string correlationId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Dispatching MCP task '{TaskName}' to role '{Role}'. CorrelationId={CorrelationId}")]
    private static partial void LogTaskDispatchStarted(
        ILogger logger,
        string taskName,
        string role,
        string correlationId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "Completed MCP task '{TaskName}' from role '{Role}' with status '{Status}' in {ElapsedMs}ms. CorrelationId={CorrelationId}")]
    private static partial void LogTaskDispatchCompleted(
        ILogger logger,
        string taskName,
        string role,
        string status,
        int elapsedMs,
        string correlationId);
}
