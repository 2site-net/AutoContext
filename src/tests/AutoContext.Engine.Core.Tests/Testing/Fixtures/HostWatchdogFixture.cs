namespace AutoContext.Engine.Core.Tests.Testing.Fixtures;

using System.Diagnostics.CodeAnalysis;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Diagnostics;
using AutoContext.Engine.Core.Tests.Testing.Fakes;
using AutoContext.Engine.Core.Tests.Testing.Utils;
using AutoContext.Engine.Core.Watchdogs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// Shared xUnit class fixture for tests that exercise
/// <see cref="HostWatchdog"/>. Each call to
/// <see cref="Create"/> returns a fresh <see cref="Context"/> wired
/// against an isolated <see cref="FakeHostApplicationLifetime"/> and
/// <see cref="FakeProcessLookup"/> so tests do not share state. The
/// fixture tracks every produced disposable and tears them down once
/// the test class completes.
/// </summary>
public sealed class HostWatchdogFixture : IAsyncDisposable
{
    /// <summary>
    /// Default parent pid used by <see cref="Create"/>. Arbitrary
    /// positive integer — the lookup is faked, so the actual value
    /// does not need to match a live OS process.
    /// </summary>
    public const int TestParentPid = 4242;

    /// <summary>
    /// Positive-assertion budget: large enough for CI scheduling
    /// jitter when waiting for the watchdog to fire after a
    /// <see cref="FakeProcessHandle.SignalExit"/> call.
    /// </summary>
    public static readonly TimeSpan FireBudget = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Negative-assertion budget: long enough that a misbehaving
    /// watchdog would have fired by now.
    /// </summary>
    public static readonly TimeSpan NoFireBudget = TimeSpan.FromMilliseconds(500);

    private readonly List<IAsyncDisposable> _asyncTracked = [];
    private readonly List<IDisposable> _syncTracked = [];

    internal Context Create(
        int? parentPid = TestParentPid,
        bool parentMissing = false)
    {
        var lifetime = new FakeHostApplicationLifetime();
        var lookup = new FakeProcessLookup();
        FakeProcessHandle? handle = null;

        if (parentPid is { } pid && !parentMissing)
        {
            handle = new FakeProcessHandle(DateTime.UtcNow.AddMinutes(-5));
            lookup.Register(pid, handle);
        }
        else if (parentPid is { } missingPid)
        {
            lookup.Register(missingPid, null);
        }

        var watchdog = CreateWatchdog(parentPid, lifetime, lookup);

        _asyncTracked.Add(watchdog);
        _syncTracked.Add(lifetime);

        return new Context(lifetime, lookup, handle, watchdog);
    }

    public static EngineOptions CreateOptions(int? parentPid = TestParentPid) =>
        new()
        {
            WorkspacePath = EngineOptionsFakeData.GetWorkspacePath(),
            InstanceId = Guid.NewGuid(),
            ParentProcessId = parentPid,
        };

    internal static HostWatchdog CreateWatchdog(
        int? parentPid,
        IHostApplicationLifetime lifetime,
        IProcessLookup lookup) =>
        new(
            Options.Create(CreateOptions(parentPid)),
            lifetime,
            lookup,
            NullLogger<HostWatchdog>.Instance);

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
                $"One or more disposables tracked by {nameof(HostWatchdogFixture)} failed to dispose.",
                failures);
        }
    }

    /// <summary>
    /// A per-test bundle produced by <see cref="Create"/>.
    /// <see cref="ParentHandle"/> is <see langword="null"/> when the
    /// context was created with <c>parentMissing: true</c> to model
    /// "parent already gone".
    /// </summary>
    internal sealed record Context(
        FakeHostApplicationLifetime Lifetime,
        FakeProcessLookup Lookup,
        FakeProcessHandle? ParentHandle,
        HostWatchdog Watchdog);
}
