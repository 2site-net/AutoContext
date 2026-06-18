namespace AutoContext.Engine.Core.Rpc.Policies;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Features.McpTools;
using AutoContext.Engine.Core.Infrastructure;
using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Core.Workspace.Context;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Protocol.Messages.Workspace;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="IRpcConnectionPolicy"/> that runs after a successful
/// <c>Engine.Hello</c> handshake on an
/// <see cref="EndpointKind.Rpc"/> connection. Per
/// <c>design § RPC surface</c> the engine currently exposes two
/// methods — <c>Engine.RegistryEntries</c> and
/// <c>Engine.Shutdown</c>; any other method name surfaces a
/// JSON-RPC <see cref="JsonRpcErrorCodes.MethodNotFound"/> reply
/// and the loop keeps serving.
/// </summary>
/// <remarks>
/// <para>
/// The policy is intentionally narrow: it does not multiplex
/// concurrent handlers on one connection and it does not interpret
/// <c>Engine.Hello</c> — the handshake step owns that. Recoverable
/// per-frame failures (malformed JSON, unknown method) reply with
/// the appropriate error code and the processor keeps reading
/// (<see cref="FrameFailurePolicy.Recover"/>).
/// </para>
/// <para>
/// <c>Engine.Shutdown</c> returns <c>{ accepted: true }</c> with a
/// <see cref="Continuation.Complete"/> continuation and a
/// <see cref="RpcHandlerResult.PostFlush"/> that calls
/// <see cref="IHostApplicationLifetime.StopApplication"/>. The
/// processor guarantees the response lands on the wire before the
/// post-flush runs — so the host begins tearing down listeners only
/// after the acknowledgement has been observed by the client. The
/// hosted-service stop sequence (which runs in reverse-registration
/// order) then drains and disposes the four pipes.
/// </para>
/// </remarks>
internal sealed partial class DispatchPolicy : IRpcConnectionPolicy
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly RegistryFileReader _registryReader;
    private readonly EngineLogFileReader _logFileReader;
    private readonly Broadcaster<JsonLogRecord> _logsBroadcaster;
    private readonly LogFrameStream _logFrameStream;
    private readonly IConfigSnapshotAccessor _configAccessor;
    private readonly SnapshotBroadcaster<JsonConfigSnapshot> _configBroadcaster;
    private readonly ConfigFrameStream _configFrameStream;
    private readonly IConfigUpdater _configUpdater;
    private readonly IWorkspaceContextAccessor _workspaceAccessor;
    private readonly IInstructionsManifestAccessor _instructionsManifestAccessor;
    private readonly InstructionsBodyProjector _instructionsBodyProjector;
    private readonly InstructionsFileReader _instructionsFileReader;
    private readonly InstructionsFullTextSearchService _instructionsFullTextSearchService;
    private readonly InstructionsListProjector _instructionsListProjector;
    private readonly SnapshotBroadcaster<IReadOnlyList<JsonInstructionsListRow>> _instructionsSnapshotBroadcaster;
    private readonly InstructionsFrameStream _instructionsFrameStream;
    private readonly IMcpToolsRegistryAccessor _mcpToolsRegistryAccessor;
    private readonly IMcpToolsInvoker _mcpToolsInvoker;
    private readonly ILogger _logger;

    public DispatchPolicy(
        IHostApplicationLifetime lifetime,
        RegistryFileReader registryReader,
        EngineLogFileReader logFileReader,
        Broadcaster<JsonLogRecord> logsBroadcaster,
        IConfigSnapshotAccessor configAccessor,
        IConfigUpdater configUpdater,
        SnapshotBroadcaster<JsonConfigSnapshot> configBroadcaster,
        IWorkspaceContextAccessor workspaceAccessor,
        IInstructionsManifestAccessor instructionsManifestAccessor,
        IInstructionsOverridesAccessor instructionsOverridesAccessor,
        InstructionsBodyProjector instructionsBodyProjector,
        InstructionsFileReader instructionsFileReader,
        InstructionsFullTextSearchService instructionsFullTextSearchService,
        SnapshotBroadcaster<IReadOnlyList<JsonInstructionsListRow>> instructionsSnapshotBroadcaster,
        IMcpToolsRegistryAccessor mcpToolsRegistryAccessor,
        ILogger logger,
        IMcpToolsInvoker? mcpToolsInvoker = null)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(registryReader);
        ArgumentNullException.ThrowIfNull(logFileReader);
        ArgumentNullException.ThrowIfNull(logsBroadcaster);
        ArgumentNullException.ThrowIfNull(configAccessor);
        ArgumentNullException.ThrowIfNull(configUpdater);
        ArgumentNullException.ThrowIfNull(configBroadcaster);
        ArgumentNullException.ThrowIfNull(workspaceAccessor);
        ArgumentNullException.ThrowIfNull(instructionsManifestAccessor);
        ArgumentNullException.ThrowIfNull(instructionsOverridesAccessor);
        ArgumentNullException.ThrowIfNull(instructionsBodyProjector);
        ArgumentNullException.ThrowIfNull(instructionsFileReader);
        ArgumentNullException.ThrowIfNull(instructionsFullTextSearchService);
        ArgumentNullException.ThrowIfNull(instructionsSnapshotBroadcaster);
        ArgumentNullException.ThrowIfNull(mcpToolsRegistryAccessor);
        ArgumentNullException.ThrowIfNull(logger);

        _lifetime = lifetime;
        _registryReader = registryReader;
        _logFileReader = logFileReader;
        _logsBroadcaster = logsBroadcaster;
        _logFrameStream = new();
        _configAccessor = configAccessor;
        _configUpdater = configUpdater;
        _configBroadcaster = configBroadcaster;
        _configFrameStream = new();
        _workspaceAccessor = workspaceAccessor;
        _instructionsManifestAccessor = instructionsManifestAccessor;
        _instructionsBodyProjector = instructionsBodyProjector;
        _instructionsFileReader = instructionsFileReader;
        _instructionsFullTextSearchService = instructionsFullTextSearchService;
        _instructionsListProjector = new InstructionsListProjector(
            instructionsManifestAccessor, instructionsOverridesAccessor, configAccessor, workspaceAccessor);
        _instructionsSnapshotBroadcaster = instructionsSnapshotBroadcaster;
        _instructionsFrameStream = new();
        _mcpToolsRegistryAccessor = mcpToolsRegistryAccessor;
        _logger = logger;
        _mcpToolsInvoker = mcpToolsInvoker ?? McpToolsInvokerNoop.Instance;
    }

    public EndpointKind EndpointKind => EndpointKind.Rpc;

    public FrameFailurePolicy FrameFailurePolicy => FrameFailurePolicy.Recover;

    public void LogFrameReadFault(Exception exception) =>
        LogReadFaulted(_logger, exception);

    public void LogFrameWriteFault(Exception exception) =>
        LogWriteFaulted(_logger, exception);

    public void LogFrameParseFault(Exception exception) =>
        LogFrameParseFailed(_logger, exception);

    public void LogFrameInvalidRequest() =>
        LogInvalidRequest(_logger);

    public void LogConnectionClosedByPeer()
    {
        // Quiet by design: a post-handshake client disconnecting
        // cleanly between requests is normal behaviour, not a
        // diagnostic event worth recording.
    }

    public async ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        switch (request.Method)
        {
            case RegistryMethods.RegistryEntries:
                return await HandleRegistryEntriesAsync(cancellationToken)
                    .ConfigureAwait(false);

            case LogsMethods.GetEngine:
                return await HandleLogsGetEngineAsync(request, cancellationToken)
                    .ConfigureAwait(false);

            case LogsMethods.TailEngine:
                return HandleLogsTailEngine();

            case ConfigMethods.Get:
                return HandleConfigGet();

            case ConfigMethods.ToggleFile:
                return await HandleConfigToggleFileAsync(request, cancellationToken)
                    .ConfigureAwait(false);

            case ConfigMethods.ToggleRule:
                return await HandleConfigToggleRuleAsync(request, cancellationToken)
                    .ConfigureAwait(false);

            case ConfigMethods.Subscribe:
                return HandleConfigSubscribe();

            case WorkspaceMethods.Detect:
                return HandleWorkspaceDetect();

            case WorkspaceMethods.Info:
                return HandleWorkspaceInfo();

            case InstructionsMethods.List:
                return HandleInstructionsList(request);

            case InstructionsMethods.Categories:
                return HandleInstructionsCategories();

            case InstructionsMethods.Get:
                return await HandleInstructionsGetAsync(request, cancellationToken)
                    .ConfigureAwait(false);

            case InstructionsMethods.GetAll:
                return await HandleInstructionsGetAllAsync(cancellationToken)
                    .ConfigureAwait(false);

            case InstructionsMethods.GetAlwaysAttached:
                return await HandleInstructionsGetAlwaysAttachedAsync(cancellationToken)
                    .ConfigureAwait(false);

            case InstructionsMethods.GetRaw:
                return await HandleInstructionsGetRawAsync(request, cancellationToken)
                    .ConfigureAwait(false);

            case InstructionsMethods.SearchContent:
                return await HandleInstructionsSearchContentAsync(request, cancellationToken)
                    .ConfigureAwait(false);

            case InstructionsMethods.Subscribe:
                return HandleInstructionsSubscribe();

            case McpToolsMethods.List:
                return HandleMcpToolsList();

            case McpToolsMethods.Invoke:
                return await HandleMcpToolsInvokeAsync(request, cancellationToken)
                    .ConfigureAwait(false);

            case ProtocolMethods.Shutdown:
                return HandleShutdown();

            default:
                LogMethodNotFound(_logger, request.Method);
                return new UnaryHandlerResult(
                    Response: new JsonRpcResponse
                    {
                        Error = new JsonRpcError
                        {
                            Code = JsonRpcErrorCodes.MethodNotFound,
                            Message = $"Unknown method '{request.Method}'.",
                        },
                    },
                    Continuation: Continuation.Continue);
        }
    }

    private async Task<RpcHandlerResult> HandleRegistryEntriesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = await _registryReader.ReadAsync(cancellationToken)
                .ConfigureAwait(false);

            var result = new JsonRegistryEntriesResult { Entries = entries };
            var resultElement = JsonSerializer.SerializeToElement(
                result, ProtocolJsonContext.Default.JsonRegistryEntriesResult);

            return new UnaryHandlerResult(
                Response: new JsonRpcResponse { Result = resultElement },
                Continuation: Continuation.Continue);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRegistryEntriesFailed(_logger, ex);
            return new UnaryHandlerResult(
                Response: new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.InternalError,
                        Message = "Failed to read the engine registry.",
                    },
                },
                Continuation: Continuation.Continue);
        }
    }

    private async Task<RpcHandlerResult> HandleLogsGetEngineAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        JsonLogsGetEngineParams? parameters;

        try
        {
            parameters = request.Params is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } element
                ? element.Deserialize(ProtocolJsonContext.Default.JsonLogsGetEngineParams)
                : null;
        }
        catch (JsonException ex)
        {
            LogParamsParseFailed(_logger, LogsMethods.GetEngine, ex);
            return new UnaryHandlerResult(
                Response: new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.InvalidParams,
                        Message = $"Invalid params for '{LogsMethods.GetEngine}'.",
                    },
                },
                Continuation: Continuation.Continue);
        }

        if (parameters?.LastN is < 0)
        {
            LogLogsGetEngineRejectedNegativeLastN(_logger, parameters.LastN.GetValueOrDefault());
            return new UnaryHandlerResult(
                Response: new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.InvalidParams,
                        Message = "LastN must be non-negative.",
                    },
                },
                Continuation: Continuation.Continue);
        }

        try
        {
            var read = await _logFileReader.ReadAsync(parameters, cancellationToken)
                .ConfigureAwait(false);

            var result = new JsonLogsGetEngineResult
            {
                Records = read.Records,
                Truncated = read.Truncated,
            };

            var resultElement = JsonSerializer.SerializeToElement(
                result, ProtocolJsonContext.Default.JsonLogsGetEngineResult);

            return new UnaryHandlerResult(
                Response: new JsonRpcResponse { Result = resultElement },
                Continuation: Continuation.Continue);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLogsGetEngineFailed(_logger, ex);
            return new UnaryHandlerResult(
                Response: new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.InternalError,
                        Message = "Failed to read the engine log.",
                    },
                },
                Continuation: Continuation.Continue);
        }
    }

    [SuppressMessage("Reliability", "CA2000",
        Justification = "Ownership of the subscription is handed off to StreamingHandlerResult.PostFlush, which the RpcConnectionProcessor runs in a finally block — disposal is guaranteed on every path.")]
    private StreamingHandlerResult HandleLogsTailEngine()
    {
        // Subscription is created up-front so its disposal can be
        // routed through StreamingHandlerResult.PostFlush, which
        // the processor runs in a finally — guaranteeing the
        // broadcaster slot is released even when the peer hangs
        // up mid-stream or the iterator faults.
        var subscription = _logsBroadcaster.Subscribe();

        return new StreamingHandlerResult(
            Payloads: MapFramesAsync(subscription),
            PostFlush: () =>
            {
                subscription.Dispose();
                return Task.CompletedTask;
            });
    }

    private async IAsyncEnumerable<JsonElement> MapFramesAsync(
        BroadcasterSubscription<JsonLogRecord> subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in _logFrameStream
            .StreamAsync(subscription, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return JsonSerializer.SerializeToElement(
                frame, ProtocolJsonContext.Default.JsonLogStreamFrame);
        }
    }

    private UnaryHandlerResult HandleConfigGet() => ConfigSnapshotResult();

    [SuppressMessage("Reliability", "CA2000",
        Justification = "Ownership of the subscription is handed off to StreamingHandlerResult.PostFlush, which the RpcConnectionProcessor runs in a finally block — disposal is guaranteed on every path.")]
    private StreamingHandlerResult HandleConfigSubscribe()
    {
        // Subscription is created up-front so its disposal can be
        // routed through StreamingHandlerResult.PostFlush, which
        // the processor runs in a finally — guaranteeing the
        // broadcaster slot is released even when the peer hangs
        // up mid-stream or the iterator faults.
        var subscription = _configBroadcaster.Subscribe();

        return new StreamingHandlerResult(
            Payloads: MapConfigFramesAsync(subscription),
            PostFlush: () =>
            {
                subscription.Dispose();
                return Task.CompletedTask;
            });
    }

    private async IAsyncEnumerable<JsonElement> MapConfigFramesAsync(
        BroadcasterSubscription<JsonConfigSnapshot> subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in _configFrameStream
            .StreamAsync(subscription, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return JsonSerializer.SerializeToElement(
                frame, ProtocolJsonContext.Default.JsonConfigStreamFrame);
        }
    }

    private async Task<RpcHandlerResult> HandleConfigToggleFileAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        JsonConfigToggleFileParams? parameters;

        try
        {
            parameters = request.Params is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } element
                ? element.Deserialize(ProtocolJsonContext.Default.JsonConfigToggleFileParams)
                : null;
        }
        catch (JsonException ex)
        {
            LogParamsParseFailed(_logger, ConfigMethods.ToggleFile, ex);
            return InvalidParams(ConfigMethods.ToggleFile);
        }

        if (string.IsNullOrWhiteSpace(parameters?.Name))
        {
            return InvalidParams(ConfigMethods.ToggleFile);
        }

        var name = parameters.Name;
        return await ApplyConfigEditAsync(
            ConfigMethods.ToggleFile,
            snapshot => snapshot.ToggleInstructionsFile(name),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<RpcHandlerResult> HandleConfigToggleRuleAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        JsonConfigToggleRuleParams? parameters;

        try
        {
            parameters = request.Params is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } element
                ? element.Deserialize(ProtocolJsonContext.Default.JsonConfigToggleRuleParams)
                : null;
        }
        catch (JsonException ex)
        {
            LogParamsParseFailed(_logger, ConfigMethods.ToggleRule, ex);
            return InvalidParams(ConfigMethods.ToggleRule);
        }

        if (string.IsNullOrWhiteSpace(parameters?.Name)
            || string.IsNullOrWhiteSpace(parameters.RuleId))
        {
            return InvalidParams(ConfigMethods.ToggleRule);
        }

        var name = parameters.Name;
        var ruleId = parameters.RuleId;
        return await ApplyConfigEditAsync(
            ConfigMethods.ToggleRule,
            snapshot => snapshot.ToggleInstructionsRule(name, ruleId),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<RpcHandlerResult> ApplyConfigEditAsync(
        string method,
        Func<ConfigSnapshot, ConfigSnapshot> edit,
        CancellationToken cancellationToken)
    {
        try
        {
            await _configUpdater.UpdateAsync(edit, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogConfigEditFailed(_logger, method, ex);
            return new UnaryHandlerResult(
                Response: new JsonRpcResponse
                {
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.InternalError,
                        Message = "Failed to update the engine config.",
                    },
                },
                Continuation: Continuation.Continue);
        }

        return ConfigSnapshotResult();
    }

    private UnaryHandlerResult ConfigSnapshotResult()
    {
        var snapshot = _configAccessor.Current.ToWireFormat();
        var resultElement = JsonSerializer.SerializeToElement(
            snapshot, ProtocolJsonContext.Default.JsonConfigSnapshot);

        return new UnaryHandlerResult(
            Response: new JsonRpcResponse { Result = resultElement },
            Continuation: Continuation.Continue);
    }

    private UnaryHandlerResult HandleWorkspaceDetect()
    {
        var result = _workspaceAccessor.Current.ToWireFormat();
        var resultElement = JsonSerializer.SerializeToElement(
            result, ProtocolJsonContext.Default.JsonWorkspaceDetectResult);

        return new UnaryHandlerResult(
            Response: new JsonRpcResponse { Result = resultElement },
            Continuation: Continuation.Continue);
    }

    private UnaryHandlerResult HandleWorkspaceInfo()
    {
        var result = new JsonWorkspaceInfoResult
        {
            EngineVersion = EngineVersion.Value,
            IdleTimeout = _workspaceAccessor.EngineInfo.IdleTimeout,
            InstanceId = _workspaceAccessor.EngineInfo.InstanceId,
            InstanceLabel = _workspaceAccessor.EngineInfo.InstanceLabel,
            Revision = _workspaceAccessor.Revision,
        };
        var resultElement = JsonSerializer.SerializeToElement(
            result, ProtocolJsonContext.Default.JsonWorkspaceInfoResult);

        return new UnaryHandlerResult(
            Response: new JsonRpcResponse { Result = resultElement },
            Continuation: Continuation.Continue);
    }

    private static UnaryHandlerResult InvalidParams(string method) =>
        new(
            Response: new JsonRpcResponse
            {
                Error = new JsonRpcError
                {
                    Code = JsonRpcErrorCodes.InvalidParams,
                    Message = $"Invalid params for '{method}'.",
                },
            },
            Continuation: Continuation.Continue);

    private UnaryHandlerResult HandleShutdown()
    {
        var result = new JsonShutdownResult { Accepted = true };
        var resultElement = JsonSerializer.SerializeToElement(
            result, ProtocolJsonContext.Default.JsonShutdownResult);

        return new UnaryHandlerResult(
            Response: new JsonRpcResponse { Result = resultElement },
            Continuation: Continuation.Complete,
            PostFlush: () =>
            {
                LogShutdownRequested(_logger);
                _lifetime.StopApplication();
                return Task.CompletedTask;
            });
    }

    [LoggerMessage(EventId = 50, Level = LogLevel.Debug,
        Message = "RPC dispatch saw unknown method '{Method}'.")]
    private static partial void LogMethodNotFound(ILogger logger, string method);

    [LoggerMessage(EventId = 51, Level = LogLevel.Warning,
        Message = "Engine.RegistryEntries handler failed to read the registry.")]
    private static partial void LogRegistryEntriesFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 52, Level = LogLevel.Information,
        Message = "Engine.Shutdown requested via RPC; initiating host stop.")]
    private static partial void LogShutdownRequested(ILogger logger);

    [LoggerMessage(EventId = 53, Level = LogLevel.Debug,
        Message = "RPC dispatch read faulted; closing connection.")]
    private static partial void LogReadFaulted(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 54, Level = LogLevel.Debug,
        Message = "RPC dispatch write faulted; closing connection.")]
    private static partial void LogWriteFaulted(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 55, Level = LogLevel.Debug,
        Message = "RPC frame failed to parse as JSON.")]
    private static partial void LogFrameParseFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 56, Level = LogLevel.Debug,
        Message = "RPC frame is not a valid JSON-RPC 2.0 request.")]
    private static partial void LogInvalidRequest(ILogger logger);

    [LoggerMessage(EventId = 57, Level = LogLevel.Debug,
        Message = "RPC dispatch could not parse params for '{Method}'.")]
    private static partial void LogParamsParseFailed(ILogger logger, string method, Exception exception);

    [LoggerMessage(EventId = 58, Level = LogLevel.Warning,
        Message = "Logs.GetEngine handler failed to read the engine log.")]
    private static partial void LogLogsGetEngineFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 59, Level = LogLevel.Debug,
        Message = "Logs.GetEngine rejected request with negative LastN={LastN}.")]
    private static partial void LogLogsGetEngineRejectedNegativeLastN(ILogger logger, int lastN);

    [LoggerMessage(EventId = 60, Level = LogLevel.Warning,
        Message = "Config edit handler '{Method}' failed to publish the update.")]
    private static partial void LogConfigEditFailed(ILogger logger, string method, Exception exception);
}
