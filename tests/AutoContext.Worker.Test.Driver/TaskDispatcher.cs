namespace AutoContext.Worker.Test.Driver;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Parses one request envelope, runs the matching
/// <see cref="ITestDriverTask"/>, and renders the response envelope the
/// engine's <c>McpToolsInvoker</c> expects
/// (<c>{ "mcpTask", "status", "output", "error" }</c>). The small wire
/// contract is re-implemented here rather than taken from
/// <c>AutoContext.Workers.Core</c>, keeping the driver independent of
/// the worker-host framework while still speaking exactly what the engine
/// dials.
/// </summary>
internal sealed class TaskDispatcher
{
    private const string EditorconfigPrefix = "editorconfig.";
    private const string StatusError = "error";
    private const string StatusOk = "ok";
    private const string TaskPropertyName = "mcpTask";

    private static readonly JsonSerializerOptions WireOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = false };

    private readonly Dictionary<string, ITestDriverTask> _tasks;

    /// <summary>
    /// Creates a dispatcher over the supplied tasks, keyed by
    /// <see cref="ITestDriverTask.TaskName"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="tasks"/>, or
    /// any element, is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Two tasks share a
    /// name.</exception>
    public TaskDispatcher(IEnumerable<ITestDriverTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        var map = new Dictionary<string, ITestDriverTask>(StringComparer.Ordinal);

        foreach (var task in tasks)
        {
            ArgumentNullException.ThrowIfNull(task);

            if (!map.TryAdd(task.TaskName, task))
            {
                throw new InvalidOperationException(
                    $"Duplicate task registration for '{task.TaskName}'.");
            }
        }

        _tasks = map;
    }

    /// <summary>
    /// Dispatches one request envelope and returns the response envelope
    /// bytes. Every failure path — malformed JSON, missing/unknown task, or
    /// a task that throws — is returned as an <c>error</c> envelope so a bad
    /// request can never crash the connection loop.
    /// </summary>
    [SuppressMessage("Design", "CA1031",
        Justification = "Worker boundary: any task failure must be returned as an error envelope, never crash the dispatcher.")]
    public async Task<byte[]> DispatchAsync(byte[] requestBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestBytes);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(requestBytes);
        }
        catch (JsonException exception)
        {
            return BuildError(taskName: string.Empty, $"Malformed request JSON: {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;

            if (!root.TryGetProperty(TaskPropertyName, out var taskNameElement)
                || taskNameElement.ValueKind != JsonValueKind.String)
            {
                return BuildError(taskName: string.Empty, $"Request is missing required field '{TaskPropertyName}'.");
            }

            var taskName = taskNameElement.GetString()!;

            if (!_tasks.TryGetValue(taskName, out var task))
            {
                return BuildError(taskName, $"Unknown task '{taskName}'.");
            }

            try
            {
                var data = BuildTaskData(root);
                var output = await task.ExecuteAsync(data, cancellationToken).ConfigureAwait(false);

                return BuildOk(taskName, output);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return BuildError(taskName, exception.Message);
            }
        }
    }

    /// <summary>
    /// Builds the task payload: the request <c>data</c> object with any
    /// <c>editorconfig</c> values flattened in as <c>editorconfig.&lt;key&gt;</c>
    /// string properties, mirroring what shipped workers see.
    /// </summary>
    private static JsonElement BuildTaskData(JsonElement root)
    {
        var data = root.TryGetProperty("data", out var dataElement)
            && dataElement.ValueKind == JsonValueKind.Object
                ? JsonNode.Parse(dataElement.GetRawText()) as JsonObject ?? []
                : [];

        if (root.TryGetProperty("editorconfig", out var editorconfig)
            && editorconfig.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in editorconfig.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    data[EditorconfigPrefix + property.Name] = property.Value.GetString();
                }
            }
        }

        return JsonSerializer.SerializeToElement(data, WireOptions);
    }

    private static byte[] BuildOk(string taskName, JsonElement output)
    {
        var response = new JsonObject
        {
            [TaskPropertyName] = taskName,
            ["status"] = StatusOk,
            ["output"] = JsonNode.Parse(output.GetRawText()),
            ["error"] = string.Empty,
        };

        return JsonSerializer.SerializeToUtf8Bytes(response, WireOptions);
    }

    private static byte[] BuildError(string taskName, string error)
    {
        var response = new JsonObject
        {
            [TaskPropertyName] = taskName,
            ["status"] = StatusError,
            ["output"] = null,
            ["error"] = error,
        };

        return JsonSerializer.SerializeToUtf8Bytes(response, WireOptions);
    }
}
