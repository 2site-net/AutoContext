namespace AutoContext.Engine.Core.Tests.Lifecycle;

using System.IO.Pipes;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Tests.Testing.Utils;
using AutoContext.Framework.Protocol;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class LifecycleServiceTests
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task StartAsync_should_throw_when_invoked_twice()
    {
        // Arrange
        await using var sut = CreateService(out _);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_should_bind_all_four_endpoints()
    {
        // Arrange
        await using var sut = CreateService(out var options);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        var workspaceHash = WorkspaceHash.Compute(options.WorkspacePath);

        // Act + Assert — connect to every kind in turn.
        foreach (var kind in new[] { EndpointKind.Rpc, EndpointKind.Events, EndpointKind.Health, EndpointKind.Logs })
        {
            var pipeName = new Endpoint(kind, workspaceHash.Value, options.InstanceId).ToString();
            await using var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await client.ConnectAsync(ConnectTimeout, TestContext.Current.CancellationToken);
            Assert.True(client.IsConnected);
        }
    }

    [Fact]
    public async Task StopAsync_should_stop_accepting_new_connections()
    {
        // Arrange
        await using var sut = CreateService(out var options);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        var workspaceHash = WorkspaceHash.Compute(options.WorkspacePath);
        var pipeName = new Endpoint(
            EndpointKind.Rpc, workspaceHash.Value, options.InstanceId).ToString();

        // Act
        await sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert — a fresh client connect must fail (no server listening).
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await client.ConnectAsync(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisposeAsync_should_be_idempotent_when_never_started()
    {
        // Arrange
        var sut = CreateService(out _);

        // Act + Assert — must not throw.
        await sut.DisposeAsync();
        await sut.DisposeAsync();
    }

    [Fact]
    public void Constructor_should_reject_null_options()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(null!, NullLoggerFactory.Instance));
    }

    [Fact]
    public void Constructor_should_reject_null_logger_factory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(Options.Create(CreateOptions()), null!));
    }

    private static LifecycleService CreateService(out EngineOptions options)
    {
        options = CreateOptions();
        return CreateService(options);
    }

    private static LifecycleService CreateService(EngineOptions options)
        => new(Options.Create(options), NullLoggerFactory.Instance);

    private static EngineOptions CreateOptions() =>
        new()
        {
            WorkspacePath = EngineOptionsFakeData.GetWorkspacePath(),
            InstanceId = Guid.NewGuid(),
        };
}
