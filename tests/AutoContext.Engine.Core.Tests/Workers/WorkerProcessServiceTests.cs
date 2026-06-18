namespace AutoContext.Engine.Core.Tests.Workers;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;
using AutoContext.Engine.Core.Tests.Support.Workers;
using AutoContext.Engine.Core.Workers;

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
                () => new WorkerProcessService(null!, launcher, probe, NullLogger<WorkerProcessService>.Instance));
        }

        [Fact]
        public void Should_reject_null_launcher()
        {
            // Arrange
            var probe = new FakeWorkerConnectionProbe(new FakeWorkerProcessLauncher());

            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => new WorkerProcessService(
                    () => [WorkerProcessInfoFakeData.CreateValid()], null!, probe, NullLogger<WorkerProcessService>.Instance));
        }

        [Fact]
        public void Should_reject_null_probe()
        {
            // Arrange
            var launcher = new FakeWorkerProcessLauncher();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(
                () => new WorkerProcessService(
                    () => [WorkerProcessInfoFakeData.CreateValid()], launcher, null!, NullLogger<WorkerProcessService>.Instance));
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
                    () => [WorkerProcessInfoFakeData.CreateValid()], launcher, probe, null!));
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
}
