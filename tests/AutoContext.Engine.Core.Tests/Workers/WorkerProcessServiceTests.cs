namespace AutoContext.Engine.Core.Tests.Workers;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support.Workers;
using AutoContext.Engine.Core.Workers;
using AutoContext.Engine.Protocol.Messages.Logs;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class WorkerProcessServiceTests
{
    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_null_provider()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            var probe = new FakeWorkerConnectionProbe(launcher);

            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => new WorkerProcessService(
                    null!, launcher, probe, new LogChannel(), TimeProvider.System, NullLogger<WorkerProcessService>.Instance));
        }

        [Fact]
        public void Should_reject_null_launcher()
        {
            // Arrange
            var probe = new FakeWorkerConnectionProbe(new FakeWorkerProcessLauncher());

            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => new WorkerProcessService(
                    () => [WorkerProcessInfoFakeData.CreateValid()], null!, probe, new LogChannel(), TimeProvider.System, NullLogger<WorkerProcessService>.Instance));
        }

        [Fact]
        public void Should_reject_null_probe()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => new WorkerProcessService(
                    () => [WorkerProcessInfoFakeData.CreateValid()], launcher, null!, new LogChannel(), TimeProvider.System, NullLogger<WorkerProcessService>.Instance));
        }

        [Fact]
        public void Should_reject_null_log_channel()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            var probe = new FakeWorkerConnectionProbe(launcher);

            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => new WorkerProcessService(
                    () => [WorkerProcessInfoFakeData.CreateValid()], launcher, probe, null!, TimeProvider.System, NullLogger<WorkerProcessService>.Instance));
        }

        [Fact]
        public void Should_reject_null_time_provider()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            var probe = new FakeWorkerConnectionProbe(launcher);

            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => new WorkerProcessService(
                    () => [WorkerProcessInfoFakeData.CreateValid()], launcher, probe, new LogChannel(), null!, NullLogger<WorkerProcessService>.Instance));
        }

        [Fact]
        public void Should_reject_null_logger()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            var probe = new FakeWorkerConnectionProbe(launcher);

            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => new WorkerProcessService(
                    () => [WorkerProcessInfoFakeData.CreateValid()], launcher, probe, new LogChannel(), TimeProvider.System, null!));
        }
    }

    public sealed class StartAsync
    {
        [Fact]
        public async Task Should_reject_duplicate_worker_ids()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            var probe = new FakeWorkerConnectionProbe(launcher);
            using var service = new WorkerProcessService(
                () =>
                [
                    WorkerProcessInfoFakeData.CreateValid("dotnet"),
                    WorkerProcessInfoFakeData.CreateValid("dotnet"),
                ],
                launcher,
                probe,
                new LogChannel(),
                TimeProvider.System,
                NullLogger<WorkerProcessService>.Instance);

            // Act + Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.StartAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_reject_a_null_manifest()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            var probe = new FakeWorkerConnectionProbe(launcher);
            using var service = new WorkerProcessService(
                () => null!,
                launcher,
                probe,
                new LogChannel(),
                TimeProvider.System,
                NullLogger<WorkerProcessService>.Instance);

            // Act + Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => service.StartAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_reject_a_null_spec()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            var probe = new FakeWorkerConnectionProbe(launcher);
            using var service = new WorkerProcessService(
                () => [null!],
                launcher,
                probe,
                new LogChannel(),
                TimeProvider.System,
                NullLogger<WorkerProcessService>.Instance);

            // Act + Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => service.StartAsync(TestContext.Current.CancellationToken));
        }
    }

    public sealed class EnsureRunningAsync
    {
        [Fact]
        public void Should_reject_empty_worker_id()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);

            // Act + Assert
            Assert.Throws<ArgumentException>(() =>
            {
                _ = manager.EnsureRunningAsync(string.Empty, TestContext.Current.CancellationToken);
            });
        }

        [Fact]
        public async Task Should_reject_unknown_worker_id()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);

            // Act + Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => manager.EnsureRunningAsync("missing", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_complete_when_worker_pipe_connectable()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);
            var ready = manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken);

            // Act
            launcher.Launches[0].MarkReady();
            await ready;

            // Assert
            Assert.Equal(1, launcher.LaunchCount);
        }

        [Fact]
        public async Task Should_spawn_once_for_concurrent_callers()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);

            // Act
            var first = manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken);
            var second = manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken);
            launcher.Launches[0].MarkReady();
            await Task.WhenAll(first, second);

            // Assert
            Assert.Equal(1, launcher.LaunchCount);
        }

        [Fact]
        public async Task Should_not_respawn_while_running()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);
            var first = manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken);
            launcher.Launches[0].MarkReady();
            await first;

            // Act
            await manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, launcher.LaunchCount);
        }

        [Fact]
        public async Task Should_respawn_after_worker_exits()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);
            var first = manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken);
            launcher.Launches[0].MarkReady();
            await first;
            launcher.Launches[0].EmitExit(0);

            // Act
            var second = manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken);
            launcher.Launches[1].MarkReady();
            await second;

            // Assert
            Assert.Equal(2, launcher.LaunchCount);
        }

        [Fact]
        public async Task Should_fault_when_launch_fails()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher
            {
                FailWith = new ProcessLaunchException<WorkerProcessInfo>(
                    WorkerProcessInfoFakeData.CreateValid("dotnet"), "boom"),
            };
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);

            // Act + Assert
            await Assert.ThrowsAsync<ProcessLaunchException<WorkerProcessInfo>>(
                () => manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_respawn_after_a_failed_launch()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher
            {
                FailWith = new ProcessLaunchException<WorkerProcessInfo>(
                    WorkerProcessInfoFakeData.CreateValid("dotnet"), "boom"),
            };
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);
            await Assert.ThrowsAsync<ProcessLaunchException<WorkerProcessInfo>>(
                () => manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken));
            launcher.FailWith = null;

            // Act
            var retry = manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken);
            launcher.Launches[0].MarkReady();
            await retry;

            // Assert
            Assert.Equal(1, launcher.LaunchCount);
        }

        [Fact]
        public async Task Should_fault_when_worker_exits_before_ready()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);
            var ready = manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken);

            // Act
            launcher.Launches[0].EmitExit(1);

            // Assert
            await Assert.ThrowsAsync<ProcessLaunchException<WorkerProcessInfo>>(() => ready);
        }

        [Fact]
        public void Should_not_complete_on_stderr_lines()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);
            var ready = manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken);

            // Act
            launcher.Launches[0].EmitStandardErrorLine("starting up");

            // Assert
            Assert.False(ready.IsCompleted);
        }

        [Fact]
        public async Task Should_honour_caller_cancellation_without_killing_the_spawn()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);
            using var cts = new CancellationTokenSource();
            var ready = manager.EnsureRunningAsync("dotnet", cts.Token);

            // Act
            await cts.CancelAsync();

            // Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => ready);
            Assert.False(launcher.Launches[0].Process.Killed);
        }
    }

    public sealed class Dispose
    {
        [Fact]
        public async Task Should_fault_pending_waiters()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            var manager = WorkerProcessServiceTestFactory.Create(launcher);
            var ready = manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken);

            // Act
            manager.Dispose();

            // Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(() => ready);
        }

        [Fact]
        public async Task Should_kill_and_dispose_running_processes()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            var manager = WorkerProcessServiceTestFactory.Create(launcher);
            var ready = manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken);
            launcher.Launches[0].MarkReady();
            await ready;

            // Act
            manager.Dispose();

            // Assert
            Assert.Multiple(
                () => Assert.True(launcher.Launches[0].Process.Killed),
                () => Assert.True(launcher.Launches[0].Process.Disposed));
        }

        [Fact]
        public async Task Should_reject_ensure_running_after_dispose()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            var manager = WorkerProcessServiceTestFactory.Create(launcher);
            manager.Dispose();

            // Act + Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken));
        }

        [Fact]
        public void Should_be_idempotent()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            var manager = WorkerProcessServiceTestFactory.Create(launcher);

            // Act
            manager.Dispose();

            // Assert
            manager.Dispose();
        }
    }

    public sealed class HasEverSpawned
    {
        [Fact]
        public void Should_reject_empty_worker_id()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);

            // Act + Assert
            Assert.Throws<ArgumentException>(() => manager.HasEverSpawned(string.Empty));
        }

        [Fact]
        public void Should_report_false_for_an_unknown_worker()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);

            // Act + Assert
            Assert.False(manager.HasEverSpawned("missing"));
        }

        [Fact]
        public void Should_report_false_for_a_registered_but_unstarted_worker()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);

            // Act + Assert
            Assert.False(manager.HasEverSpawned("dotnet"));
        }

        [Fact]
        public async Task Should_report_true_after_the_worker_is_spawned()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher);
            var ready = manager.EnsureRunningAsync("dotnet", TestContext.Current.CancellationToken);

            // Act
            launcher.Launches[0].MarkReady();
            await ready;

            // Assert
            Assert.True(manager.HasEverSpawned("dotnet"));
        }
    }

    public sealed class StandardErrorCapture
    {
        [Fact]
        public async Task Should_route_worker_stderr_into_the_log_channel_under_the_worker_category()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var launcher = new FakeWorkerProcessLauncher();
            var logChannel = new LogChannel();
            using var manager = WorkerProcessServiceTestFactory.Create(launcher, logChannel);
            var ready = manager.EnsureRunningAsync("dotnet", cancellationToken);
            launcher.Launches[0].MarkReady();
            await ready;

            // Act
            launcher.Launches[0].EmitStandardErrorLine("boom from the worker");
            logChannel.Complete();

            // Assert
            var records = new List<JsonLogRecord>();
            await foreach (var record in logChannel.ReadAllAsync(cancellationToken))
            {
                records.Add(record);
            }

            var single = Assert.Single(records);
            Assert.Multiple(
                () => Assert.Equal("worker.dotnet.engine.stderr", single.Category),
                () => Assert.Equal("boom from the worker", single.Message),
                () => Assert.Equal(LogLevels.Information, single.Level));
        }
    }
}
