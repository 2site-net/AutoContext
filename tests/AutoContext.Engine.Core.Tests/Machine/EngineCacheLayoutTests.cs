namespace AutoContext.Engine.Core.Tests.Machine;

using AutoContext.Engine.Core.Machine;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Core.Tests.Support.Machine;

public sealed class EngineCacheLayoutTests
{
    [Fact]
    public void Should_compose_worker_log_path_under_the_logs_directory()
    {
        var layout = EngineCacheLayoutTestFactory.Create(EngineCrashWriterFixture.CreateOptions());

        var path = layout.WorkerLogFilePath("dotnet");

        Assert.Equal(Path.Combine(layout.LogsDirPath, "worker-dotnet.log"), path);
    }

    [Fact]
    public void Should_place_worker_log_beside_the_engine_log()
    {
        var layout = EngineCacheLayoutTestFactory.Create(EngineCrashWriterFixture.CreateOptions());

        var workerDirectory = Path.GetDirectoryName(layout.WorkerLogFilePath("web"));
        var engineDirectory = Path.GetDirectoryName(layout.EngineLogFilePath);

        Assert.Equal(engineDirectory, workerDirectory);
    }

    [Fact]
    public void Should_derive_worker_rotation_basename_from_the_worker_id()
    {
        Assert.Equal("worker-dotnet", EngineCacheLayout.WorkerLogBaseName("dotnet"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Should_reject_a_missing_worker_id(string? workerId)
    {
        var layout = EngineCacheLayoutTestFactory.Create(EngineCrashWriterFixture.CreateOptions());

        Assert.ThrowsAny<ArgumentException>(() => layout.WorkerLogFilePath(workerId!));
        Assert.ThrowsAny<ArgumentException>(() => EngineCacheLayout.WorkerLogBaseName(workerId!));
    }
}
