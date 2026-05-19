namespace AutoContext.Engine.Core.Tests.Lifecycle;

using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Tests.Testing.Fakes;
using AutoContext.Engine.Core.Tests.Testing.Utils;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using static AutoContext.Engine.Core.Tests.Testing.Utils.IdleTimeoutWatchdogHarness;

public sealed class IdleTimeoutWatchdogTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_options()
    {
        using var lifetime = new FakeHostApplicationLifetime();

        Assert.Throws<ArgumentNullException>(() =>
            new IdleTimeoutWatchdog(
                null!,
                lifetime,
                TimeProvider.System,
                NullLogger<IdleTimeoutWatchdog>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_application_lifetime()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new IdleTimeoutWatchdog(
                Options.Create(CreateOptions()),
                null!,
                TimeProvider.System,
                NullLogger<IdleTimeoutWatchdog>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_time_provider()
    {
        using var lifetime = new FakeHostApplicationLifetime();

        Assert.Throws<ArgumentNullException>(() =>
            new IdleTimeoutWatchdog(
                Options.Create(CreateOptions()),
                lifetime,
                null!,
                NullLogger<IdleTimeoutWatchdog>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        using var lifetime = new FakeHostApplicationLifetime();

        Assert.Throws<ArgumentNullException>(() =>
            new IdleTimeoutWatchdog(
                Options.Create(CreateOptions()),
                lifetime,
                TimeProvider.System,
                null!));
    }

    [Fact]
    public async Task Should_fire_after_idle_timeout_when_no_keep_alive_holders()
    {
        await using var harness = Create();

        await harness.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        await harness.Lifetime.WaitForStopRequestedAsync(FireBudget);
        Assert.Equal(1, harness.Lifetime.StopApplicationCallCount);
    }

    [Fact]
    public async Task Should_not_fire_when_idle_timeout_is_zero()
    {
        await using var harness = Create(TimeSpan.Zero);

        await harness.Watchdog.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(NoFireBudget, TestContext.Current.CancellationToken);

        Assert.Equal(0, harness.Lifetime.StopApplicationCallCount);
    }

    [Fact]
    public async Task Should_return_noop_token_when_idle_timeout_is_zero()
    {
        await using var harness = Create(TimeSpan.Zero);
        await harness.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        await using var token1 = await harness.Watchdog.AcquireKeepAliveAsync();
        await using var token2 = await harness.Watchdog.AcquireKeepAliveAsync();
        await Task.Delay(NoFireBudget, TestContext.Current.CancellationToken);

        Assert.Equal(0, harness.Lifetime.StopApplicationCallCount);
    }

    [Fact]
    public async Task Should_disarm_when_keep_alive_holder_acquires()
    {
        await using var harness = Create();
        await harness.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        await using (await harness.Watchdog.AcquireKeepAliveAsync())
        {
            await Task.Delay(FireBudget, TestContext.Current.CancellationToken);

            Assert.Equal(0, harness.Lifetime.StopApplicationCallCount);
        }
    }

    [Fact]
    public async Task Should_re_arm_when_last_keep_alive_holder_releases()
    {
        // Arrange
        await using var harness = Create();
        await harness.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        var token = await harness.Watchdog.AcquireKeepAliveAsync();
        await Task.Delay(NoFireBudget, TestContext.Current.CancellationToken);
        Assert.Equal(0, harness.Lifetime.StopApplicationCallCount);

        // Act
        await token.DisposeAsync();

        // Assert
        await harness.Lifetime.WaitForStopRequestedAsync(FireBudget);
        Assert.Equal(1, harness.Lifetime.StopApplicationCallCount);
    }

    [Fact]
    public async Task Should_stay_disarmed_while_at_least_one_holder_remains()
    {
        // Arrange
        await using var harness = Create();
        await harness.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        var first = await harness.Watchdog.AcquireKeepAliveAsync();
        var second = await harness.Watchdog.AcquireKeepAliveAsync();

        // Act
        await first.DisposeAsync();
        await Task.Delay(FireBudget, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, harness.Lifetime.StopApplicationCallCount);

        await second.DisposeAsync();
    }

    [Fact]
    public async Task Should_not_fire_after_StopAsync()
    {
        // Arrange
        await using var harness = Create();
        await harness.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        // Act
        await harness.Watchdog.StopAsync(TestContext.Current.CancellationToken);
        await Task.Delay(FireBudget, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, harness.Lifetime.StopApplicationCallCount);
    }

    [Fact]
    public async Task Should_be_idempotent_on_disposing_keep_alive_token_twice()
    {
        // Arrange
        await using var harness = Create();
        await harness.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        var holderA = await harness.Watchdog.AcquireKeepAliveAsync();
        var holderB = await harness.Watchdog.AcquireKeepAliveAsync();

        // Act
        await holderA.DisposeAsync();
        await holderA.DisposeAsync();

        // Assert
        await Task.Delay(FireBudget, TestContext.Current.CancellationToken);
        Assert.Equal(0, harness.Lifetime.StopApplicationCallCount);

        await holderB.DisposeAsync();
    }
}
