namespace AutoContext.Mcp.Server.EditorConfig;

using System.Collections.Frozen;
using System.Text.Json;

using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Workers;
using AutoContext.Mcp.Server.Workers.Protocol;
using AutoContext.Mcp.Server.Workers.Transport;

using Microsoft.Extensions.Logging;

/// <summary>
/// Resolves EditorConfig values for one tool invocation in a single
/// batched pipe call to <c>Worker.Workspace</c>'s
/// <c>get_editorconfig_rules</c> task. Sends the union of declared keys
/// across the supplied tasks, then slices the result per task so each
/// task receives only the keys it asked for.
/// </summary>
public sealed partial class EditorConfigBatcher
{
    /// <summary>The pipe-name value used by Worker.Workspace.</summary>
    public static readonly string DefaultWorkspaceEndpoint = EndpointFormatter.Format("workspace");

    /// <summary>The MCP Task name on Worker.Workspace that resolves EditorConfig keys.</summary>
    public const string ResolveTaskName = "get_editorconfig_rules";

    private readonly WorkerClient _workerClient;
    private readonly string _workspaceEndpoint;
    private readonly ILogger<EditorConfigBatcher> _logger;

    public EditorConfigBatcher(WorkerClient workerClient, ILogger<EditorConfigBatcher> logger)
        : this(workerClient, DefaultWorkspaceEndpoint, logger)
    {
    }

    public EditorConfigBatcher(
        WorkerClient workerClient,
        string workspaceEndpoint,
        ILogger<EditorConfigBatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(workerClient);
        ArgumentException.ThrowIfNullOrEmpty(workspaceEndpoint);
        ArgumentNullException.ThrowIfNull(logger);

        _workerClient = workerClient;
        _workspaceEndpoint = workspaceEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the union of <see cref="McpTaskDefinition.EditorConfig"/> keys
    /// across <paramref name="tasks"/> for <paramref name="filePath"/>,
    /// then returns a per-task slice. Tasks that declared no keys (or
    /// whose declared keys all came back missing) map to an empty
    /// dictionary. On pipe failure every task maps to an empty dictionary
    /// — <see cref="ToolInvoker"/> logs the warning and proceeds anyway,
    /// matching the contract in the architecture doc.
    /// </summary>
    public async Task<EditorConfigBatchResult> ResolveAsync(
        string filePath,
        IReadOnlyList<McpTaskDefinition> tasks,
        string correlationId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentException.ThrowIfNullOrEmpty(correlationId);

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
            WorkerJsonOptions.Instance);

        var request = new TaskRequest
        {
            McpTask = ResolveTaskName,
            Data = requestData,
            EditorConfig = FrozenDictionary<string, string>.Empty,
            CorrelationId = correlationId,
        };

        var response = await _workerClient
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

        if (!string.Equals(response.Status, TaskResponse.StatusOk, StringComparison.Ordinal))
        {
            return new EditorConfigBatchResult
            {
                Slices = BuildEmptySlices(tasks),
                ResolutionFailed = true,
                FailureMessage = response.Error,
            };
        }

        // Distinguish "worker had nothing to say" (null / JSON null /
        // missing — legitimate "no rules apply") from "worker returned
        // a non-object payload" (a contract violation we want to surface
        // instead of silently swallowing).
        if (response.Output is { } element &&
            element.ValueKind != JsonValueKind.Null &&
            element.ValueKind != JsonValueKind.Undefined &&
            element.ValueKind != JsonValueKind.Object)
        {
            LogNonObjectOutput(_logger, ResolveTaskName, element.ValueKind);

            return new EditorConfigBatchResult
            {
                Slices = BuildEmptySlices(tasks),
                ResolutionFailed = true,
                FailureMessage =
                    $"Worker returned non-object output for task '{ResolveTaskName}' (kind: {element.ValueKind}); expected a JSON object.",
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

        // Stable order keeps the worker request deterministic and easier
        // to diff in logs. Ordinal sort matches the comparer above.
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

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Worker returned non-object output for task '{TaskName}' (kind: {ValueKind}); expected a JSON object.")]
    private static partial void LogNonObjectOutput(ILogger logger, string taskName, JsonValueKind valueKind);
}
