namespace AutoContext.Engine.Core.Features.McpTools.EditorConfig;

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;
using AutoContext.Engine.Core.Workers;
using AutoContext.Engine.Protocol;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Default <see cref="IEditorConfigResolver"/>: resolves EditorConfig keys
/// by round-tripping to <c>Worker.Workspace</c>'s
/// <c>get_editorconfig_rules</c> task over the shared request/response pipe
/// contract. This is the engine's single editorconfig hop — resolution
/// itself lives in the workspace worker, never in-process.
/// </summary>
internal sealed partial class WorkerEditorConfigResolver : IEditorConfigResolver
{
    private const string CorrelationIdPropertyName = "correlationId";
    private const string DataPropertyName = "data";
    private const string EditorconfigPropertyName = "editorconfig";
    private const string KeysPropertyName = "keys";
    private const string OutputPropertyName = "output";
    private const string PathPropertyName = "path";
    private const string ResolveTaskName = "get_editorconfig_rules";
    private const string StatusOk = "ok";
    private const string StatusPropertyName = "status";
    private const string TaskPropertyName = "mcpTask";
    private const string WorkspaceWorkerId = "workspace";

    private static readonly IReadOnlyDictionary<string, string> EmptyMap =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions WorkerJsonOptions = CreateWorkerJsonOptions();

    private readonly string _instanceId;
    private readonly ILogger<WorkerEditorConfigResolver> _logger;
    private readonly PipeTransport _transport;
    private readonly TimeSpan _waitDeadline;
    private readonly WorkerManager _workerManager;

    public WorkerEditorConfigResolver(
        WorkerManager workerManager,
        PipeTransport transport,
        string instanceId,
        ILogger<WorkerEditorConfigResolver> logger)
        : this(workerManager, transport, instanceId, TimeSpan.FromSeconds(30), logger)
    {
    }

    public WorkerEditorConfigResolver(
        WorkerManager workerManager,
        PipeTransport transport,
        string instanceId,
        TimeSpan waitDeadline,
        ILogger<WorkerEditorConfigResolver> logger)
    {
        if (waitDeadline <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(waitDeadline),
                waitDeadline,
                "Resolve wait deadline must be positive.");
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
    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string? filePath,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);

        if (ShouldSkip(filePath, keys))
        {
            return EmptyMap;
        }

        var endpoint = ServiceAddressFormatter.Format($"worker-{WorkspaceWorkerId}", _instanceId);
        var correlationId = CreateCorrelationId();

        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCts.CancelAfter(_waitDeadline);

        try
        {
            await _workerManager
                .EnsureRunningAsync(WorkspaceWorkerId, deadlineCts.Token)
                .ConfigureAwait(false);

            var requestBytes = BuildRequestBytes(filePath, keys, correlationId);

            var exchange = new PipeTransientExchangeClient(_transport, endpoint);
            await using (exchange.ConfigureAwait(false))
            {
                var responseBytes = await exchange
                    .ExchangeAsync(requestBytes, deadlineCts.Token)
                    .ConfigureAwait(false);

                return ParseResolvedMap(responseBytes);
            }
        }
        catch (OperationCanceledException) when (
            deadlineCts.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            LogResolveFailed(
                _logger,
                endpoint,
                $"Resolution exceeded the {_waitDeadline.TotalSeconds:0.##}s wait deadline.",
                null);
            return EmptyMap;
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
            LogResolveFailed(_logger, endpoint, ex.Message, ex);
            return EmptyMap;
        }
    }

    /// <summary>
    /// Builds the <c>get_editorconfig_rules</c> worker request envelope for
    /// <paramref name="filePath"/> and <paramref name="keys"/>.
    /// </summary>
    internal static byte[] BuildRequestBytes(
        string filePath,
        IReadOnlyList<string> keys,
        string correlationId)
    {
        var keysArray = new JsonArray();

        foreach (var key in keys)
        {
            keysArray.Add(key);
        }

        var request = new JsonObject
        {
            [TaskPropertyName] = ResolveTaskName,
            [DataPropertyName] = new JsonObject
            {
                [PathPropertyName] = filePath,
                [KeysPropertyName] = keysArray,
            },
            [EditorconfigPropertyName] = new JsonObject(),
            [CorrelationIdPropertyName] = correlationId,
        };

        return JsonSerializer.SerializeToUtf8Bytes(request, WorkerJsonOptions);
    }

    internal static IReadOnlyDictionary<string, string> ParseResolvedMap(byte[] responseBytes)
    {
        using var document = JsonDocument.Parse(responseBytes);
        var root = document.RootElement;

        var status = root.TryGetProperty(StatusPropertyName, out var statusElement)
            && statusElement.ValueKind == JsonValueKind.String
                ? statusElement.GetString()
                : null;

        if (!string.Equals(status, StatusOk, StringComparison.Ordinal))
        {
            return EmptyMap;
        }

        if (!root.TryGetProperty(OutputPropertyName, out var output)
            || output.ValueKind != JsonValueKind.Object)
        {
            return EmptyMap;
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in output.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                map[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return map;
    }

    /// <summary>
    /// True when resolution can be skipped entirely (no keys declared or no
    /// file path supplied), so no worker hop is taken.
    /// </summary>
    internal static bool ShouldSkip([NotNullWhen(false)] string? filePath, IReadOnlyList<string> keys)
        => keys.Count == 0 || string.IsNullOrWhiteSpace(filePath);

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

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "EditorConfig resolution failed on worker endpoint '{Endpoint}': {Reason}")]
    private static partial void LogResolveFailed(
        ILogger logger,
        string endpoint,
        string reason,
        Exception? exception);
}
