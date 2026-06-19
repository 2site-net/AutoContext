namespace AutoContext.Engine.Core.Tests.Support.Rpc.Policies;

using System.Diagnostics.CodeAnalysis;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.McpTools;
using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Rpc.Handlers;
using AutoContext.Engine.Core.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support.Endpoints;
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
/// dependency the policy composes to a <see cref="EndpointHostServiceFixture"/>
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
        IMcpToolsInvoker? mcpToolsInvoker = null,
        ILogger? logger = null)
    {
        var config = configAccessor ?? EndpointHostServiceFixture.CreateConfigAccessor();
        var overrides = overridesAccessor
            ?? EndpointHostServiceFixture.CreateInstructionsOverridesAccessor();
        var manifest = manifestAccessor
            ?? EndpointHostServiceFixture.CreateInstructionsManifestAccessor();
        var workspace = workspaceAccessor ?? EndpointHostServiceFixture.CreateWorkspaceAccessor();
        var projector = bodyProjector
            ?? EndpointHostServiceFixture.CreateInstructionsBodyProjector(overrides, config);
        var reader = fileReader
            ?? EndpointHostServiceFixture.CreateInstructionsFileReader(overrides);
        var search = searchService
            ?? EndpointHostServiceFixture.CreateInstructionsSearchService(manifest, projector, config);
        var mcpToolsHandler = new McpToolsRpcHandler(
            mcpToolsRegistryAccessor ?? EndpointHostServiceFixture.CreateMcpToolsRegistryAccessor(),
            mcpToolsInvoker ?? EndpointHostServiceFixture.CreateMcpToolsInvoker(),
            config,
            NullLogger<McpToolsRpcHandler>.Instance);
        var instructionsHandler = new InstructionsRpcHandler(
            manifest,
            EndpointHostServiceFixture.CreateInstructionsListProjector(manifest, overrides, config, workspace),
            projector,
            reader,
            search,
            instructionsBroadcaster ?? EndpointHostServiceFixture.CreateInstructionsBroadcaster(),
            config,
            NullLogger<InstructionsRpcHandler>.Instance);
        var configHandler = new ConfigRpcHandler(
            config,
            configUpdater ?? EndpointHostServiceFixture.CreateConfigUpdater(),
            configBroadcaster ?? EndpointHostServiceFixture.CreateConfigBroadcaster(),
            NullLogger<ConfigRpcHandler>.Instance);
        var logsHandler = new LogsRpcHandler(
            logFileReader ?? EndpointHostServiceFixture.CreateLogFileReader(),
            logsBroadcaster ?? EndpointHostServiceFixture.CreateLogsBroadcaster(),
            NullLogger<LogsRpcHandler>.Instance);
        var registryHandler = new RegistryRpcHandler(
            registryReader ?? EndpointHostServiceFixture.CreateRegistryReader(),
            NullLogger<RegistryRpcHandler>.Instance);
        var workspaceHandler = new WorkspaceRpcHandler(workspace);

        return new DispatchPolicy(
            lifetime,
            new IRpcMethodHandler[]
            {
                mcpToolsHandler,
                instructionsHandler,
                configHandler,
                logsHandler,
                registryHandler,
                workspaceHandler,
            },
            logger ?? NullLogger.Instance);
    }
}
