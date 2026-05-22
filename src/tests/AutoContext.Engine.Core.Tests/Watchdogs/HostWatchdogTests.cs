namespace AutoContext.Engine.Core.Tests.Watchdogs;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Core.Tests.Support.Watchdogs;
using AutoContext.Engine.Core.Watchdogs;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using static AutoContext.Engine.Core.Tests.Support.Watchdogs.HostWatchdogFixture;

public sealed class HostWatchdogTests(HostWatchdogFixture fixture)
    : IClassFixture<HostWatchdogFixture>
{
    [Fact]
    public void Should_throw_when_constructed_with_null_options()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        var lookup = new FakeProcessLookup();

        Assert.Throws<ArgumentNullException>(() =>
            new HostWatchdog(
                null!,
                lifetime,
                lookup,
                NullLogger<HostWatchdog>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_application_lifetime()
    {
        var lookup = new FakeProcessLookup();

        Assert.Throws<ArgumentNullException>(() =>
            new HostWatchdog(
                Options.Create(CreateOptions()),
                null!,
                lookup,
                NullLogger<HostWatchdog>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_process_lookup()
    {
        using var lifetime = new FakeHostApplicationLifetime();

        Assert.Throws<ArgumentNullException>(() =>
            new HostWatchdog(
                Options.Create(CreateOptions()),
                lifetime,
                null!,
                NullLogger<HostWatchdog>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        var lookup = new FakeProcessLookup();

        Assert.Throws<ArgumentNullException>(() =>
            new HostWatchdog(
                Options.Create(CreateOptions()),
                lifetime,
                lookup,
                null!));
    }

    [Fact]
    public async Task Should_not_probe_when_parent_pid_is_unset()
    {
        var context = fixture.Create(parentPid: null);

        await context.Watchdog.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(NoFireBudget, TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(0, context.Lookup.TryOpenCallCount),
            () => Assert.Equal(0, context.Lifetime.StopApplicationCallCount));
    }

    [Fact]
    public async Task Should_fire_at_startup_when_parent_already_gone()
    {
        var context = fixture.Create(parentMissing: true);

        await context.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        await context.Lifetime.WaitForStopRequestedAsync(FireBudget);
        Assert.Multiple(
            () => Assert.Equal(1, context.Lookup.TryOpenCallCount),
            () => Assert.Equal(1, context.Lifetime.StopApplicationCallCount));
    }

    [Fact]
    public async Task Should_stay_armed_while_parent_is_alive()
    {
        var context = fixture.Create();
        await context.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        await Task.Delay(NoFireBudget, TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(1, context.Lookup.TryOpenCallCount),
            () => Assert.Equal(0, context.Lifetime.StopApplicationCallCount));
    }

    [Fact]
    public async Task Should_fire_when_parent_exits_after_startup()
    {
        var context = fixture.Create();
        await context.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        context.ParentHandle!.SignalExit();

        await context.Lifetime.WaitForStopRequestedAsync(FireBudget);
        Assert.Equal(1, context.Lifetime.StopApplicationCallCount);
    }

    [Fact]
    public async Task Should_fire_when_parent_wait_throws_unexpectedly()
    {
        var context = fixture.Create();
        await context.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        context.ParentHandle!.SignalWaitFailure(new InvalidOperationException("boom"));

        await context.Lifetime.WaitForStopRequestedAsync(FireBudget);
        Assert.Equal(1, context.Lifetime.StopApplicationCallCount);
    }

    [Fact]
    public async Task Should_not_fire_after_StopAsync_cancels_the_wait()
    {
        var context = fixture.Create();
        await context.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        await context.Watchdog.StopAsync(TestContext.Current.CancellationToken);
        context.ParentHandle!.SignalExit();
        await Task.Delay(NoFireBudget, TestContext.Current.CancellationToken);

        Assert.Equal(0, context.Lifetime.StopApplicationCallCount);
    }

    [Fact]
    public async Task Should_dispose_parent_handle_on_StopAsync()
    {
        var context = fixture.Create();
        await context.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        await context.Watchdog.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, context.ParentHandle!.DisposeCallCount);
    }

    [Fact]
    public async Task Should_be_idempotent_on_DisposeAsync()
    {
        var context = fixture.Create();
        await context.Watchdog.StartAsync(TestContext.Current.CancellationToken);

        await context.Watchdog.DisposeAsync();
        await context.Watchdog.DisposeAsync();

        Assert.Equal(0, context.Lifetime.StopApplicationCallCount);
    }
}
