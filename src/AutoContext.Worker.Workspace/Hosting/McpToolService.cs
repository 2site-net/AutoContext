namespace AutoContext.Worker.Workspace.Hosting;

using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;

using AutoContext.Mcp.Abstractions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Named-pipe server that accepts per-task connections and dispatches each
/// one to an <see cref="IMcpTask"/> instance.
/// </summary>
/// <remarks>
/// Wire protocol (per-task, see architecture-centralized-mcp.md §"Protocol &amp; Contracts"):
/// 4-byte little-endian length prefix + UTF-8 JSON payload.
/// <para>
/// Request:  <c>{ "mcpTask", "data", "editorconfig" }</c><br/>
/// Response: <c>{ "mcpTask", "status", "output", "error" }</c>
/// </para>
/// <para>
/// Any <c>editorconfig</c> object on the request envelope is flattened into
/// <c>data</c> as properties prefixed with <c>editorconfig.</c> before the
/// task is invoked, so tasks see a single payload.
/// </para>
/// <para>
/// One connection = one task call. The service spawns a per-connection
/// <see cref="Task"/> and immediately returns to <c>WaitForConnectionAsync</c>.
/// </para>
/// </remarks>
internal sealed partial class McpToolService : BackgroundService
{
    internal const string ReadyMarker = "[AutoContext.Worker.Workspace] Ready.";

    internal static JsonSerializerOptions WireJsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ILogger<McpToolService> _logger;
    private readonly WorkerOptions _options;
    private readonly Dictionary<string, IMcpTask> _tasks;

    public McpToolService(
        IOptions<WorkerOptions> options,
        IEnumerable<IMcpTask> tasks,
        ILogger<McpToolService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
        _tasks = tasks.ToDictionary(t => t.TaskName, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeName = _options.Pipe;

        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new InvalidOperationException("Missing required configuration: --pipe");
        }

        LogStarting(_logger, pipeName);

        var connections = new List<Task>();
        var firstAccept = true;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var pipe = await AcceptConnectionAsync(
                    pipeName,
                    onListening: () =>
                    {
                        if (!firstAccept)
                        {
                            return Task.CompletedTask;
                        }

                        firstAccept = false;

                        // Host-handshake contract: the parent (extension) process scrapes
                        // worker stderr for this exact marker to know the named pipe is
                        // listening. ILogger output is routed elsewhere and would miss the
                        // contract — this call MUST stay on Console.Error.
                        return Console.Error.WriteLineAsync(ReadyMarker);
                    },
                    stoppingToken).ConfigureAwait(false);

                if (pipe is null)
                {
                    break;
                }

                connections.Add(HandleConnectionAsync(pipe, stoppingToken));
            }
        }
        finally
        {
            await Task.WhenAll(connections).ConfigureAwait(false);
        }
    }

    private static async Task<NamedPipeServerStream?> AcceptConnectionAsync(
        string pipeName,
        Func<Task> onListening,
        CancellationToken ct)
    {
        NamedPipeServerStream? pipe = null;
        var ownsPipe = true;
        CancellationTokenRegistration registration = default;

        try
        {
            pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            // On Windows, WaitForConnectionAsync does not reliably honor the
            // cancellation token. Disposing the pipe from the cancellation
            // callback forces the wait to throw.
            registration = ct.Register(pipe.Dispose);

            var waitTask = pipe.WaitForConnectionAsync(ct);

            await onListening().ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);

            ownsPipe = false;

            return pipe;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            await registration.DisposeAsync().ConfigureAwait(false);

            if (ownsPipe && pipe is not null)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            await using (pipe.ConfigureAwait(false))
            {
                var requestBytes = await PipeFraming.ReadMessageAsync(pipe, ct).ConfigureAwait(false);

                if (requestBytes is null)
                {
                    return;
                }

                var responseBytes = await DispatchAsync(requestBytes, ct).ConfigureAwait(false);

                await PipeFraming.WriteMessageAsync(pipe, responseBytes, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown — exit silently.
        }
        catch (IOException)
        {
            // Client disconnected mid-call — exit silently.
        }
        catch (ObjectDisposedException)
        {
            // Pipe disposed during shutdown — exit silently.
        }
    }

    [SuppressMessage("Design", "CA1031",
        Justification = "Worker boundary: any task failure must be returned as an error envelope, never crash the dispatcher.")]
    private async Task<byte[]> DispatchAsync(byte[] requestBytes, CancellationToken ct)
    {
        string? taskName = null;

        try
        {
            using var doc = JsonDocument.Parse(requestBytes);
            var root = doc.RootElement;

            if (!root.TryGetProperty("mcpTask", out var taskNameElement)
                || taskNameElement.ValueKind != JsonValueKind.String)
            {
                return BuildErrorResponse(taskName: "", "Request is missing required field 'mcpTask'.");
            }

            taskName = taskNameElement.GetString()!;

            if (!_tasks.TryGetValue(taskName, out var task))
            {
                return BuildErrorResponse(taskName, $"Unknown task '{taskName}'.");
            }

            var data = BuildTaskData(root);

            var output = await task.ExecuteAsync(data, ct).ConfigureAwait(false);

            return BuildSuccessResponse(taskName, output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            return BuildErrorResponse(taskName ?? "", $"Malformed request JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            LogTaskFailed(_logger, taskName ?? "<unknown>", ex);
            return BuildErrorResponse(taskName ?? "", $"Task threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static JsonElement BuildTaskData(JsonElement root)
    {
        var hasData = root.TryGetProperty("data", out var dataElement)
            && dataElement.ValueKind == JsonValueKind.Object;

        var hasEditorConfig = root.TryGetProperty("editorconfig", out var ec)
            && ec.ValueKind == JsonValueKind.Object;

        if (!hasEditorConfig)
        {
            return hasData ? dataElement.Clone() : default;
        }

        // Merge editorconfig.<key> properties into data so tasks see a single payload.
        var merged = hasData
            ? (JsonObject)JsonNode.Parse(dataElement.GetRawText())!
            : [];

        foreach (var prop in ec.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                merged["editorconfig." + prop.Name] = prop.Value.GetString();
            }
        }

        return JsonSerializer.SerializeToElement(merged);
    }

    internal static byte[] BuildSuccessResponse(string taskName, JsonElement output)
    {
        var response = new JsonObject
        {
            ["mcpTask"] = taskName,
            ["status"] = "ok",
            ["output"] = JsonNode.Parse(output.GetRawText()),
            ["error"] = string.Empty,
        };

        return JsonSerializer.SerializeToUtf8Bytes(response, WireJsonOptions);
    }

    internal static byte[] BuildErrorResponse(string taskName, string error)
    {
        var response = new JsonObject
        {
            ["mcpTask"] = taskName,
            ["status"] = "error",
            ["output"] = null,
            ["error"] = error,
        };

        return JsonSerializer.SerializeToUtf8Bytes(response, WireJsonOptions);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Workspace worker listening on pipe: {PipeName}")]
    private static partial void LogStarting(ILogger logger, string pipeName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Task '{TaskName}' threw an unhandled exception.")]
    private static partial void LogTaskFailed(ILogger logger, string taskName, Exception exception);
}
