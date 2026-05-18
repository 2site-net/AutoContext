namespace AutoContext.Engine.Core.Tests.Lifecycle;

using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Testing.Fakes;
using AutoContext.Engine.Core.Tests.Testing.Fixtures;
using AutoContext.Engine.Core.Tests.Testing.Utils;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Lifecycle;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using static AutoContext.Engine.Core.Tests.Testing.Utils.EngineRpcTestClient;

public sealed class LifecycleServiceTests(TempDirectoryFixture tempDirectory)
    : IClassFixture<TempDirectoryFixture>
{
    private const string RegistryFileName = "engine-registry.json";

    [Fact]
    public async Task Should_throw_when_StartAsync_is_invoked_twice()
    {
        // Arrange
        await using var sut = CreateService(out _);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.StartAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(EndpointKind.Rpc)]
    [InlineData(EndpointKind.Events)]
    [InlineData(EndpointKind.Health)]
    [InlineData(EndpointKind.Logs)]
    public async Task Should_bind_endpoint_on_StartAsync(EndpointKind kind)
    {
        // Arrange
        await using var sut = CreateService(out var options);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        // Act
        await using var client = await ConnectAsync(
            kind, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Should_stop_accepting_new_connections_on_StopAsync()
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
    public async Task Should_be_idempotent_when_DisposeAsync_is_invoked_before_start()
    {
        // Arrange
        var sut = CreateService(out _);

        // Act + Assert — must not throw.
        await sut.DisposeAsync();
        await sut.DisposeAsync();
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_options()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                null!,
                NullLoggerFactory.Instance,
                new FakeHostApplicationLifetime(),
                CreateRegistryReader(),
                CreateEventStream(),
                CreateNotifier()));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger_factory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(CreateOptions()),
                null!,
                new FakeHostApplicationLifetime(),
                CreateRegistryReader(),
                CreateEventStream(),
                CreateNotifier()));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_application_lifetime()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(CreateOptions()),
                NullLoggerFactory.Instance,
                null!,
                CreateRegistryReader(),
                CreateEventStream(),
                CreateNotifier()));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_registry_reader()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                null!,
                CreateEventStream(),
                CreateNotifier()));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_event_stream()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                CreateRegistryReader(),
                null!,
                CreateNotifier()));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_lifecycle_notifier()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                CreateRegistryReader(),
                CreateEventStream(),
                null!));
    }

    private static LifecycleService CreateService(out EngineOptions options)
    {
        options = CreateOptions();
#pragma warning disable CA2000 // The lifetime is owned by the service for the duration of the test.
        return CreateService(options, new FakeHostApplicationLifetime(), CreateRegistryReader());
#pragma warning restore CA2000
    }

    private static LifecycleService CreateService(
        EngineOptions options,
        IHostApplicationLifetime applicationLifetime,
        RegistryFileReader registryReader)
    {
        var stream = CreateEventStream(options);
        var notifier = CreateNotifier(options, stream);
        return new(
            Options.Create(options),
            NullLoggerFactory.Instance,
            applicationLifetime,
            registryReader,
            stream,
            notifier);
    }

    private static LifecycleEventStream CreateEventStream(EngineOptions? options = null)
        => new(
            Options.Create(options ?? CreateOptions()),
            NullLogger<LifecycleEventStream>.Instance);

    private static LifecycleNotifier CreateNotifier(
        EngineOptions? options = null,
        LifecycleEventStream? stream = null)
    {
        var resolvedOptions = options ?? CreateOptions();
        return new(
            stream ?? CreateEventStream(resolvedOptions),
            Options.Create(resolvedOptions));
    }

    private static RegistryFileReader CreateRegistryReader()
    {
        // A non-existent path is a valid input — the reader treats
        // "file missing" as an empty registry, so tests that do not
        // exercise Engine.RegistryEntries can use this default.
        var path = Path.Combine(
            Path.GetTempPath(),
            $"autocontext-engine-registry-{Guid.NewGuid():N}.json");
        return new RegistryFileReader(path);
    }

    private static EngineOptions CreateOptions() =>
        new()
        {
            WorkspacePath = EngineOptionsFakeData.GetWorkspacePath(),
            InstanceId = Guid.NewGuid(),
        };

    [Fact]
    public async Task Should_accept_rpc_handshake_when_protocol_version_matches()
    {
        // Arrange
        await using var sut = CreateService(out var options);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, options, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        // Act
        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        var response = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Null(response.Error),
            () => Assert.NotNull(response.Result),
            () => Assert.Equal(1, response.Id.GetInt32()));

        var result = response.Result!.Value.Deserialize(
            ProtocolJsonContext.Default.HandshakeResult);
        Assert.NotNull(result);
        Assert.Multiple(
            () => Assert.Equal(ProtocolVersion.Current, result!.ProtocolVersion),
            () => Assert.False(string.IsNullOrWhiteSpace(result!.EngineVersion)));
    }

    [Fact]
    public async Task Should_accept_events_handshake_when_protocol_version_matches()
    {
        // Arrange
        await using var sut = CreateService(out var options);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Events, options, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        // Act
        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        var response = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Null(response.Error),
            () => Assert.NotNull(response.Result));
    }

    [Fact]
    public async Task Should_refuse_rpc_handshake_when_protocol_version_mismatches()
    {
        // Arrange
        await using var sut = CreateService(out var options);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, options, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        // Act
        await SendHelloAsync(codec, ProtocolVersion.Current + 1, TestContext.Current.CancellationToken);
        var response = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

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
        await using var sut = CreateService(out var options);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, options, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        var request = new JsonRpcRequest
        {
            Jsonrpc = JsonRpcVersion.Value,
            Id = JsonDocument.Parse("9").RootElement,
            Method = "Engine.SomethingElse",
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            request, ProtocolJsonContext.Default.JsonRpcRequest);

        // Act
        await codec.WriteAsync(bytes, TestContext.Current.CancellationToken);
        var response = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

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
        await using var sut = CreateService(out var options);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, options, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        var request = new JsonRpcRequest
        {
            Jsonrpc = JsonRpcVersion.Value,
            Id = JsonDocument.Parse("1").RootElement,
            Method = ProtocolMethods.Hello,
            Params = JsonDocument.Parse("{}").RootElement,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            request, ProtocolJsonContext.Default.JsonRpcRequest);

        // Act
        await codec.WriteAsync(bytes, TestContext.Current.CancellationToken);
        var response = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

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
        await using var sut = CreateService(out var options);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        // Act
        await using var client = await ConnectAsync(
            EndpointKind.Health, options, TestContext.Current.CancellationToken);

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
        await using var sut = CreateService(out var options);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        // Act
        await using var client = await ConnectAsync(
            EndpointKind.Logs, options, TestContext.Current.CancellationToken);

        // Assert — same passive shape as health.
        var codec = new LengthPrefixedFrameCodec(client);
        var bytes = await codec.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Null(bytes);
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
        new RegistryFileWriter(registryPath).Write(seeded);

        var options = CreateOptions();
        var reader = new RegistryFileReader(registryPath);
        using var lifetime = new FakeHostApplicationLifetime();
        await using var sut = CreateService(options, lifetime, reader);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, options, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act
        await SendRequestAsync(
            codec, id: 7, method: RegistryMethods.RegistryEntries,
            TestContext.Current.CancellationToken);
        var response = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Null(response.Error),
            () => Assert.NotNull(response.Result),
            () => Assert.Equal(7, response.Id.GetInt32()));

        var result = response.Result!.Value.Deserialize(
            ProtocolJsonContext.Default.RegistryEntriesResult);
        Assert.NotNull(result);
        Assert.Equal(seeded.Length, result!.Entries.Count);
    }

    [Fact]
    public async Task Should_reply_method_not_found_for_unknown_rpc_method()
    {
        // Arrange
        await using var sut = CreateService(out var options);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, options, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act
        await SendRequestAsync(
            codec, id: 42, method: "Engine.DoesNotExist",
            TestContext.Current.CancellationToken);
        var response = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

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
        new RegistryFileWriter(registryPath).Write([]);

        var options = CreateOptions();
        var reader = new RegistryFileReader(registryPath);
        using var lifetime = new FakeHostApplicationLifetime();
        await using var sut = CreateService(options, lifetime, reader);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, options, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act — three back-to-back Engine.RegistryEntries calls on
        // the same connection; each response must carry the id of
        // the matching request, proving the dispatcher keeps the
        // pipe open and ordered.
        await SendRequestAsync(codec, id: 100, method: RegistryMethods.RegistryEntries, TestContext.Current.CancellationToken);
        var first = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        await SendRequestAsync(codec, id: 101, method: RegistryMethods.RegistryEntries, TestContext.Current.CancellationToken);
        var second = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        await SendRequestAsync(codec, id: 102, method: RegistryMethods.RegistryEntries, TestContext.Current.CancellationToken);
        var third = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

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
        using var lifetime = new FakeHostApplicationLifetime();
        var options = CreateOptions();
        var reader = CreateRegistryReader();
        await using var sut = CreateService(options, lifetime, reader);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, options, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act
        await SendRequestAsync(
            codec, id: 9, method: ProtocolMethods.Shutdown,
            TestContext.Current.CancellationToken);
        var response = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert — { accepted: true } returned, lifetime told to
        // stop, and the connection then closes from the engine end.
        Assert.Multiple(
            () => Assert.Null(response.Error),
            () => Assert.NotNull(response.Result),
            () => Assert.Equal(9, response.Id.GetInt32()));

        var result = response.Result!.Value.Deserialize(
            ProtocolJsonContext.Default.ShutdownResult);
        Assert.NotNull(result);
        Assert.True(result!.Accepted);

        // The dispatcher requests StopApplication after flushing the
        // response; await the signal directly instead of polling.
        await lifetime.StopApplicationRequested.WaitAsync(
            TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var next = await codec.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Null(next); // EOF — dispatcher returned and the stream closed.
    }

    [Fact]
    public async Task Should_push_started_notification_on_events_pipe_after_handshake()
    {
        // Arrange
        await using var sut = CreateService(out var options);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Events, options, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act — read the first server-pushed frame, which the
        // stream seeds with the current (instanceId, revision).
        var frame = await codec.ReadAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(frame);

        var notification = JsonSerializer.Deserialize(
            frame!, ProtocolJsonContext.Default.JsonRpcNotification);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal(LifecycleMethods.Notification, notification!.Method);
        Assert.NotNull(notification.Params);

        var evt = notification.Params!.Value.Deserialize(
            ProtocolJsonContext.Default.LifecycleEvent);
        Assert.NotNull(evt);
        Assert.Multiple(
            () => Assert.Equal(LifecycleEventKinds.Started, evt!.Kind),
            () => Assert.Equal(options.InstanceId, evt!.InstanceId),
            () => Assert.Equal(0L, evt!.Revision));
    }

    [Fact]
    public async Task Should_push_shutting_down_notification_on_events_pipe_on_graceful_stop()
    {
        // Arrange
        var options = CreateOptions();
        using var lifetime = new FakeHostApplicationLifetime();
        var reader = CreateRegistryReader();
        await using var sut = CreateService(options, lifetime, reader);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Events, options, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Drain the seeded started event.
        var startedFrame = await codec.ReadAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(startedFrame);

        // Act — graceful stop triggers NotifyShutdown, which the
        // events pump must flush before the listener tears the
        // connection down. The pump's write blocks until the peer
        // drains it, so the test must read concurrently with
        // StopAsync rather than awaiting Stop first (that would
        // deadlock: Stop waits for the pump, the pump waits for a
        // reader, and the only reader is this test).
        var stopTask = sut.StopAsync(TestContext.Current.CancellationToken);

        // Assert — the next frame is the shutting-down notification.
        var shuttingDownFrame = await codec.ReadAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(shuttingDownFrame);

        var notification = JsonSerializer.Deserialize(
            shuttingDownFrame!, ProtocolJsonContext.Default.JsonRpcNotification);
        Assert.NotNull(notification);

        var evt = notification!.Params!.Value.Deserialize(
            ProtocolJsonContext.Default.LifecycleEvent);
        Assert.NotNull(evt);
        Assert.Multiple(
            () => Assert.Equal(LifecycleEventKinds.ShuttingDown, evt!.Kind),
            () => Assert.Equal(options.InstanceId, evt!.InstanceId));

        // The stream closes after the notifier completes the
        // subscriber's channel; the next read returns EOF.
        var eof = await codec.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Null(eof);

        await stopTask;
    }
}
