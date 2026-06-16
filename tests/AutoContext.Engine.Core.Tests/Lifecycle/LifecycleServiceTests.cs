namespace AutoContext.Engine.Core.Tests.Lifecycle;

using System.IO.Pipes;
using System.Text;
using System.Text.Json;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Core.Tests.Support.Logging;
using AutoContext.Engine.Core.Tests.Support.Registry;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Lifecycle;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class LifecycleServiceTests(
    TempDirectoryFixture tempDirectory,
    LifecycleServiceFixture lifecycle)
    : IClassFixture<TempDirectoryFixture>, IClassFixture<LifecycleServiceFixture>
{
    private const string RegistryFileName = "engine-registry.json";

    [Fact]
    public async Task Should_throw_when_StartAsync_is_invoked_twice()
    {
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Service.StartAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(EndpointKind.Rpc)]
    [InlineData(EndpointKind.Events)]
    [InlineData(EndpointKind.Health)]
    [InlineData(EndpointKind.Logs)]
    public async Task Should_bind_endpoint_on_StartAsync(EndpointKind kind)
    {
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            kind, context.EngineOptions, TestContext.Current.CancellationToken);

        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Should_stop_accepting_new_connections_on_StopAsync()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        var workspaceHash = WorkspaceHash.Compute(context.EngineOptions.WorkspacePath);
        var pipeName = new Endpoint(
            EndpointKind.Rpc, workspaceHash.Value, context.EngineOptions.InstanceId).ToString();

        // Act
        await context.Service.StopAsync(TestContext.Current.CancellationToken);

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
    public async Task Should_be_idempotent_when_DisposeAsync_is_invoked_before_start()
    {
        var context = lifecycle.Create();

        // Act + Assert — must not throw.
        await context.Service.DisposeAsync();
        await context.Service.DisposeAsync();
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_options()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                null!,
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_logger_factory()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                null!,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_application_lifetime()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                null!,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_registry_reader()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                null!,
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_event_stream()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                null!,
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_lifecycle_notifier()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                null!,
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_idle_timeout_watchdog()
    {
        using var lifetime = new FakeHostApplicationLifetime();

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                null!,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_instance_guard()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                null!,
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_logs_broadcaster()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                null!,
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_log_file_reader()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                null!,
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_config_source()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                null!,
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_config_updater()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                null!,
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_config_broadcaster()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                null!,
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_workspace_accessor()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                null!,
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_manifest_accessor()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                null!,
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_overrides_accessor()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                null!,
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_body_projector()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                null!,
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_file_reader()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                null!,
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_search_service()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                null!,
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_instructions_broadcaster()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                null!,
                LifecycleServiceFixture.CreateMcpToolsRegistryAccessor()));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_mcp_tools_registry_accessor()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = LifecycleServiceFixture.CreateWatchdog(LifecycleServiceFixture.CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(LifecycleServiceFixture.CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                LifecycleServiceFixture.CreateRegistryReader(),
                LifecycleServiceFixture.CreateEventStream(),
                LifecycleServiceFixture.CreateNotifier(),
                watchdog,
                new FakeUniqueInstanceGuard(),
                LifecycleServiceFixture.CreateLogsBroadcaster(),
                LifecycleServiceFixture.CreateLogFileReader(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateConfigUpdater(),
                LifecycleServiceFixture.CreateConfigBroadcaster(),
                LifecycleServiceFixture.CreateWorkspaceAccessor(),
                LifecycleServiceFixture.CreateInstructionsManifestAccessor(),
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateInstructionsBodyProjector(),
                LifecycleServiceFixture.CreateInstructionsFileReader(),
                LifecycleServiceFixture.CreateInstructionsSearchService(),
                LifecycleServiceFixture.CreateInstructionsBroadcaster(),
                null!));
    }

    [Fact]
    public async Task Should_accept_rpc_handshake_when_protocol_version_matches()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Rpc, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        // Act
        await EngineRpcTestClient.SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        var response = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Null(response.Error),
            () => Assert.NotNull(response.Result),
            () => Assert.Equal(1, response.Id.GetInt32()));

        var result = response.Result!.Value.Deserialize(
            ProtocolJsonContext.Default.JsonHandshakeResult);
        Assert.NotNull(result);
        Assert.Multiple(
            () => Assert.Equal(ProtocolVersion.Current, result!.ProtocolVersion),
            () => Assert.False(string.IsNullOrWhiteSpace(result!.EngineVersion)));
    }

    [Fact]
    public async Task Should_accept_events_handshake_when_protocol_version_matches()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Events, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        // Act
        await EngineRpcTestClient.SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        var response = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Null(response.Error),
            () => Assert.NotNull(response.Result));
    }

    [Fact]
    public async Task Should_refuse_rpc_handshake_when_protocol_version_mismatches()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Rpc, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        // Act
        await EngineRpcTestClient.SendHelloAsync(codec, ProtocolVersion.Current + 1, TestContext.Current.CancellationToken);
        var response = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert — error response carries the mismatch code, then
        // the engine closes the pipe.
        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.ProtocolVersionMismatch, response.Error!.Code);

        var next = await codec.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Null(next); // EOF — connection closed by engine.
    }

    [Fact]
    public async Task Should_refuse_rpc_handshake_when_first_frame_is_not_hello()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Rpc, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        var request = new JsonRpcRequest
        {
            JsonRpc = JsonRpcVersion.Value,
            Id = JsonDocument.Parse("9").RootElement,
            Method = "Engine.SomethingElse",
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            request, ProtocolJsonContext.Default.JsonRpcRequest);

        // Act
        await codec.WriteAsync(bytes, TestContext.Current.CancellationToken);
        var response = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.HelloRequired, response.Error!.Code);

        var next = await codec.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Null(next);
    }

    [Fact]
    public async Task Should_refuse_rpc_handshake_when_hello_params_omit_protocol_version()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Rpc, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        var request = new JsonRpcRequest
        {
            JsonRpc = JsonRpcVersion.Value,
            Id = JsonDocument.Parse("1").RootElement,
            Method = ProtocolMethods.Hello,
            Params = JsonDocument.Parse("{}").RootElement,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            request, ProtocolJsonContext.Default.JsonRpcRequest);

        // Act
        await codec.WriteAsync(bytes, TestContext.Current.CancellationToken);
        var response = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert — missing field is InvalidParams, not a 0-vs-1
        // ProtocolVersionMismatch (which would mis-attribute the
        // failure to a version skew that never happened).
        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.InvalidParams, response.Error!.Code);

        var next = await codec.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Null(next);
    }

    [Fact]
    public async Task Should_accept_health_connection_without_handshake()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        // Act
        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Health, context.EngineOptions, TestContext.Current.CancellationToken);

        // Assert — the engine does not write a Hello reply on health;
        // the read returns EOF as soon as the handler returns and the
        // listener disposes the server-side stream.
        var codec = new LengthPrefixedFrameCodec(client);
        var bytes = await codec.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Null(bytes);
    }

    [Fact]
    public async Task Should_accept_logs_connection_without_handshake()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        // Act
        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Logs, context.EngineOptions, TestContext.Current.CancellationToken);

        // Assert — the logs pipe streams broadcaster frames until the
        // service stops. After StopAsync drains and closes the
        // broadcaster, the server tears the connection down and the
        // client observes EOF.
        Assert.True(client.IsConnected);

        var codec = new LengthPrefixedFrameCodec(client);
        await context.Service.StopAsync(TestContext.Current.CancellationToken);
        var bytes = await codec.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Null(bytes);
    }

    [Fact]
    public async Task Should_stream_published_log_record_to_logs_pipe_subscriber()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Logs, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        var record = LogRecordFakeData.CreateLogRecord(
            category: "engine.test",
            level: LogLevels.Information,
            message: "wire-record",
            timestamp: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        // Act — publish in a background polling loop until the
        // wire delivers a frame. The pump subscribes after the
        // accept loop hands the connection off, which can lag the
        // client-side ConnectAsync return; any record published
        // before Subscribe() runs is dropped, so we keep pumping
        // until ReadAsync resolves.
        var cancellationToken = TestContext.Current.CancellationToken;
        var stopPublishing = new TaskCompletionSource();
        var publisherTask = Task.Run(async () =>
        {
            while (!stopPublishing.Task.IsCompleted)
            {
                context.LogsBroadcaster.TryPublish(record);
                var delay = Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
                await Task.WhenAny(stopPublishing.Task, delay).ConfigureAwait(false);
            }
        }, cancellationToken);

        var bytes = await codec.ReadAsync(cancellationToken);
        stopPublishing.TrySetResult();
        await publisherTask;

        // Stop afterwards so the connection tears down cleanly.
        await context.Service.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(bytes);
        var frame = JsonSerializer.Deserialize(
            bytes!, ProtocolJsonContext.Default.JsonLogStreamFrame);
        var recordFrame = Assert.IsType<JsonLogRecordFrame>(frame);
        Assert.Multiple(
            () => Assert.Equal("wire-record", recordFrame.Record.Message),
            () => Assert.Equal("engine.test", recordFrame.Record.Category),
            () => Assert.Equal(LogLevels.Information, recordFrame.Record.Level));
    }

    [Fact]
    public async Task Should_drop_slow_logs_pipe_subscriber_with_terminal_frame()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Logs, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        var record = LogRecordFakeData.CreateLogRecord(
            category: "engine.test",
            level: LogLevels.Information,
            message: "flood",
            timestamp: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        // Act — flood the broadcaster while the wire is not being
        // read. The OS pipe buffer fills, the server-side pump
        // blocks, the 64-slot subscription buffer fills, and the
        // next publish drops the subscriber with a terminal
        // LogDroppedFrame. The flood count must dwarf any
        // reasonable OS pipe buffer (default NamedPipe out-buffer
        // is typically a few KB-64 KB).
        const int FloodCount = 65_536;
        for (var i = 0; i < FloodCount; i++)
        {
            context.LogsBroadcaster.TryPublish(record);
        }

        // Drain the wire until EOF, collecting every frame.
        var frames = new List<JsonLogStreamFrame>();
        while (true)
        {
            var bytes = await codec.ReadAsync(TestContext.Current.CancellationToken);
            if (bytes is null)
            {
                break;
            }

            var frame = JsonSerializer.Deserialize(
                bytes, ProtocolJsonContext.Default.JsonLogStreamFrame);
            Assert.NotNull(frame);
            frames.Add(frame!);
        }

        // Stop the service so the test fixture cleanup is clean.
        await context.Service.StopAsync(TestContext.Current.CancellationToken);

        // Assert — the very last frame on the wire is the
        // terminal drop frame with the slow-subscriber reason.
        var terminal = Assert.IsType<JsonLogDroppedFrame>(frames[^1]);
        Assert.Equal(JsonLogDroppedFrame.SlowSubscriberReason, terminal.Reason);
    }

    [Fact]
    public async Task Should_serve_Engine_RegistryEntries_after_handshake()
    {
        // Arrange — seed a registry file with two entries so the
        // handler has something interesting to return.
        var registryPath = tempDirectory.CreatePath(RegistryFileName);
        var seeded = new[]
        {
            RegistryEntryFakeData.CreateValidEntry(),
            RegistryEntryFakeData.CreateValidEntry(),
        };
        RegistryFileTestWriter.Write(registryPath, seeded);

        var context = lifecycle.Create(
            registryReader: RegistryFileReaderTestFactory.Create(registryPath));
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Rpc, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await EngineRpcTestClient.SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act
        await EngineRpcTestClient.SendRequestAsync(
            codec, id: 7, method: RegistryMethods.RegistryEntries,
            TestContext.Current.CancellationToken);
        var response = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Null(response.Error),
            () => Assert.NotNull(response.Result),
            () => Assert.Equal(7, response.Id.GetInt32()));

        var result = response.Result!.Value.Deserialize(
            ProtocolJsonContext.Default.JsonRegistryEntriesResult);
        Assert.NotNull(result);
        Assert.Equal(seeded.Length, result!.Entries.Count);
    }

    [Fact]
    public async Task Should_reply_method_not_found_for_unknown_rpc_method()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Rpc, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await EngineRpcTestClient.SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act
        await EngineRpcTestClient.SendRequestAsync(
            codec, id: 42, method: "Engine.DoesNotExist",
            TestContext.Current.CancellationToken);
        var response = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert — dispatcher keeps the connection open after an
        // unknown-method reply so the caller can issue further
        // requests on the same pipe.
        Assert.Multiple(
            () => Assert.NotNull(response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.MethodNotFound, response.Error!.Code),
            () => Assert.Equal(42, response.Id.GetInt32()));
    }

    [Fact]
    public async Task Should_serve_multiple_sequential_requests_on_one_rpc_connection()
    {
        // Arrange
        var registryPath = tempDirectory.CreatePath(RegistryFileName);
        RegistryFileTestWriter.Write(registryPath);

        var context = lifecycle.Create(
            registryReader: RegistryFileReaderTestFactory.Create(registryPath));
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Rpc, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await EngineRpcTestClient.SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act — three back-to-back Engine.RegistryEntries calls on
        // the same connection; each response must carry the id of
        // the matching request, proving the dispatcher keeps the
        // pipe open and ordered.
        await EngineRpcTestClient.SendRequestAsync(codec, id: 100, method: RegistryMethods.RegistryEntries, TestContext.Current.CancellationToken);
        var first = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        await EngineRpcTestClient.SendRequestAsync(codec, id: 101, method: RegistryMethods.RegistryEntries, TestContext.Current.CancellationToken);
        var second = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        await EngineRpcTestClient.SendRequestAsync(codec, id: 102, method: RegistryMethods.RegistryEntries, TestContext.Current.CancellationToken);
        var third = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Null(first.Error),
            () => Assert.Null(second.Error),
            () => Assert.Null(third.Error),
            () => Assert.Equal(100, first.Id.GetInt32()),
            () => Assert.Equal(101, second.Id.GetInt32()),
            () => Assert.Equal(102, third.Id.GetInt32()));
    }

    [Fact]
    public async Task Should_accept_Engine_Shutdown_and_stop_the_application()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Rpc, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await EngineRpcTestClient.SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act
        await EngineRpcTestClient.SendRequestAsync(
            codec, id: 9, method: ProtocolMethods.Shutdown,
            TestContext.Current.CancellationToken);
        var response = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert — { accepted: true } returned, lifetime told to
        // stop, and the connection then closes from the engine end.
        Assert.Multiple(
            () => Assert.Null(response.Error),
            () => Assert.NotNull(response.Result),
            () => Assert.Equal(9, response.Id.GetInt32()));

        var result = response.Result!.Value.Deserialize(
            ProtocolJsonContext.Default.JsonShutdownResult);
        Assert.NotNull(result);
        Assert.True(result!.Accepted);

        // The dispatcher requests StopApplication after flushing the
        // response; await the signal directly instead of polling.
        await context.Lifetime.StopApplicationRequested.WaitAsync(
            TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var next = await codec.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Null(next); // EOF — dispatcher returned and the stream closed.
    }

    [Fact]
    public async Task Should_push_started_notification_on_events_pipe_after_handshake()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Events, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await EngineRpcTestClient.SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act
        var frame = await codec.ReadAsync(TestContext.Current.CancellationToken);

        // Assert
        var evt = LifecycleNotificationTestDecoder.Decode(frame);

        Assert.Multiple(
            () => Assert.Equal(LifecycleEventKinds.Started, evt.Kind),
            () => Assert.Equal(context.EngineOptions.InstanceId, evt.InstanceId),
            () => Assert.Equal(0L, evt.Revision));
    }

    [Fact]
    public async Task Should_push_shutting_down_notification_on_events_pipe_on_graceful_stop()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Events, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await EngineRpcTestClient.SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);
        _ = await codec.ReadAsync(TestContext.Current.CancellationToken);

        // Act
        var stopTask = context.Service.StopAsync(TestContext.Current.CancellationToken);
        var shuttingDownFrame = await codec.ReadAsync(TestContext.Current.CancellationToken);
        var eof = await codec.ReadAsync(TestContext.Current.CancellationToken);
        await stopTask;

        // Assert
        var evt = LifecycleNotificationTestDecoder.Decode(shuttingDownFrame);

        Assert.Multiple(
            () => Assert.Null(eof),
            () => Assert.Equal(LifecycleEventKinds.ShuttingDown, evt.Kind),
            () => Assert.Equal(context.EngineOptions.InstanceId, evt.InstanceId));
    }

    [Fact]
    public async Task Should_complete_StopAsync_within_drain_timeout_when_events_peer_never_reads()
    {
        // Arrange
        var options = LifecycleServiceFixture.CreateOptions();
        options.ShutdownDrainTimeout = TimeSpan.FromMilliseconds(250);
        var context = lifecycle.Create(options);
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Events, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await EngineRpcTestClient.SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act + Assert — the peer never reads the pushed started
        // frame, so the events-pipe writer is blocked. StopAsync
        // must still return within the drain timeout (plus a
        // reasonable teardown slack) instead of deadlocking on the
        // stuck pump.
        await context.Service.StopAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_recover_with_ParseError_on_malformed_rpc_frame_post_handshake_and_keep_serving()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Rpc, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await EngineRpcTestClient.SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act — send garbage that is not JSON, then a valid request.
        await codec.WriteAsync(
            Encoding.UTF8.GetBytes("not-json-here"), TestContext.Current.CancellationToken);
        var errorResponse = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        await EngineRpcTestClient.SendRequestAsync(
            codec, id: 21, method: "Engine.DoesNotExist", TestContext.Current.CancellationToken);
        var followUp = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert — first reply is a ParseError on the recovered
        // connection; second reply lands successfully on the same
        // pipe.
        Assert.Multiple(
            () => Assert.NotNull(errorResponse.Error),
            () => Assert.Equal(JsonRpcErrorCodes.ParseError, errorResponse.Error!.Code),
            () => Assert.Equal(JsonValueKind.Null, errorResponse.Id.ValueKind),
            () => Assert.NotNull(followUp.Error),
            () => Assert.Equal(JsonRpcErrorCodes.MethodNotFound, followUp.Error!.Code),
            () => Assert.Equal(21, followUp.Id.GetInt32()));
    }

    [Fact]
    public async Task Should_recover_with_InvalidRequest_on_wrong_jsonrpc_version_post_handshake_and_keep_serving()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Rpc, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await EngineRpcTestClient.SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act
        var bogus = Encoding.UTF8.GetBytes("""{"jsonrpc":"1.0","id":31,"method":"Engine.X"}""");
        await codec.WriteAsync(bogus, TestContext.Current.CancellationToken);
        var errorResponse = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        await EngineRpcTestClient.SendRequestAsync(
            codec, id: 32, method: "Engine.Other", TestContext.Current.CancellationToken);
        var followUp = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(errorResponse.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidRequest, errorResponse.Error!.Code),
            () => Assert.Equal(31, errorResponse.Id.GetInt32()),
            () => Assert.NotNull(followUp.Error),
            () => Assert.Equal(JsonRpcErrorCodes.MethodNotFound, followUp.Error!.Code),
            () => Assert.Equal(32, followUp.Id.GetInt32()));
    }

    [Fact]
    public async Task Should_terminate_rpc_connection_on_malformed_first_frame_with_ParseError_reply()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Rpc, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        // Act
        await codec.WriteAsync(
            Encoding.UTF8.GetBytes("definitely-not-json"), TestContext.Current.CancellationToken);
        var errorResponse = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);
        var afterError = await codec.ReadAsync(TestContext.Current.CancellationToken);

        // Assert — handshake policy is Terminate: server writes the
        // ParseError reply and then drops the connection.
        Assert.Multiple(
            () => Assert.NotNull(errorResponse.Error),
            () => Assert.Equal(JsonRpcErrorCodes.ParseError, errorResponse.Error!.Code),
            () => Assert.Equal(JsonValueKind.Null, errorResponse.Id.ValueKind),
            () => Assert.Null(afterError));
    }

    [Fact]
    public async Task Should_terminate_rpc_connection_on_invalid_first_frame_with_InvalidRequest_reply()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Rpc, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        // Act — well-formed JSON, but wrong jsonrpc version.
        var bogus = Encoding.UTF8.GetBytes("""{"jsonrpc":"1.0","id":41,"method":"Engine.Hello"}""");
        await codec.WriteAsync(bogus, TestContext.Current.CancellationToken);
        var errorResponse = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);
        var afterError = await codec.ReadAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(errorResponse.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidRequest, errorResponse.Error!.Code),
            () => Assert.Equal(41, errorResponse.Id.GetInt32()),
            () => Assert.Null(afterError));
    }

    [Fact]
    public async Task Should_reply_with_Null_id_when_post_handshake_request_omits_id()
    {
        // Arrange
        var context = lifecycle.Create();
        await context.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await EngineRpcTestClient.ConnectAsync(
            EndpointKind.Rpc, context.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await EngineRpcTestClient.SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act — valid JSON-RPC 2.0 frame with the id field absent.
        var noIdRequest = Encoding.UTF8.GetBytes(
            """{"jsonrpc":"2.0","method":"Engine.DoesNotExist"}""");
        await codec.WriteAsync(noIdRequest, TestContext.Current.CancellationToken);
        var response = await EngineRpcTestClient.ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert — the dispatcher normalises the response id to
        // JSON null per JsonRpcId.Normalize(request.Id).
        Assert.Multiple(
            () => Assert.NotNull(response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.MethodNotFound, response.Error!.Code),
            () => Assert.Equal(JsonValueKind.Null, response.Id.ValueKind));
    }
}
