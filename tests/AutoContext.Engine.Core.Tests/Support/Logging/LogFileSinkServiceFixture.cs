namespace AutoContext.Engine.Core.Tests.Support.Logging;

using System.Diagnostics.CodeAnalysis;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using static AutoContext.Engine.Core.Tests.Support.EngineCrashWriterFixture;

/// <summary>
/// Shared xUnit class fixture for tests that exercise a
/// <see cref="LogFileSinkService"/> end-to-end. Each call to
/// <see cref="Create"/> returns a fresh <see cref="Context"/>
/// bundling the service together with the collaborators a test
/// usually needs to drive it (channel, broadcaster, options). The
/// fixture tracks every produced service and the temp cache roots
/// it minted, then tears them down in the correct order — service
/// first, then directories — once the test class completes.
/// </summary>
public sealed class LogFileSinkServiceFixture : IDisposable
{
    private readonly List<IDisposable> _tracked = [];
    private readonly List<string> _trackedCacheRoots = [];

    internal Context Create(
        EngineOptions? options = null,
        LogChannel? channel = null,
        LogRotationThresholds? thresholds = null,
        LogSubscriptionBroadcaster? broadcaster = null,
        TimeProvider? timeProvider = null)
    {
        EngineOptions resolvedOptions;
        if (options is null)
        {
            resolvedOptions = CreateOptions();
            _trackedCacheRoots.Add(resolvedOptions.CacheRootOverride!);
        }
        else
        {
            resolvedOptions = options;
            if (!string.IsNullOrEmpty(resolvedOptions.CacheRootOverride))
            {
                _trackedCacheRoots.Add(resolvedOptions.CacheRootOverride);
            }
        }

        var resolvedChannel = channel ?? new LogChannel();
        var resolvedThresholds = thresholds ?? LogRotationThresholdsFakeData.Normal;
        var resolvedBroadcaster = broadcaster ?? LogSubscriptionBroadcasterTestFactory.Create();
        var resolvedClock = timeProvider ?? TimeProvider.System;

        var service = new LogFileSinkService(
            resolvedChannel,
            Options.Create(resolvedOptions),
            resolvedThresholds,
            RotatedLogCleanerTestFactory.Create(resolvedOptions, resolvedClock),
            resolvedBroadcaster,
            resolvedClock,
            NullLogger<LogFileSinkService>.Instance);

        _tracked.Add(service);

        return new Context(resolvedOptions, resolvedChannel, resolvedBroadcaster, service);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Test fixture teardown must attempt every tracked disposable; failures are aggregated and rethrown so xUnit still reports them.")]
    [SuppressMessage(
        "Design",
        "CA1065:Do not raise exceptions in unexpected locations",
        Justification = "Test fixture teardown must surface aggregated disposal failures to xUnit; swallowing them would hide real test-cleanup bugs.")]
    public void Dispose()
    {
        List<Exception>? failures = null;

        foreach (var disposable in _tracked)
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

        foreach (var cacheRoot in _trackedCacheRoots)
        {
            try
            {
                if (Directory.Exists(cacheRoot))
                {
                    Directory.Delete(cacheRoot, recursive: true);
                }
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(
                $"One or more disposables tracked by {nameof(LogFileSinkServiceFixture)} failed to dispose.",
                failures);
        }
    }

    internal sealed record Context(
        EngineOptions Options,
        LogChannel Channel,
        LogSubscriptionBroadcaster Broadcaster,
        LogFileSinkService Service);
}
