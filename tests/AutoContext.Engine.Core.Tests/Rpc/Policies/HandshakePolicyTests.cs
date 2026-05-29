namespace AutoContext.Engine.Core.Tests.Rpc.Policies;

using System.Text.Json;

using AutoContext.Engine.Core.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using AutoContext.Engine.Core.Tests.Support.Rpc;

public sealed class HandshakePolicyTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        // Arrange + Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new HandshakePolicy(EndpointKind.Rpc, logger: null!));
    }

    [Fact]
    public void Should_expose_constructed_EndpointKind_and_Terminate_FrameFailurePolicy()
    {
        // Arrange
        var policy = new HandshakePolicy(EndpointKind.Events, NullLogger.Instance);

        // Act + Assert
        Assert.Multiple(
            () => Assert.Equal(EndpointKind.Events, policy.EndpointKind),
            () => Assert.Equal(FrameFailurePolicy.Terminate, policy.FrameFailurePolicy));
    }

    [Theory]
    [InlineData(nameof(IRpcConnectionPolicy.LogFrameReadFault))]
    [InlineData(nameof(IRpcConnectionPolicy.LogFrameWriteFault))]
    [InlineData(nameof(IRpcConnectionPolicy.LogFrameParseFault))]
    public void Should_log_frame_faults_as_Warning(string hook)
    {
        // Arrange
        var recorder = new FakeRecordingLogger();
        var policy = new HandshakePolicy(EndpointKind.Rpc, recorder);
        var boom = new InvalidOperationException("framing");

        // Act
        PolicyTestHookInvoker.InvokeHook(policy, hook, boom);

        // Assert
        var entry = Assert.Single(recorder.Entries);
        Assert.Multiple(
            () => Assert.Equal(LogLevel.Warning, entry.Level),
            () => Assert.Same(boom, entry.Exception));
    }

    [Fact]
    public void Should_log_LogFrameInvalidRequest_as_Warning()
    {
        // Arrange
        var recorder = new FakeRecordingLogger();
        var policy = new HandshakePolicy(EndpointKind.Rpc, recorder);

        // Act
        policy.LogFrameInvalidRequest();

        // Assert
        var entry = Assert.Single(recorder.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    [Fact]
    public void Should_log_LogConnectionClosedByPeer_as_Debug()
    {
        // Arrange
        var recorder = new FakeRecordingLogger();
        var policy = new HandshakePolicy(EndpointKind.Rpc, recorder);

        // Act
        policy.LogConnectionClosedByPeer();

        // Assert
        var entry = Assert.Single(recorder.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
    }

    [Fact]
    public async Task Should_abort_with_HelloRequired_when_method_is_not_Hello()
    {
        // Arrange
        var policy = new HandshakePolicy(EndpointKind.Rpc, NullLogger.Instance);
        var request = JsonRpcRequestTestFactory.BuildHelloRequest("Engine.SomethingElse", protocolVersion: ProtocolVersion.Current);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Abort, result.Continuation),
            () => Assert.NotNull(result.Response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.HelloRequired, result.Response.Error!.Code),
            () => Assert.Null(result.PostFlush));
    }

    [Fact]
    public async Task Should_abort_with_InvalidParams_when_params_are_missing()
    {
        // Arrange
        var policy = new HandshakePolicy(EndpointKind.Rpc, NullLogger.Instance);
        var request = new JsonRpcRequest
        {
            Method = ProtocolMethods.Hello,
            Id = JsonDocument.Parse("1").RootElement,
        };

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Abort, result.Continuation),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidParams, result.Response.Error!.Code));
    }

    [Fact]
    public async Task Should_abort_with_InvalidParams_when_protocolVersion_is_omitted()
    {
        // Arrange
        var policy = new HandshakePolicy(EndpointKind.Rpc, NullLogger.Instance);
        var request = JsonRpcRequestTestFactory.BuildHelloRequest(ProtocolMethods.Hello, protocolVersion: null);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Abort, result.Continuation),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidParams, result.Response.Error!.Code));
    }

    [Fact]
    public async Task Should_abort_with_ProtocolVersionMismatch_when_version_differs()
    {
        // Arrange
        var policy = new HandshakePolicy(EndpointKind.Rpc, NullLogger.Instance);
        var request = JsonRpcRequestTestFactory.BuildHelloRequest(
            ProtocolMethods.Hello, protocolVersion: ProtocolVersion.Current + 1);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Abort, result.Continuation),
            () => Assert.Equal(JsonRpcErrorCodes.ProtocolVersionMismatch, result.Response.Error!.Code));
    }

    [Fact]
    public async Task Should_complete_with_HandshakeResult_when_versions_match()
    {
        // Arrange
        var policy = new HandshakePolicy(EndpointKind.Rpc, NullLogger.Instance);
        var request = JsonRpcRequestTestFactory.BuildHelloRequest(
            ProtocolMethods.Hello, protocolVersion: ProtocolVersion.Current);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Complete, result.Continuation),
            () => Assert.Null(result.Response.Error),
            () => Assert.NotNull(result.Response.Result),
            () => Assert.Null(result.PostFlush));
    }
}
