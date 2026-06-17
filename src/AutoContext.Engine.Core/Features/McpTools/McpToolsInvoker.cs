namespace AutoContext.Engine.Core.Features.McpTools;

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Core.Infrastructure.Diagnostics;
using AutoContext.Engine.Core.Workers;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Production <see cref="IMcpToolsInvoker"/> that dispatches one tool call
/// to the owning worker over the shared request/response pipe contract.
/// </summary>
internal sealed partial class McpToolsInvoker : IMcpToolsInvoker
{
    private const string CorrelationIdPropertyName = "correlationId";
    private const string DataPropertyName = "data";
    private const string EditorconfigPropertyName = "editorconfig";
    private const string ErrorPropertyName = "error";
    private const string OutputPropertyName = "output";
    private const string StatusError = "error";
    private const string StatusOk = "ok";
    private const string StatusPropertyName = "status";
    private const string TaskPropertyName = "mcpTask";
    private const string TextBlockType = "text";

    private static readonly JsonSerializerOptions WorkerJsonOptions = CreateWorkerJsonOptions();

    private readonly string _instanceId;
    private readonly ILogger<McpToolsInvoker> _logger;
    private readonly PipeTransport _transport;
    private readonly TimeSpan _waitDeadline;
    private readonly WorkerManager _workerManager;

    public McpToolsInvoker(
        WorkerManager workerManager,
        PipeTransport transport,
        string instanceId,
        ILogger<McpToolsInvoker> logger)
        : this(workerManager, transport, instanceId, TimeSpan.FromSeconds(30), logger)
    {
    }

    public McpToolsInvoker(
        WorkerManager workerManager,
        PipeTransport transport,
        string instanceId,
        TimeSpan waitDeadline,
        ILogger<McpToolsInvoker> logger)
    {
        if (waitDeadline <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(waitDeadline),
                waitDeadline,
                "Invoke wait deadline must be positive.");
        }

        ArgumentNullException.ThrowIfNull(workerManager);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(logger);

        _workerManager = workerManager;
        _transport = transport;
        _instanceId = instanceId;
        _waitDeadline = waitDeadline;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<JsonMcpToolsInvokeResult> InvokeAsync(
        McpToolsRegistryEntry tool,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var endpoint = ServiceAddressFormatter.Format($"worker-{tool.WorkerId}", _instanceId);
        var correlationId = CreateCorrelationId();

        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCts.CancelAfter(_waitDeadline);

        try
        {
            await _workerManager
                .EnsureRunningAsync(tool.WorkerId, deadlineCts.Token)
                .ConfigureAwait(false);

            var requestBytes = BuildRequestBytes(tool.Name, arguments, correlationId);

            var exchange = new PipeTransientExchangeClient(_transport, endpoint);
            await using (exchange.ConfigureAwait(false))
            {
                var responseBytes = await exchange
                    .ExchangeAsync(requestBytes, deadlineCts.Token)
                    .ConfigureAwait(false);

                return ParseResponse(tool.Name, responseBytes);
            }
        }
        catch (OperationCanceledException) when (
            deadlineCts.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            var message =
                $"Worker invocation exceeded the {_waitDeadline.TotalSeconds:0.##}s wait deadline.";
            LogInvokeFailed(_logger, tool.Name, endpoint, message, null);
            return ToolError(tool.Name, message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is IOException
                or TimeoutException
                or UnauthorizedAccessException
                or JsonException
                or InvalidOperationException
                or ObjectDisposedException
                or ProcessLaunchException<WorkerProcessInfo>)
        {
            var message = $"Worker invocation failed: {ex.Message}";
            LogInvokeFailed(_logger, tool.Name, endpoint, message, ex);
            return ToolError(tool.Name, message);
        }
    }

    private static byte[] BuildRequestBytes(
        string taskName,
        JsonElement arguments,
        string correlationId)
    {
        var request = new JsonObject
        {
            [TaskPropertyName] = taskName,
            [DataPropertyName] = JsonNode.Parse(arguments.GetRawText()),
            [EditorconfigPropertyName] = new JsonObject(),
            [CorrelationIdPropertyName] = correlationId,
        };

        return JsonSerializer.SerializeToUtf8Bytes(request, WorkerJsonOptions);
    }

    private static JsonElement CreateTextBlock(string text)
        => JsonSerializer.SerializeToElement(new { type = TextBlockType, text });

    private static string CreateCorrelationId()
        => Guid.NewGuid().ToString("N")[..8];

    private static JsonSerializerOptions CreateWorkerJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
        };

        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }

    private static List<JsonElement> GetContent(JsonElement? output, string? fallbackText)
    {
        if (output is { ValueKind: JsonValueKind.Object } outputObject
            && outputObject.TryGetProperty("content", out var contentElement)
            && contentElement.ValueKind == JsonValueKind.Array)
        {
            var blocks = new List<JsonElement>(contentElement.GetArrayLength());

            foreach (var block in contentElement.EnumerateArray())
            {
                if (block.ValueKind == JsonValueKind.Object)
                {
                    blocks.Add(block.Clone());
                }
            }

            if (blocks.Count > 0)
            {
                return blocks;
            }
        }

        if (output is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } outputValue)
        {
            return [CreateTextBlock(outputValue.GetRawText())];
        }

        if (!string.IsNullOrWhiteSpace(fallbackText))
        {
            return [CreateTextBlock(fallbackText)];
        }

        return [];
    }

    private static bool? GetIsError(JsonElement? output)
    {
        if (output is { ValueKind: JsonValueKind.Object } outputObject
            && outputObject.TryGetProperty("isError", out var isError)
            && isError.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return isError.GetBoolean();
        }

        return null;
    }

    private static JsonMcpToolsInvokeResult ParseResponse(string toolName, byte[] responseBytes)
    {
        using var document = JsonDocument.Parse(responseBytes);
        var root = document.RootElement;

        var status = root.TryGetProperty(StatusPropertyName, out var statusElement)
            && statusElement.ValueKind == JsonValueKind.String
                ? statusElement.GetString()
                : null;

        var output = root.TryGetProperty(OutputPropertyName, out var outputElement)
            ? outputElement.Clone()
            : (JsonElement?)null;

        var error = root.TryGetProperty(ErrorPropertyName, out var errorElement)
            && errorElement.ValueKind == JsonValueKind.String
                ? errorElement.GetString()
                : null;

        if (string.Equals(status, StatusOk, StringComparison.Ordinal))
        {
            return new JsonMcpToolsInvokeOkResult
            {
                Name = toolName,
                Content = GetContent(output, fallbackText: null),
                IsError = GetIsError(output),
            };
        }

        if (string.Equals(status, StatusError, StringComparison.Ordinal))
        {
            return ToolError(toolName, error, output);
        }

        return ToolError(toolName, $"Worker returned unknown status '{status ?? "(missing)"}'.", output);
    }

    private static JsonMcpToolsInvokeToolErrorResult ToolError(
        string toolName,
        string? message,
        JsonElement? output = null)
    {
        var error = string.IsNullOrWhiteSpace(message)
            ? $"Tool '{toolName}' reported failure."
            : message;

        return new JsonMcpToolsInvokeToolErrorResult
        {
            Name = toolName,
            Content = GetContent(output, error),
        };
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "MCP tool invoke failed for '{ToolName}' on worker endpoint '{Endpoint}': {Reason}")]
    private static partial void LogInvokeFailed(
        ILogger logger,
        string toolName,
        string endpoint,
        string reason,
        Exception? exception);
}
