namespace AutoContext.Engine.Core.Rpc.Handlers;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging;

/// <summary>
/// The <c>Config.*</c> handler. Serves the engine-resolved config snapshot
/// (<c>Config.Get</c>), streams live snapshots to subscribers
/// (<c>Config.Subscribe</c>), and applies file- and rule-level toggles
/// (<c>Config.ToggleFile</c>, <c>Config.ToggleRule</c>). Schema-invalid
/// edits reply <see cref="JsonRpcErrorCodes.InvalidParams"/>; a faulted
/// update replies <see cref="JsonRpcErrorCodes.InternalError"/>; in every
/// case the connection keeps serving.
/// </summary>
internal sealed partial class ConfigRpcHandler : IRpcMethodHandler
{
    private readonly IConfigSnapshotAccessor _configAccessor;
    private readonly SnapshotBroadcaster<JsonConfigSnapshot> _configBroadcaster;
    private readonly ConfigFrameStream _configFrameStream = new();
    private readonly IConfigUpdater _configUpdater;
    private readonly ILogger<ConfigRpcHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigRpcHandler"/>
    /// class.
    /// </summary>
    public ConfigRpcHandler(
        IConfigSnapshotAccessor configAccessor,
        IConfigUpdater configUpdater,
        SnapshotBroadcaster<JsonConfigSnapshot> configBroadcaster,
        ILogger<ConfigRpcHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(configAccessor);
        ArgumentNullException.ThrowIfNull(configUpdater);
        ArgumentNullException.ThrowIfNull(configBroadcaster);
        ArgumentNullException.ThrowIfNull(logger);

        _configAccessor = configAccessor;
        _configUpdater = configUpdater;
        _configBroadcaster = configBroadcaster;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Methods { get; } =
        [ConfigMethods.Get, ConfigMethods.ToggleFile, ConfigMethods.ToggleRule, ConfigMethods.Subscribe];

    /// <inheritdoc />
    public async ValueTask<RpcHandlerResult> InvokeAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Method switch
        {
            ConfigMethods.ToggleFile => await HandleConfigToggleFileAsync(request, cancellationToken).ConfigureAwait(false),
            ConfigMethods.ToggleRule => await HandleConfigToggleRuleAsync(request, cancellationToken).ConfigureAwait(false),
            ConfigMethods.Subscribe => HandleConfigSubscribe(),
            _ => HandleConfigGet(),
        };
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Config edit handler '{Method}' failed to publish the update.")]
    private static partial void LogConfigEditFailed(ILogger logger, string method, Exception exception);

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
            return RpcMethodResults.InternalError("Failed to update the engine config.");
        }

        return ConfigSnapshotResult();
    }

    private UnaryHandlerResult ConfigSnapshotResult()
    {
        var snapshot = _configAccessor.Current.ToWireFormat();
        return RpcMethodResults.Success(snapshot, ProtocolJsonContext.Default.JsonConfigSnapshot);
    }

    private UnaryHandlerResult HandleConfigGet()
        => ConfigSnapshotResult();

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

    private async Task<RpcHandlerResult> HandleConfigToggleFileAsync(
        JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (RpcMethodResults.TryDeserialize(
                request,
                ConfigMethods.ToggleFile,
                ProtocolJsonContext.Default.JsonConfigToggleFileParams,
                _logger,
                out var parameters) is { } failure)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(parameters?.Name))
        {
            return RpcMethodResults.InvalidParams(ConfigMethods.ToggleFile);
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
        if (RpcMethodResults.TryDeserialize(
                request,
                ConfigMethods.ToggleRule,
                ProtocolJsonContext.Default.JsonConfigToggleRuleParams,
                _logger,
                out var parameters) is { } failure)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(parameters?.Name)
            || string.IsNullOrWhiteSpace(parameters.RuleId))
        {
            return RpcMethodResults.InvalidParams(ConfigMethods.ToggleRule);
        }

        var name = parameters.Name;
        var ruleId = parameters.RuleId;
        return await ApplyConfigEditAsync(
            ConfigMethods.ToggleRule,
            snapshot => snapshot.ToggleInstructionsRule(name, ruleId),
            cancellationToken).ConfigureAwait(false);
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
}
