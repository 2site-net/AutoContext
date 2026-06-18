namespace AutoContext.Engine.Core.Tests.Support.Endpoints;

using System.Diagnostics.CodeAnalysis;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Endpoints;
using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.McpTools;
using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Machine;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Core.Tests.Support.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Features.McpTools;
using AutoContext.Engine.Core.Tests.Support.Machine;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Tests.Support.Workspace.Context;
using AutoContext.Engine.Core.Watchdogs;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Context;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Messages.Logs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// Shared xUnit class fixture for tests that exercise a
/// <see cref="EndpointHostService"/> end-to-end. Each call to
/// <see cref="Create"/> returns a fresh <see cref="Context"/>
/// bundling the disposables required to drive the service. The
/// watchdog is wired with <see cref="EngineOptions.IdleTimeout"/>
/// of zero so its background timer never races test teardown. The
/// fixture tracks every produced disposable and tears them down in
/// the correct order once the test class completes.
/// </summary>
public sealed class EndpointHostServiceFixture : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _asyncTracked = [];
    private readonly List<IDisposable> _syncTracked = [];

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The full-text search service is owned by the constructed EndpointHostService, which the fixture tracks and disposes during teardown.")]
    internal Context Create(
        EngineOptions? options = null,
        RegistryFileReader? registryReader = null)
    {
        var resolvedOptions = options ?? CreateOptions();
        var lifetime = new FakeHostApplicationLifetime();
        var reader = registryReader ?? CreateRegistryReader();
        var stream = CreateEventStream(resolvedOptions);
        var notifier = CreateNotifier(resolvedOptions, stream);
        var watchdog = CreateWatchdog(resolvedOptions, lifetime);
        var instanceGuard = new FakeUniqueInstanceGuard();
        var logsBroadcaster = new Broadcaster<JsonLogRecord>(
            NullLogger<Broadcaster<JsonLogRecord>>.Instance, "logs-pipe");
        var logFileReader = new EngineLogFileReader(
            EngineCacheLayoutTestFactory.Create(resolvedOptions));
        var dispatchPolicyFactory = CreateDispatchPolicyFactory(
            lifetime, reader, logFileReader, logsBroadcaster);
        var service = new EndpointHostService(
            Options.Create(resolvedOptions),
            NullLoggerFactory.Instance,
            stream,
            notifier,
            watchdog,
            instanceGuard,
            logsBroadcaster,
            dispatchPolicyFactory);

        // Track in reverse dependency order so Dispose tears the
        // service down first, then the watchdog, then the lifetime.
        _asyncTracked.Add(service);
        _asyncTracked.Add(watchdog);
        _syncTracked.Add(lifetime);

        return new Context(resolvedOptions, lifetime, watchdog, service, logsBroadcaster);
    }

    public static EngineOptions CreateOptions() =>
        new()
        {
            WorkspacePath = EngineOptionsFakeData.GetWorkspacePath(),
            InstanceId = Guid.NewGuid(),
        };

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The full-text search service is owned by the constructed DispatchPolicy, whose owning host the fixture tracks and disposes during teardown.")]
    internal static DispatchPolicyFactory CreateDispatchPolicyFactory(
        IHostApplicationLifetime lifetime,
        RegistryFileReader? registryReader = null,
        EngineLogFileReader? logFileReader = null,
        Broadcaster<JsonLogRecord>? logsBroadcaster = null) =>
        new(
            lifetime,
            registryReader ?? CreateRegistryReader(),
            logFileReader ?? CreateLogFileReader(),
            logsBroadcaster ?? CreateLogsBroadcaster(),
            CreateConfigAccessor(),
            CreateConfigUpdater(),
            CreateConfigBroadcaster(),
            CreateWorkspaceAccessor(),
            CreateInstructionsManifestAccessor(),
            CreateInstructionsOverridesAccessor(),
            CreateInstructionsBodyProjector(),
            CreateInstructionsFileReader(),
            CreateInstructionsSearchService(),
            CreateInstructionsBroadcaster(),
            CreateMcpToolsRegistryAccessor(),
            CreateMcpToolsInvoker(),
            NullLogger<DispatchPolicy>.Instance);

    internal static IConfigSnapshotAccessor CreateConfigAccessor() =>
        new FakeConfigSnapshotAccessor();

    internal static IConfigUpdater CreateConfigUpdater() =>
        new FakeConfigSnapshotAccessor();

    internal static IConfigChangeNotifier CreateConfigChangeNotifier() =>
        new FakeConfigSnapshotAccessor();

    internal static SnapshotBroadcaster<JsonConfigSnapshot> CreateConfigBroadcaster() =>
        new(NullLogger<SnapshotBroadcaster<JsonConfigSnapshot>>.Instance, "Config.Subscribe");

    internal static SnapshotBroadcaster<IReadOnlyList<JsonInstructionsListRow>> CreateInstructionsBroadcaster() =>
        new(
            NullLogger<SnapshotBroadcaster<IReadOnlyList<JsonInstructionsListRow>>>.Instance,
            "Instructions.Subscribe");

    internal static IWorkspaceContextAccessor CreateWorkspaceAccessor() =>
        new FakeWorkspaceContextAccessor();

    internal static IInstructionsManifestAccessor CreateInstructionsManifestAccessor() =>
        new FakeInstructionsManifestAccessor();

    internal static IMcpToolsRegistryAccessor CreateMcpToolsRegistryAccessor() =>
        new FakeMcpToolsRegistryAccessor();

    internal static IMcpToolsInvoker CreateMcpToolsInvoker() =>
        new FakeMcpToolsInvoker();

    internal static IInstructionsOverridesAccessor CreateInstructionsOverridesAccessor() =>
        new FakeInstructionsOverridesAccessor();

    internal static InstructionsListProjector CreateInstructionsListProjector(
        IInstructionsManifestAccessor? manifest = null,
        IInstructionsOverridesAccessor? overrides = null,
        IConfigSnapshotAccessor? config = null,
        IWorkspaceContextAccessor? workspace = null) =>
        new(
            manifest ?? CreateInstructionsManifestAccessor(),
            overrides ?? CreateInstructionsOverridesAccessor(),
            config ?? CreateConfigAccessor(),
            workspace ?? CreateWorkspaceAccessor());

    internal static InstructionsBodyProjector CreateInstructionsBodyProjector(
        IInstructionsOverridesAccessor? overrides = null,
        IConfigSnapshotAccessor? config = null) =>
        new(
            CreateInstructionsDirectory(),
            overrides ?? CreateInstructionsOverridesAccessor(),
            config ?? CreateConfigAccessor());

    internal static InstructionsFileReader CreateInstructionsFileReader(
        IInstructionsOverridesAccessor? overrides = null) =>
        new(
            CreateInstructionsDirectory(),
            overrides ?? CreateInstructionsOverridesAccessor());

    internal static InstructionsFullTextSearchService CreateInstructionsSearchService(
        IInstructionsManifestAccessor? manifest = null,
        InstructionsBodyProjector? projector = null,
        IConfigSnapshotAccessor? config = null)
    {
        var resolvedConfig = config ?? CreateConfigAccessor();
        var resolvedManifest = manifest ?? CreateInstructionsManifestAccessor();
        var resolvedProjector = projector
            ?? CreateInstructionsBodyProjector(config: resolvedConfig);

        return new InstructionsFullTextSearchService(
            resolvedManifest,
            resolvedProjector,
            resolvedConfig,
            NullLogger<InstructionsFullTextSearchService>.Instance);
    }

    private static string CreateInstructionsDirectory() =>
        Path.Combine(
            Path.GetTempPath(),
            $"autocontext-instructions-{Guid.NewGuid():N}");

    internal static LifecycleEventStream CreateEventStream(EngineOptions? options = null) =>
        new(
            Options.Create(options ?? CreateOptions()),
            NullLogger<LifecycleEventStream>.Instance);

    internal static LifecycleNotifier CreateNotifier(
        EngineOptions? options = null,
        LifecycleEventStream? stream = null)
    {
        var resolved = options ?? CreateOptions();

        return new(
            stream ?? CreateEventStream(resolved),
            Options.Create(resolved));
    }

    public static RegistryFileReader CreateRegistryReader()
    {
        // A non-existent path is a valid input — the reader treats
        // "file missing" as an empty registry, so tests that do not
        // exercise Engine.RegistryEntries can use this default.
        var path = Path.Combine(
            Path.GetTempPath(),
            $"autocontext-engine-registry-{Guid.NewGuid():N}.json");

        return new RegistryFileReader(path, NullLogger<RegistryFileReader>.Instance);
    }

    internal static IdleTimeoutWatchdog CreateWatchdog(
        EngineOptions options,
        IHostApplicationLifetime lifetime)
    {
        // Clone with IdleTimeout=Zero so the gate is disabled
        // regardless of the caller's options. Tests that exercise
        // the live watchdog live in IdleTimeoutWatchdogTests.
        var resolved = new EngineOptions
        {
            WorkspacePath = options.WorkspacePath,
            InstanceId = options.InstanceId,
            IdleTimeout = TimeSpan.Zero,
        };

        return new IdleTimeoutWatchdog(
            Options.Create(resolved),
            lifetime,
            TimeProvider.System,
            NullLogger<IdleTimeoutWatchdog>.Instance);
    }

    internal static Broadcaster<JsonLogRecord> CreateLogsBroadcaster() =>
        new(NullLogger<Broadcaster<JsonLogRecord>>.Instance, "logs-pipe");

    internal static EngineLogFileReader CreateLogFileReader(EngineOptions? options = null) =>
        new(EngineCacheLayoutTestFactory.Create(options ?? CreateOptions()));

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Test fixture teardown must attempt every tracked disposable; failures are aggregated and rethrown so xUnit still reports them.")]
    public async ValueTask DisposeAsync()
    {
        List<Exception>? failures = null;

        foreach (var disposable in _asyncTracked)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }

        foreach (var disposable in _syncTracked)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(
                $"One or more disposables tracked by {nameof(EndpointHostServiceFixture)} failed to dispose.",
                failures);
        }
    }

    internal sealed record Context(
        EngineOptions EngineOptions,
        FakeHostApplicationLifetime Lifetime,
        IdleTimeoutWatchdog Watchdog,
        EndpointHostService Service,
        Broadcaster<JsonLogRecord> LogsBroadcaster);
}
