namespace AutoContext.Engine.Core.Tests.Lifecycle;

using System.IO.Pipes;
using System.Text;
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

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using static AutoContext.Engine.Core.Tests.Testing.Utils.EngineRpcTestClient;
using static AutoContext.Engine.Core.Tests.Testing.Utils.LifecycleServiceHarness;

public sealed class LifecycleServiceTests(TempDirectoryFixture tempDirectory)
    : IClassFixture<TempDirectoryFixture>
{
    private const string RegistryFileName = "engine-registry.json";

    [Fact]
    public async Task Should_throw_when_StartAsync_is_invoked_twice()
    {
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Service.StartAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(EndpointKind.Rpc)]
    [InlineData(EndpointKind.Events)]
    [InlineData(EndpointKind.Health)]
    [InlineData(EndpointKind.Logs)]
    public async Task Should_bind_endpoint_on_StartAsync(EndpointKind kind)
    {
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            kind, harness.EngineOptions, TestContext.Current.CancellationToken);

        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Should_stop_accepting_new_connections_on_StopAsync()
    {
        // Arrange
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        var workspaceHash = WorkspaceHash.Compute(harness.EngineOptions.WorkspacePath);
        var pipeName = new Endpoint(
            EndpointKind.Rpc, workspaceHash.Value, harness.EngineOptions.InstanceId).ToString();

        // Act
        await harness.Service.StopAsync(TestContext.Current.CancellationToken);

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
        var harness = Create();

        // Act + Assert — must not throw.
        await harness.DisposeAsync();
        await harness.DisposeAsync();
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_options()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = CreateWatchdog(CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                null!,
                NullLoggerFactory.Instance,
                lifetime,
                CreateRegistryReader(),
                CreateEventStream(),
                CreateNotifier(),
                watchdog));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_logger_factory()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = CreateWatchdog(CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(CreateOptions()),
                null!,
                lifetime,
                CreateRegistryReader(),
                CreateEventStream(),
                CreateNotifier(),
                watchdog));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_application_lifetime()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = CreateWatchdog(CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(CreateOptions()),
                NullLoggerFactory.Instance,
                null!,
                CreateRegistryReader(),
                CreateEventStream(),
                CreateNotifier(),
                watchdog));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_registry_reader()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = CreateWatchdog(CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                null!,
                CreateEventStream(),
                CreateNotifier(),
                watchdog));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_event_stream()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = CreateWatchdog(CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                CreateRegistryReader(),
                null!,
                CreateNotifier(),
                watchdog));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_null_lifecycle_notifier()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        await using var watchdog = CreateWatchdog(CreateOptions(), lifetime);

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                CreateRegistryReader(),
                CreateEventStream(),
                null!,
                watchdog));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_idle_timeout_watchdog()
    {
        using var lifetime = new FakeHostApplicationLifetime();

        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleService(
                Options.Create(CreateOptions()),
                NullLoggerFactory.Instance,
                lifetime,
                CreateRegistryReader(),
                CreateEventStream(),
                CreateNotifier(),
                null!));
    }

    [Fact]
    public async Task Should_accept_rpc_handshake_when_protocol_version_matches()
    {
        // Arrange
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, harness.EngineOptions, TestContext.Current.CancellationToken);
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
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Events, harness.EngineOptions, TestContext.Current.CancellationToken);
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
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, harness.EngineOptions, TestContext.Current.CancellationToken);
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
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, harness.EngineOptions, TestContext.Current.CancellationToken);
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
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, harness.EngineOptions, TestContext.Current.CancellationToken);
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
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        // Act
        await using var client = await ConnectAsync(
            EndpointKind.Health, harness.EngineOptions, TestContext.Current.CancellationToken);

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
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        // Act
        await using var client = await ConnectAsync(
            EndpointKind.Logs, harness.EngineOptions, TestContext.Current.CancellationToken);

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

        await using var harness = Create(
            registryReader: new RegistryFileReader(registryPath));
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, harness.EngineOptions, TestContext.Current.CancellationToken);
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
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, harness.EngineOptions, TestContext.Current.CancellationToken);
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

        await using var harness = Create(
            registryReader: new RegistryFileReader(registryPath));
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, harness.EngineOptions, TestContext.Current.CancellationToken);
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
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, harness.EngineOptions, TestContext.Current.CancellationToken);
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
        await harness.Lifetime.StopApplicationRequested.WaitAsync(
            TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var next = await codec.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Null(next); // EOF — dispatcher returned and the stream closed.
    }

    [Fact]
    public async Task Should_push_started_notification_on_events_pipe_after_handshake()
    {
        // Arrange
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Events, harness.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act
        var frame = await codec.ReadAsync(TestContext.Current.CancellationToken);

        // Assert
        var evt = LifecycleNotificationFrame.Decode(frame);

        Assert.Multiple(
            () => Assert.Equal(LifecycleEventKinds.Started, evt.Kind),
            () => Assert.Equal(harness.EngineOptions.InstanceId, evt.InstanceId),
            () => Assert.Equal(0L, evt.Revision));
    }

    [Fact]
    public async Task Should_push_shutting_down_notification_on_events_pipe_on_graceful_stop()
    {
        // Arrange
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Events, harness.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);
        _ = await codec.ReadAsync(TestContext.Current.CancellationToken);

        // Act
        var stopTask = harness.Service.StopAsync(TestContext.Current.CancellationToken);
        var shuttingDownFrame = await codec.ReadAsync(TestContext.Current.CancellationToken);
        var eof = await codec.ReadAsync(TestContext.Current.CancellationToken);
        await stopTask;

        // Assert
        var evt = LifecycleNotificationFrame.Decode(shuttingDownFrame);

        Assert.Multiple(
            () => Assert.Null(eof),
            () => Assert.Equal(LifecycleEventKinds.ShuttingDown, evt.Kind),
            () => Assert.Equal(harness.EngineOptions.InstanceId, evt.InstanceId));
    }

    [Fact]
    public async Task Should_complete_StopAsync_within_drain_timeout_when_events_peer_never_reads()
    {
        // Arrange
        var options = CreateOptions();
        options.ShutdownDrainTimeout = TimeSpan.FromMilliseconds(250);
        await using var harness = Create(options);
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Events, harness.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act + Assert — the peer never reads the pushed started
        // frame, so the events-pipe writer is blocked. StopAsync
        // must still return within the drain timeout (plus a
        // reasonable teardown slack) instead of deadlocking on the
        // stuck pump.
        await harness.Service.StopAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_recover_with_ParseError_on_malformed_rpc_frame_post_handshake_and_keep_serving()
    {
        // Arrange
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, harness.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act — send garbage that is not JSON, then a valid request.
        await codec.WriteAsync(
            Encoding.UTF8.GetBytes("not-json-here"), TestContext.Current.CancellationToken);
        var errorResponse = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        await SendRequestAsync(
            codec, id: 21, method: "Engine.DoesNotExist", TestContext.Current.CancellationToken);
        var followUp = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

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
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, harness.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act
        var bogus = Encoding.UTF8.GetBytes("""{"jsonrpc":"1.0","id":31,"method":"Engine.X"}""");
        await codec.WriteAsync(bogus, TestContext.Current.CancellationToken);
        var errorResponse = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        await SendRequestAsync(
            codec, id: 32, method: "Engine.Other", TestContext.Current.CancellationToken);
        var followUp = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

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
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, harness.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        // Act
        await codec.WriteAsync(
            Encoding.UTF8.GetBytes("definitely-not-json"), TestContext.Current.CancellationToken);
        var errorResponse = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);
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
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, harness.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        // Act — well-formed JSON, but wrong jsonrpc version.
        var bogus = Encoding.UTF8.GetBytes("""{"jsonrpc":"1.0","id":41,"method":"Engine.Hello"}""");
        await codec.WriteAsync(bogus, TestContext.Current.CancellationToken);
        var errorResponse = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);
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
        await using var harness = Create();
        await harness.Service.StartAsync(TestContext.Current.CancellationToken);

        await using var client = await ConnectAsync(
            EndpointKind.Rpc, harness.EngineOptions, TestContext.Current.CancellationToken);
        var codec = new LengthPrefixedFrameCodec(client);

        await SendHelloAsync(codec, ProtocolVersion.Current, TestContext.Current.CancellationToken);
        _ = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Act — valid JSON-RPC 2.0 frame with the id field absent.
        var noIdRequest = Encoding.UTF8.GetBytes(
            """{"jsonrpc":"2.0","method":"Engine.DoesNotExist"}""");
        await codec.WriteAsync(noIdRequest, TestContext.Current.CancellationToken);
        var response = await ReadResponseAsync(codec, TestContext.Current.CancellationToken);

        // Assert — the dispatcher normalises the response id to
        // JSON null per JsonRpcId.Normalize(request.Id).
        Assert.Multiple(
            () => Assert.NotNull(response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.MethodNotFound, response.Error!.Code),
            () => Assert.Equal(JsonValueKind.Null, response.Id.ValueKind));
    }
}
