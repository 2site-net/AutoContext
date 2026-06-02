namespace AutoContext.Engine.Core;

using AutoContext.Engine.Core.Infrastructure;
using AutoContext.Engine.Core.Infrastructure.Diagnostics;
using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Machine;
using AutoContext.Engine.Core.Machine.Housekeeping;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Watchdogs;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Composition root for <c>AutoContext.Engine.Core</c>. Registering
/// the engine with <see cref="AddAutoContextEngine"/> binds
/// <see cref="EngineOptions"/> into the host's options pipeline,
/// installs the shape validator, and prepares the DI graph for the
/// engine's hosted services. The engine binary's <c>Program.Main</c>
/// and every test harness call this method; nothing else does.
/// </summary>
/// <remarks>
/// Per <c>design § Composition contracts</c> this method is the
/// engine library's <i>single</i> public entry point. Adding new
/// engine capabilities means adding new hosted services and DI
/// registrations behind this call, never new top-level extension
/// methods.
/// </remarks>
public static class EngineHostBuilderExtensions
{
    /// <summary>
    /// Registers the AutoContext engine on
    /// <paramref name="builder"/>'s service collection.
    /// </summary>
    /// <param name="builder">Host application builder to extend.
    /// Must not be <see langword="null"/>.</param>
    /// <param name="configure">Callback that mutates the
    /// <see cref="EngineOptions"/> instance before it is validated.
    /// The callback runs once when the options pipeline first
    /// materialises the instance. Must not be <see langword="null"/>.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="configure"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static IHostApplicationBuilder AddAutoContextEngine(
        this IHostApplicationBuilder builder,
        Action<EngineOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<EngineOptions>()
            .Configure(configure)
            .ValidateOnStart();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<EngineOptions>, EngineOptionsValidator>());

        // Clock source for hosted services that stamp wire-visible
        // timestamps (RegistryFileService's own-entry row et al.).
        // Idempotent — a host that already has a TimeProvider keeps
        // its own.
        builder.Services.TryAddSingleton(TimeProvider.System);

        // Registration order encodes the stop order (hosted
        // services stop in reverse). The log pipeline registers
        // first so it stops LAST — every other hosted service,
        // including RegistryFileService and the watchdogs below,
        // can still log through the live drain loop during its
        // own StopAsync. RegistryFileService then stops next-to-
        // last so future hosted writers (housekeeping,
        // crash-writers) registered after it can still await one
        // final WriteAsync through its still-live channel. The
        // file service itself owns the lifecycle of this
        // engine's own row — append on Start, best-effort
        // remove on Stop — so the writer/file-service split
        // that earlier drafts proposed is collapsed into a
        // single service.

        // Engine logging pipeline: LogChannel is the
        // in-process ingest channel; LogFileSinkService drains it
        // into <cacheRoot>/<workspaceHash>/<instanceId>/logs/engine.log.
        // Registered BEFORE RegistryFileService so it stops AFTER
        // it — hosted services stop in reverse registration order,
        // so the registry's own teardown can still log through
        // the live drain loop; the file service then has the
        // final word on its own row. The worker-bound producer
        // (Engine.WriteLog) lands in a later phase.
        builder.Services.TryAddSingleton<LogChannel>();

        // Per-instance identity bundle: workspace hash, instance id,
        // and the resolved cache-root subtree. Every consumer that
        // needs a path under the cache root composes off this
        // singleton (directly, or via EngineCacheLayout below).
        builder.Services.TryAddSingleton<CacheRoot>();

        // Single source of truth for every on-disk path the engine
        // owns under its cache root — per-instance logs / crash
        // tombstone, plus the shared liveness registry file at the
        // cache-root level. Producers and consumers resolve through
        // this singleton so each path is defined once.
        builder.Services.TryAddSingleton<EngineCacheLayout>();

        // Forward-pass NDJSON reader over the active engine.log,
        // consumed by the Logs.GetEngine RPC handler.
        builder.Services.TryAddSingleton<EngineLogFileReader>();

        // Rotation + retention support for the file sink. The
        // thresholds factory pins itself to the resolved
        // EngineOptions.Logging verbosity at first resolve; the
        // singletons composed below are read-only after startup.
        // RetentionPolicy is the sole reader of
        // EngineOptions.Retention — both the rotated-log cleaner
        // here and the cross-instance subtree cleaner consult it
        // instead of reading the option directly.
        builder.Services.TryAddSingleton<RetentionPolicy>();
        builder.Services.TryAddSingleton(sp =>
        {
            var verbosity = sp.GetRequiredService<IOptions<EngineOptions>>().Value.Logging;
            return LogRotationThresholds.ForVerbosity(verbosity);
        });
        builder.Services.TryAddSingleton<RotatedLogCleaner>();

        // Logs-pipe fan-out broadcaster. Sibling consumer of every
        // record drained by LogFileSinkService — file sink and
        // broadcaster receive each record symmetrically. Per-
        // subscriber bounded buffers and slow-subscriber drop
        // shield the file sink from a stalled pipe consumer.
        // Registered as a singleton so LifecycleService's logs-pipe
        // pump and the file sink share the same instance.
        builder.Services.TryAddSingleton(sp => new Broadcaster<JsonLogRecord>(
            sp.GetRequiredService<ILogger<Broadcaster<JsonLogRecord>>>(),
            "logs-pipe"));

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, LogFileSinkService>());

        // Engine ILogger<T> → LogChannel routing. Registered as an
        // ILoggerProvider on the host's logging pipeline so every
        // engine record materialises as a LogRecord on the channel
        // (alongside the framework default console/debug providers
        // Host.CreateApplicationBuilder installs). The provider
        // does not own the channel — the file-sink service above
        // is the channel's terminator — so this registration's
        // order relative to LogFileSinkService is immaterial for
        // teardown.
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider, EngineLoggerProvider>());

        builder.Services.TryAddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EngineOptions>>().Value;
            var clock = sp.GetRequiredService<TimeProvider>();
            var layout = sp.GetRequiredService<EngineCacheLayout>();
            return new RegistryFileService(
                layout.RegistryFilePath,
                serviceOptions: null,
                readerOptions: null,
                loggerFactory: sp.GetService<ILoggerFactory>(),
                ownEntryFactory: () => RegistryEntryBuilder.Build(options, clock));
        });
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, RegistryFileService>(
                sp => sp.GetRequiredService<RegistryFileService>()));

        // Stateless concurrent reader used by the RPC dispatcher's
        // Engine.RegistryEntries handler. Bound to the same
        // resolved registry path as the writer so the two see the
        // same file.
        builder.Services.TryAddSingleton(sp =>
        {
            var layout = sp.GetRequiredService<EngineCacheLayout>();
            return new RegistryFileReader(
                layout.RegistryFilePath,
                options: null,
                logger: sp.GetService<ILogger<RegistryFileReader>>());
        });

        // Liveness-aware view over the registry: composes the
        // stateless RegistryFileReader above with IProcessLookup
        // (registered alongside HostWatchdog further down) and tags
        // each entry Live/Stale via Process.StartTime comparison.
        // CacheRootScanner consumes this to derive the registration
        // half of its SubtreeRegistryStatus output.
        builder.Services.TryAddSingleton<RegistryEntryReader>();

        // Housekeeping cache-root scanner: walks the cache root
        // once and classifies every child directory into one of
        // four SubtreeRegistryStatus arms by composing
        // RegistryEntryReader with a structural shape check.
        // Pure read + classification — no deletion. Consumed by
        // StaleSubtreeCleaner and the HousekeepingService
        // shutdown sweep below.
        builder.Services.TryAddSingleton<CacheRootScanner>();

        // Pattern-matches the scanner's SubtreeRegistryStatus
        // output and deletes each subtree whose retention window
        // has elapsed. Registered subtrees are never touched;
        // stale-registration subtrees honour the entry's own
        // retention; unregistered and foreign subtrees fall back
        // to this engine's --retention via RetentionPolicy.
        builder.Services.TryAddSingleton<StaleSubtreeCleaner>();

        // HousekeepingService is the only hosted service in the
        // engine that runs work in StopAsync only (no startup
        // sweep — every spawn gets a fresh <instanceId>).
        // Registered AFTER RegistryFileService and BEFORE
        // LifecycleService so the host stops it BEFORE the
        // registry file service tears down — the sweep observes
        // the on-disk registry in its post-pipe-close shape
        // while RegistryFileService's channel is still live —
        // and AFTER LifecycleService closes the four pipes.
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, HousekeepingService>());

        builder.Services.TryAddSingleton<LifecycleEventStream>();
        builder.Services.TryAddSingleton<LifecycleNotifier>();

        // Idle-timeout watchdog: registered as a singleton (so
        // LifecycleService can inject it directly for keep-alive
        // accounting) and as an IHostedService (so it arms its
        // countdown on host start and disarms on host stop).
        // Registered BEFORE LifecycleService so it stops AFTER it
        // — LifecycleService's StopAsync tears down accept loops
        // first, then the watchdog cancels its timer.
        builder.Services.TryAddSingleton<IdleTimeoutWatchdog>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, IdleTimeoutWatchdog>(
                sp => sp.GetRequiredService<IdleTimeoutWatchdog>()));

        // Parent-process watchdog. Standalone hosted service —
        // no per-connection coupling — clamps engine lifetime to
        // the spawner's lifetime when --parent-pid is set, no-op
        // otherwise. Registered after the idle watchdog so it
        // stops in the same window and before LifecycleService so
        // its StopAsync runs after the dispatcher tears down.
        builder.Services.TryAddSingleton<IProcessLookup, SystemProcessLookup>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, HostWatchdog>());

        // Pre-bind unique-instance guard: LifecycleService
        // resolves this and invokes EnsureUniqueAsync at the top
        // of StartAsync, before any pipe bind, so a launcher-bug
        // instance-id collision surfaces as a clear diagnostic
        // instead of an opaque pipe-bind error.
        // PipeTransport is the connect primitive the guard's
        // probe rides on; registered as a singleton because the
        // type is stateless and depended on by both the guard
        // and (later) the registry-sweep liveness probes.
        builder.Services.TryAddSingleton<PipeTransport>();
        builder.Services.TryAddSingleton<IUniqueInstanceGuard, PerWorkspaceInstanceGuard>();

        // Workspace config store. The manager owns the in-memory
        // .autocontext.json snapshot for this workspace; it is the
        // singleton source both the Config.Get RPC handler (via the
        // IConfigSnapshotAccessor read seam) and future config writers
        // resolve. ConfigFileService loads the snapshot from disk
        // and arms the file watcher at startup. Registered BEFORE
        // LifecycleService so it starts first — the snapshot is
        // populated before the first rpc connection can issue
        // Config.Get — and stops after the pipes are torn down. The
        // manager is an IDisposable singleton the container disposes
        // on host stop.
        builder.Services.TryAddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EngineOptions>>().Value;
            return new ConfigFileManager(
                options.WorkspacePath,
                EngineVersion.Value,
                logger: sp.GetService<ILogger<ConfigFileManager>>(),
                timeProvider: sp.GetRequiredService<TimeProvider>());
        });
        builder.Services.TryAddSingleton<IConfigSnapshotAccessor>(
            sp => sp.GetRequiredService<ConfigFileManager>());
        builder.Services.TryAddSingleton<IConfigUpdater>(
            sp => sp.GetRequiredService<ConfigFileManager>());

        // Config-subscription fan-out broadcaster. Singleton so the
        // RPC Config.Subscribe handler and the ConfigFileService
        // change-event bridge share one instance: the bridge primes
        // the snapshot seed and publishes every change, the handler
        // enrolls snapshot-seeded subscribers.
        builder.Services.TryAddSingleton(sp => new SnapshotBroadcaster<JsonConfigSnapshot>(
            sp.GetRequiredService<ILogger<SnapshotBroadcaster<JsonConfigSnapshot>>>(),
            "Config.Subscribe"));
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, ConfigFileService>());

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, LifecycleService>());

        return builder;
    }
}
