namespace AutoContext.Engine.Core.Tests.Testing.Fixtures;

using System.Diagnostics.CodeAnalysis;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Testing.Fakes;
using AutoContext.Engine.Core.Tests.Testing.Utils;
using AutoContext.Engine.Core.Watchdogs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// Shared xUnit class fixture for tests that exercise a
/// <see cref="LifecycleService"/> end-to-end. Each call to
/// <see cref="Create"/> returns a fresh <see cref="Context"/>
/// bundling the disposables required to drive the service. The
/// watchdog is wired with <see cref="EngineOptions.IdleTimeout"/>
/// of zero so its background timer never races test teardown. The
/// fixture tracks every produced disposable and tears them down in
/// the correct order once the test class completes.
/// </summary>
public sealed class LifecycleServiceFixture : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _asyncTracked = [];
    private readonly List<IDisposable> _syncTracked = [];

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
        var service = new LifecycleService(
            Options.Create(resolvedOptions),
            NullLoggerFactory.Instance,
            lifetime,
            reader,
            stream,
            notifier,
            watchdog);

        // Track in reverse dependency order so Dispose tears the
        // service down first, then the watchdog, then the lifetime.
        _asyncTracked.Add(service);
        _asyncTracked.Add(watchdog);
        _syncTracked.Add(lifetime);

        return new Context(resolvedOptions, lifetime, watchdog, service);
    }

    public static EngineOptions CreateOptions() =>
        new()
        {
            WorkspacePath = EngineOptionsFakeData.GetWorkspacePath(),
            InstanceId = Guid.NewGuid(),
        };

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

        return new RegistryFileReader(path);
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
                $"One or more disposables tracked by {nameof(LifecycleServiceFixture)} failed to dispose.",
                failures);
        }
    }

    internal sealed record Context(
        EngineOptions EngineOptions,
        FakeHostApplicationLifetime Lifetime,
        IdleTimeoutWatchdog Watchdog,
        LifecycleService Service);
}
