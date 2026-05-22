namespace AutoContext.Engine.Core.Tests.Support.Watchdogs;

using System.Diagnostics.CodeAnalysis;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Core.Watchdogs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// Shared xUnit class fixture for tests that exercise
/// <see cref="IdleTimeoutWatchdog"/>. Each call to <see cref="Create"/>
/// returns a fresh <see cref="Context"/> wired against an isolated
/// <see cref="FakeHostApplicationLifetime"/> so tests do not share
/// state. The fixture tracks every produced disposable and tears
/// them down once the test class completes.
/// </summary>
public sealed class IdleTimeoutWatchdogFixture : IAsyncDisposable
{
    /// <summary>
    /// Default idle timeout used by <see cref="Create"/>. Short enough to
    /// keep tests fast, long enough that the watchdog cannot fire before
    /// the test has finished its arrange/act setup.
    /// </summary>
    public static readonly TimeSpan TestIdleTimeout = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Positive-assertion budget: must exceed <see cref="TestIdleTimeout"/>
    /// plus the watchdog grace period (2 s) by enough headroom for CI
    /// scheduling jitter.
    /// </summary>
    public static readonly TimeSpan FireBudget = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Negative-assertion budget: smaller than
    /// <see cref="TestIdleTimeout"/> plus the watchdog grace period so
    /// "did not fire" is meaningful, but large enough that a misbehaving
    /// timer would have fired by now.
    /// </summary>
    public static readonly TimeSpan NoFireBudget = TimeSpan.FromMilliseconds(750);

    private readonly List<IAsyncDisposable> _asyncTracked = [];
    private readonly List<IDisposable> _syncTracked = [];

    internal Context Create(TimeSpan? idleTimeout = null)
    {
        var lifetime = new FakeHostApplicationLifetime();
        var watchdog = CreateWatchdog(idleTimeout ?? TestIdleTimeout, lifetime);

        _asyncTracked.Add(watchdog);
        _syncTracked.Add(lifetime);

        return new Context(lifetime, watchdog);
    }

    public static EngineOptions CreateOptions(TimeSpan? idleTimeout = null) =>
        new()
        {
            WorkspacePath = EngineOptionsFakeData.GetWorkspacePath(),
            InstanceId = Guid.NewGuid(),
            IdleTimeout = idleTimeout ?? TestIdleTimeout,
        };

    internal static IdleTimeoutWatchdog CreateWatchdog(
        TimeSpan idleTimeout,
        IHostApplicationLifetime lifetime) =>
        new(
            Options.Create(CreateOptions(idleTimeout)),
            lifetime,
            TimeProvider.System,
            NullLogger<IdleTimeoutWatchdog>.Instance);

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
                $"One or more disposables tracked by {nameof(IdleTimeoutWatchdogFixture)} failed to dispose.",
                failures);
        }
    }

    internal sealed record Context(
        FakeHostApplicationLifetime Lifetime,
        IdleTimeoutWatchdog Watchdog);
}
