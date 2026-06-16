namespace AutoContext.Engine.Core.Tests.Support.Rpc.Policies;

using System.Diagnostics.CodeAnalysis;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.McpTools;
using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Context;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Messages.Logs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Builds <see cref="DispatchPolicy"/> instances for tests, defaulting every
/// dependency the policy composes to a <see cref="LifecycleServiceFixture"/>
/// helper so each test supplies only the collaborators it exercises. The
/// instructions deps (manifest, overrides, body projector, file reader,
/// full-text search) default to an empty corpus so non-instructions tests are
/// unaffected.
/// </summary>
internal static class DispatchPolicyTestFactory
{
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The full-text search service's lifetime is bound to the test; the constructed DispatchPolicy borrows it and the test owns disposal.")]
    public static DispatchPolicy Create(
        IHostApplicationLifetime lifetime,
        RegistryFileReader? registryReader = null,
        EngineLogFileReader? logFileReader = null,
        Broadcaster<JsonLogRecord>? logsBroadcaster = null,
        IConfigSnapshotAccessor? configAccessor = null,
        IConfigUpdater? configUpdater = null,
        SnapshotBroadcaster<JsonConfigSnapshot>? configBroadcaster = null,
        IWorkspaceContextAccessor? workspaceAccessor = null,
        IInstructionsManifestAccessor? manifestAccessor = null,
        IInstructionsOverridesAccessor? overridesAccessor = null,
        InstructionsBodyProjector? bodyProjector = null,
        InstructionsFileReader? fileReader = null,
        InstructionsFullTextSearchService? searchService = null,
        SnapshotBroadcaster<IReadOnlyList<JsonInstructionsListRow>>? instructionsBroadcaster = null,
        IMcpToolsRegistryAccessor? mcpToolsRegistryAccessor = null,
        ILogger? logger = null)
    {
        var config = configAccessor ?? LifecycleServiceFixture.CreateConfigAccessor();
        var overrides = overridesAccessor
            ?? LifecycleServiceFixture.CreateInstructionsOverridesAccessor();
        var manifest = manifestAccessor
            ?? LifecycleServiceFixture.CreateInstructionsManifestAccessor();
        var projector = bodyProjector
            ?? LifecycleServiceFixture.CreateInstructionsBodyProjector(overrides, config);
        var reader = fileReader
            ?? LifecycleServiceFixture.CreateInstructionsFileReader(overrides);
        var search = searchService
            ?? LifecycleServiceFixture.CreateInstructionsSearchService(manifest, projector, config);

        return new DispatchPolicy(
            lifetime,
            registryReader ?? LifecycleServiceFixture.CreateRegistryReader(),
            logFileReader ?? LifecycleServiceFixture.CreateLogFileReader(),
            logsBroadcaster ?? LifecycleServiceFixture.CreateLogsBroadcaster(),
            config,
            configUpdater ?? LifecycleServiceFixture.CreateConfigUpdater(),
            configBroadcaster ?? LifecycleServiceFixture.CreateConfigBroadcaster(),
            workspaceAccessor ?? LifecycleServiceFixture.CreateWorkspaceAccessor(),
            manifest,
            overrides,
            projector,
            reader,
            search,
            instructionsBroadcaster ?? LifecycleServiceFixture.CreateInstructionsBroadcaster(),
            mcpToolsRegistryAccessor ?? LifecycleServiceFixture.CreateMcpToolsRegistryAccessor(),
            logger ?? NullLogger.Instance);
    }
}
