namespace AutoContext.Engine.Core.Tests.Lifecycle;

using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Tests.Testing.Utils;
using AutoContext.Framework.Pipes;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class LifecycleServiceTests
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(5);

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

    private static async Task<NamedPipeClientStream> ConnectAsync(
        EndpointKind kind,
        EngineOptions options,
        CancellationToken cancellationToken)
    {
        var workspaceHash = WorkspaceHash.Compute(options.WorkspacePath);
        var pipeName = new Endpoint(kind, workspaceHash.Value, options.InstanceId).ToString();
        var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(ConnectTimeout, cancellationToken).ConfigureAwait(false);
        return client;
    }

    private static async Task SendHelloAsync(
        LengthPrefixedFrameCodec codec,
        int protocolVersion,
        CancellationToken cancellationToken)
    {
        var paramsElement = JsonSerializer.SerializeToElement(
            new HandshakeParams { ProtocolVersion = protocolVersion },
            ProtocolJsonContext.Default.HandshakeParams);

        var idElement = JsonDocument.Parse("1").RootElement;

        var request = new JsonRpcRequest
        {
            Jsonrpc = JsonRpcVersion.Value,
            Id = idElement,
            Method = ProtocolMethods.Hello,
            Params = paramsElement,
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            request, ProtocolJsonContext.Default.JsonRpcRequest);

        await codec.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<JsonRpcResponse> ReadResponseAsync(
        LengthPrefixedFrameCodec codec,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(HandshakeTimeout);

        var bytes = await codec.ReadAsync(cts.Token).ConfigureAwait(false);
        Assert.NotNull(bytes);

        var response = JsonSerializer.Deserialize(
            bytes!, ProtocolJsonContext.Default.JsonRpcResponse);
        Assert.NotNull(response);
        return response!;
    }

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
}
