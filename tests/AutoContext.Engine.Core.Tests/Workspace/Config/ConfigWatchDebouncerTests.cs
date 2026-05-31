namespace AutoContext.Engine.Core.Tests.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config;

using Microsoft.Extensions.Time.Testing;

public sealed class ConfigWatchDebouncerTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(100);

    private static async Task AdvanceUntilAsync(FakeTimeProvider time, Task target)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);

        while (!target.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            time.Advance(Window);
            await Task.Delay(5);
        }

        await target;
    }

    private static Task SettleAsync()
        => Task.Delay(25);

    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_null_callback()
        {
            // Arrange
            var time = new FakeTimeProvider();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => new ConfigWatchDebouncer(null!, time, Window));
        }

        [Fact]
        public void Should_reject_null_time_provider()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => new ConfigWatchDebouncer(_ => Task.CompletedTask, null!, Window));
        }

        [Fact]
        public void Should_reject_non_positive_delay()
        {
            // Arrange
            var time = new FakeTimeProvider();

            // Act + Assert
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ConfigWatchDebouncer(_ => Task.CompletedTask, time, TimeSpan.Zero));
        }
    }

    public sealed class Signal
    {
        [Fact]
        public async Task Should_reconcile_once_after_quiet_window()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var reconciled = 0;
            var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var debouncer = new ConfigWatchDebouncer(
                _ =>
                {
                    Interlocked.Increment(ref reconciled);
                    fired.TrySetResult();
                    return Task.CompletedTask;
                },
                time,
                Window);
            debouncer.Start();

            // Act
            debouncer.Signal();
            await AdvanceUntilAsync(time, fired.Task);

            // Assert
            Assert.Equal(1, reconciled);
        }

        [Fact]
        public async Task Should_collapse_burst_into_single_reconcile()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var reconciled = 0;
            var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var debouncer = new ConfigWatchDebouncer(
                _ =>
                {
                    Interlocked.Increment(ref reconciled);
                    fired.TrySetResult();
                    return Task.CompletedTask;
                },
                time,
                Window);

            // Act
            for (var i = 0; i < 5; i++)
            {
                debouncer.Signal();
            }

            debouncer.Start();
            await AdvanceUntilAsync(time, fired.Task);
            time.Advance(Window);
            await SettleAsync();

            // Assert
            Assert.Equal(1, reconciled);
        }

        [Fact]
        public async Task Should_reset_window_on_each_signal()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var reconciled = 0;
            var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var debouncer = new ConfigWatchDebouncer(
                _ =>
                {
                    Interlocked.Increment(ref reconciled);
                    fired.TrySetResult();
                    return Task.CompletedTask;
                },
                time,
                Window);
            debouncer.Start();

            // Act + Assert
            debouncer.Signal();
            await SettleAsync();
            time.Advance(TimeSpan.FromMilliseconds(60));
            await SettleAsync();
            Assert.Equal(0, reconciled);

            debouncer.Signal();
            await SettleAsync();
            time.Advance(TimeSpan.FromMilliseconds(50));
            await SettleAsync();
            Assert.Equal(0, reconciled);

            await AdvanceUntilAsync(time, fired.Task);
            Assert.Equal(1, reconciled);
        }
    }

    public sealed class Dispose
    {
        [Fact]
        public async Task Should_not_reconcile_when_disposed_before_window()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var reconciled = 0;
            var debouncer = new ConfigWatchDebouncer(
                _ =>
                {
                    Interlocked.Increment(ref reconciled);
                    return Task.CompletedTask;
                },
                time,
                Window);
            debouncer.Start();
            debouncer.Signal();
            await SettleAsync();

            // Act
            debouncer.Dispose();
            time.Advance(Window);
            await SettleAsync();

            // Assert
            Assert.Equal(0, reconciled);
        }

        [Fact]
        public void Should_be_idempotent_when_disposed_twice()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var debouncer = new ConfigWatchDebouncer(_ => Task.CompletedTask, time, Window);
            debouncer.Start();

            // Act + Assert
            debouncer.Dispose();
            debouncer.Dispose();
        }
    }
}
