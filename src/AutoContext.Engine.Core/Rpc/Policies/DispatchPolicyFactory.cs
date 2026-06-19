namespace AutoContext.Engine.Core.Rpc.Policies;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Rpc.Handlers;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Context;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Messages.Logs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Constructs a fresh <see cref="DispatchPolicy"/> for each accepted
/// RPC connection.
/// </summary>
/// <remarks>
/// <para>
/// A new <see cref="DispatchPolicy"/> is created per connection because
/// the policy owns per-connection frame-stream state. This factory holds
/// the shared, DI-resolved leaf dependencies so the RPC endpoint host
/// does not have to forward them through its own constructor — it simply
/// calls <see cref="Create"/> whenever a connection arrives.
/// </para>
/// </remarks>
internal sealed class DispatchPolicyFactory
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly RegistryFileReader _registryReader;
    private readonly EngineLogFileReader _logFileReader;
    private readonly Broadcaster<JsonLogRecord> _logsBroadcaster;
    private readonly IConfigSnapshotAccessor _configAccessor;
    private readonly IConfigUpdater _configUpdater;
    private readonly SnapshotBroadcaster<JsonConfigSnapshot> _configBroadcaster;
    private readonly IWorkspaceContextAccessor _workspaceAccessor;
    private readonly IInstructionsManifestAccessor _instructionsManifestAccessor;
    private readonly IInstructionsOverridesAccessor _instructionsOverridesAccessor;
    private readonly InstructionsBodyProjector _instructionsBodyProjector;
    private readonly InstructionsFileReader _instructionsFileReader;
    private readonly InstructionsFullTextSearchService _instructionsFullTextSearchService;
    private readonly SnapshotBroadcaster<IReadOnlyList<JsonInstructionsListRow>> _instructionsSnapshotBroadcaster;
    private readonly IEnumerable<IRpcMethodHandler> _methodHandlers;
    private readonly ILogger<DispatchPolicy> _logger;

    public DispatchPolicyFactory(
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
        IEnumerable<IRpcMethodHandler> methodHandlers,
        ILogger<DispatchPolicy> logger)
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
        ArgumentNullException.ThrowIfNull(methodHandlers);
        ArgumentNullException.ThrowIfNull(logger);

        _lifetime = lifetime;
        _registryReader = registryReader;
        _logFileReader = logFileReader;
        _logsBroadcaster = logsBroadcaster;
        _configAccessor = configAccessor;
        _configUpdater = configUpdater;
        _configBroadcaster = configBroadcaster;
        _workspaceAccessor = workspaceAccessor;
        _instructionsManifestAccessor = instructionsManifestAccessor;
        _instructionsOverridesAccessor = instructionsOverridesAccessor;
        _instructionsBodyProjector = instructionsBodyProjector;
        _instructionsFileReader = instructionsFileReader;
        _instructionsFullTextSearchService = instructionsFullTextSearchService;
        _instructionsSnapshotBroadcaster = instructionsSnapshotBroadcaster;
        _methodHandlers = methodHandlers;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new <see cref="DispatchPolicy"/> bound to the shared
    /// engine dependencies, ready to serve a single RPC connection.
    /// </summary>
    public DispatchPolicy Create() =>
        new(
            _lifetime,
            _registryReader,
            _logFileReader,
            _logsBroadcaster,
            _configAccessor,
            _configUpdater,
            _configBroadcaster,
            _workspaceAccessor,
            _instructionsManifestAccessor,
            _instructionsOverridesAccessor,
            _instructionsBodyProjector,
            _instructionsFileReader,
            _instructionsFullTextSearchService,
            _instructionsSnapshotBroadcaster,
            _methodHandlers,
            _logger);
}
