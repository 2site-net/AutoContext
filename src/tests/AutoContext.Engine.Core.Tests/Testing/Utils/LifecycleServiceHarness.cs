namespace AutoContext.Engine.Core.Tests.Testing.Utils;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Testing.Fakes;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// Test-only bundle that owns the disposables required to drive a
/// <see cref="LifecycleService"/> end-to-end (lifetime, watchdog,
/// service) and disposes them in the correct order on teardown.
/// The watchdog is wired with <see cref="EngineOptions.IdleTimeout"/>
/// of zero so its background timer never races test teardown.
/// </summary>
internal sealed class LifecycleServiceHarness : IAsyncDisposable
{
    private LifecycleServiceHarness(
        EngineOptions options,
        FakeHostApplicationLifetime lifetime,
        IdleTimeoutWatchdog watchdog,
        LifecycleService service)
    {
        EngineOptions = options;
        Lifetime = lifetime;
        Watchdog = watchdog;
        Service = service;
    }

    public EngineOptions EngineOptions { get; }

    public FakeHostApplicationLifetime Lifetime { get; }

    public LifecycleService Service { get; }

    public IdleTimeoutWatchdog Watchdog { get; }

    public static LifecycleServiceHarness Create(
        EngineOptions? options = null,
        RegistryFileReader? registryReader = null)
    {
        var resolvedOptions = options ?? CreateOptions();
        var lifetime = new FakeHostApplicationLifetime();
        var reader = registryReader ?? CreateRegistryReader();
        var stream = CreateEventStream(resolvedOptions);
        var notifier = CreateNotifier(resolvedOptions, stream);
        var watchdog = CreateWatchdog(resolvedOptions, lifetime);
        var service = new LifecycleService(
            Microsoft.Extensions.Options.Options.Create(resolvedOptions),
            NullLoggerFactory.Instance,
            lifetime,
            reader,
            stream,
            notifier,
            watchdog);
        return new LifecycleServiceHarness(resolvedOptions, lifetime, watchdog, service);
    }

    public static EngineOptions CreateOptions() =>
        new()
        {
            WorkspacePath = EngineOptionsFakeData.GetWorkspacePath(),
            InstanceId = Guid.NewGuid(),
        };

    public static LifecycleEventStream CreateEventStream(EngineOptions? options = null) =>
        new(
            Microsoft.Extensions.Options.Options.Create(options ?? CreateOptions()),
            NullLogger<LifecycleEventStream>.Instance);

    public static LifecycleNotifier CreateNotifier(
        EngineOptions? options = null,
        LifecycleEventStream? stream = null)
    {
        var resolved = options ?? CreateOptions();
        return new(
            stream ?? CreateEventStream(resolved),
            Microsoft.Extensions.Options.Options.Create(resolved));
    }

    public static RegistryFileReader CreateRegistryReader()
    {
        // A non-existent path is a valid input — the reader treats
        // "file missing" as an empty registry, so tests that do not
        // exercise Engine.RegistryEntries can use this default.
        var path = Path.Combine(
            Path.GetTempPath(),
            $"autocontext-engine-registry-{Guid.NewGuid():N}.json");
        return new RegistryFileReader(path);
    }

    public static IdleTimeoutWatchdog CreateWatchdog(
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
            Microsoft.Extensions.Options.Options.Create(resolved),
            lifetime,
            TimeProvider.System,
            NullLogger<IdleTimeoutWatchdog>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await Service.DisposeAsync().ConfigureAwait(false);
        await Watchdog.DisposeAsync().ConfigureAwait(false);
        Lifetime.Dispose();
    }
}
