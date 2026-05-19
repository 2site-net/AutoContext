namespace AutoContext.Engine.Core.Tests.Testing.Utils;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Tests.Testing.Fakes;
using AutoContext.Engine.Core.Tests.Testing.Fixtures;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// Disposable bundle wiring an <see cref="IdleTimeoutWatchdog"/> against a
/// <see cref="FakeHostApplicationLifetime"/> for tests. Keeps construction,
/// budget tuning, and teardown out of the test class body.
/// </summary>
internal sealed class IdleTimeoutWatchdogHarness : IAsyncDisposable
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

    private IdleTimeoutWatchdogHarness(
        FakeHostApplicationLifetime lifetime,
        IdleTimeoutWatchdog watchdog)
    {
        Lifetime = lifetime;
        Watchdog = watchdog;
    }

    public FakeHostApplicationLifetime Lifetime { get; }

    public IdleTimeoutWatchdog Watchdog { get; }

    public static IdleTimeoutWatchdogHarness Create(TimeSpan? idleTimeout = null)
    {
        var lifetime = new FakeHostApplicationLifetime();
        var watchdog = CreateWatchdog(idleTimeout ?? TestIdleTimeout, lifetime);

        return new IdleTimeoutWatchdogHarness(lifetime, watchdog);
    }

    public static EngineOptions CreateOptions(TimeSpan? idleTimeout = null) =>
        new()
        {
            WorkspacePath = EngineOptionsFakeData.GetWorkspacePath(),
            InstanceId = Guid.NewGuid(),
            IdleTimeout = idleTimeout ?? TestIdleTimeout,
        };

    public static IdleTimeoutWatchdog CreateWatchdog(
        TimeSpan idleTimeout,
        IHostApplicationLifetime lifetime) =>
        new(
            Options.Create(CreateOptions(idleTimeout)),
            lifetime,
            TimeProvider.System,
            NullLogger<IdleTimeoutWatchdog>.Instance);

    public async ValueTask DisposeAsync()
    {
        await Watchdog.DisposeAsync().ConfigureAwait(false);
        Lifetime.Dispose();
    }
}
