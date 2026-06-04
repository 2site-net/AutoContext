namespace AutoContext.Engine.Core.Tests.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

using Microsoft.Extensions.Time.Testing;

public sealed class ConfigBatchWriterTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(5);

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

    private static Func<ConfigSnapshot, ConfigSnapshot> AppendTool(string name)
        => config => config with
        {
            McpTools = [.. config.McpTools, new ConfigMcpTool { Name = name, Disabled = true }],
        };

    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_null_updater()
            => Assert.Throws<ArgumentNullException>(
                () => new ConfigBatchWriter(null!, new FakeTimeProvider(), Window));

        [Fact]
        public void Should_reject_non_positive_batch_window()
            => Assert.Throws<ArgumentOutOfRangeException>(
                () => new ConfigBatchWriter(new FakeConfigUpdater(), new FakeTimeProvider(), TimeSpan.Zero));
    }

    public sealed class EnqueueAsync
    {
        [Fact]
        public async Task Should_apply_single_edit_with_one_update_call()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var updater = new FakeConfigUpdater();
            using var writer = new ConfigBatchWriter(updater, time, Window);

            // Act
            var pending = writer.EnqueueAsync(AppendTool("t1"), TestContext.Current.CancellationToken);
            await AdvanceUntilAsync(time, pending);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(1, updater.UpdateCalls),
                () => Assert.Equal("t1", Assert.Single(updater.Current.McpTools).Name));
        }

        [Fact]
        public async Task Should_coalesce_back_to_back_edits_into_one_update_call()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var updater = new FakeConfigUpdater();
            using var writer = new ConfigBatchWriter(updater, time, Window);

            // Act
            var pending = Task.WhenAll(
                writer.EnqueueAsync(AppendTool("t1"), TestContext.Current.CancellationToken),
                writer.EnqueueAsync(AppendTool("t2"), TestContext.Current.CancellationToken),
                writer.EnqueueAsync(AppendTool("t3"), TestContext.Current.CancellationToken));
            await AdvanceUntilAsync(time, pending);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(1, updater.UpdateCalls),
                () => Assert.Equal(["t1", "t2", "t3"], updater.Current.McpTools.Select(tool => tool.Name)));
        }

        [Fact]
        public async Task Should_drop_canceled_edit_and_apply_the_rest()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var updater = new FakeConfigUpdater();
            using var writer = new ConfigBatchWriter(updater, time, Window);
            using var canceled = new CancellationTokenSource();

            // Act
            var dropped = writer.EnqueueAsync(AppendTool("dropped"), canceled.Token);
            await canceled.CancelAsync();
            var kept = writer.EnqueueAsync(AppendTool("kept"), TestContext.Current.CancellationToken);
            await AdvanceUntilAsync(time, kept);

            // Assert
            Assert.Multiple(
                () => Assert.True(dropped.IsCanceled),
                () => Assert.Equal("kept", Assert.Single(updater.Current.McpTools).Name));
        }

        [Fact]
        public async Task Should_fault_when_enqueued_after_dispose()
        {
            // Arrange
            var writer = new ConfigBatchWriter(new FakeConfigUpdater(), new FakeTimeProvider(), Window);
            writer.Dispose();

            // Act + Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => writer.EnqueueAsync(AppendTool("t1"), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_cancel_pending_edit_on_dispose()
        {
            // Arrange
            var writer = new ConfigBatchWriter(new FakeConfigUpdater(), new FakeTimeProvider(), Window);

            // Act
            var pending = writer.EnqueueAsync(AppendTool("t1"), TestContext.Current.CancellationToken);
            writer.Dispose();

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
    }

    private sealed class FakeConfigUpdater : IConfigUpdater
    {
        private ConfigSnapshot _current = ConfigSnapshot.Empty;

        public ConfigSnapshot Current
            => _current;

        public int UpdateCalls { get; private set; }

        public Task UpdateAsync(
            Func<ConfigSnapshot, ConfigSnapshot> edit,
            CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            _current = edit(_current);
            return Task.CompletedTask;
        }
    }
}
