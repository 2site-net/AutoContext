namespace AutoContext.Engine.Core.Tests.Rpc.Policies;

using System.Text.Json;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Core.Tests.Support.Registry;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Core.Tests.Support.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Registry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class DispatchPolicyTests(TempDirectoryFixture tempDirectory)
    : IClassFixture<TempDirectoryFixture>
{
    private const string RegistryFileName = "engine-registry.json";

    [Fact]
    public void Should_throw_when_constructed_with_null_lifetime()
    {
        // Arrange + Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(null!, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_registryReader()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, registryReader: null!, LifecycleServiceFixture.CreateLogFileReader(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_log_file_reader()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), logFileReader: null!, NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), logger: null!));
    }

    [Fact]
    public void Should_expose_Rpc_EndpointKind_and_Recover_FrameFailurePolicy()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), NullLogger.Instance);

        // Act + Assert
        Assert.Multiple(
            () => Assert.Equal(EndpointKind.Rpc, policy.EndpointKind),
            () => Assert.Equal(FrameFailurePolicy.Recover, policy.FrameFailurePolicy));
    }

    [Theory]
    [InlineData(nameof(IRpcConnectionPolicy.LogFrameReadFault))]
    [InlineData(nameof(IRpcConnectionPolicy.LogFrameWriteFault))]
    [InlineData(nameof(IRpcConnectionPolicy.LogFrameParseFault))]
    public void Should_log_frame_faults_as_Debug(string hook)
    {
        // Arrange
        var recorder = new FakeRecordingLogger();
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), recorder);
        var boom = new InvalidOperationException("framing");

        // Act
        PolicyTestHookInvoker.InvokeHook(policy, hook, boom);

        // Assert
        var entry = Assert.Single(recorder.Entries);
        Assert.Multiple(
            () => Assert.Equal(LogLevel.Debug, entry.Level),
            () => Assert.Same(boom, entry.Exception));
    }

    [Fact]
    public void Should_log_LogFrameInvalidRequest_as_Debug()
    {
        // Arrange
        var recorder = new FakeRecordingLogger();
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), recorder);

        // Act
        policy.LogFrameInvalidRequest();

        // Assert
        var entry = Assert.Single(recorder.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
    }

    [Fact]
    public void Should_emit_no_log_when_connection_is_closed_by_peer()
    {
        // Arrange
        var recorder = new FakeRecordingLogger();
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), recorder);

        // Act
        policy.LogConnectionClosedByPeer();

        // Assert
        Assert.Empty(recorder.Entries);
    }

    [Fact]
    public async Task Should_return_Continue_with_RegistryEntries_when_reader_succeeds()
    {
        // Arrange
        var registryPath = tempDirectory.CreatePath(RegistryFileName);
        var seeded = new[] { RegistryEntryFakeData.CreateValidEntry() };
        new RegistryFileWriter(registryPath).Write(seeded);
        var reader = new RegistryFileReader(registryPath);
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(lifetime, reader, LifecycleServiceFixture.CreateLogFileReader(), NullLogger.Instance);
        var request = JsonRpcRequestTestFactory.BuildRequest(RegistryMethods.RegistryEntries);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.Null(result.Response.Error),
            () => Assert.NotNull(result.Response.Result),
            () => Assert.Null(result.PostFlush),
            () => Assert.Equal(0, lifetime.StopApplicationCallCount));
    }

    [Fact]
    public async Task Should_return_Continue_with_InternalError_when_reader_throws_IOException()
    {
        // Arrange — seed the file then hold an exclusive handle so
        // the reader's single attempt fails with IOException, which
        // the dispatch handler translates into an InternalError.
        var registryPath = tempDirectory.CreatePath(RegistryFileName);
        new RegistryFileWriter(registryPath).Write([RegistryEntryFakeData.CreateValidEntry()]);
        var readerOptions = new RegistryFileReaderOptions
        {
            MaxAttempts = 1,
            InitialRetryDelay = TimeSpan.FromMilliseconds(1),
            MaxRetryDelay = TimeSpan.FromMilliseconds(1),
        };
        var reader = new RegistryFileReader(registryPath, readerOptions);
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(lifetime, reader, LifecycleServiceFixture.CreateLogFileReader(), NullLogger.Instance);
        var request = JsonRpcRequestTestFactory.BuildRequest(RegistryMethods.RegistryEntries);
        using var lockedHandle = new FileStream(
            registryPath, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.NotNull(result.Response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InternalError, result.Response.Error!.Code));
    }

    [Fact]
    public async Task Should_complete_with_PostFlush_that_calls_StopApplication_for_Shutdown()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), NullLogger.Instance);
        var request = JsonRpcRequestTestFactory.BuildRequest(ProtocolMethods.Shutdown);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));
        Assert.NotNull(result.PostFlush);
        await result.PostFlush();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Complete, result.Continuation),
            () => Assert.NotNull(result.Response.Result),
            () => Assert.Equal(1, lifetime.StopApplicationCallCount));
    }

    [Fact]
    public async Task Should_return_Continue_with_MethodNotFound_for_unknown_method()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), NullLogger.Instance);
        var request = JsonRpcRequestTestFactory.BuildRequest("Engine.WhoKnows");

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.NotNull(result.Response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.MethodNotFound, result.Response.Error!.Code));
    }

    [Fact]
    public async Task Should_return_empty_LogsGetEngineResult_when_engine_log_file_is_absent()
    {
        // Arrange — reader points at a fresh CacheRootOverride
        // where no engine.log has been written yet.
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(
            lifetime,
            RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)),
            LifecycleServiceFixture.CreateLogFileReader(),
            NullLogger.Instance);
        var request = JsonRpcRequestTestFactory.BuildRequest(Protocol.Messages.Logs.LogsMethods.GetEngine);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.Null(result.Response.Error),
            () => Assert.NotNull(result.Response.Result));

        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            Protocol.Serialization.ProtocolJsonContext.Default.LogsGetEngineResult);
        Assert.NotNull(payload);
        Assert.Multiple(
            () => Assert.Empty(payload!.Records),
            () => Assert.False(payload!.Truncated));
    }

    [Fact]
    public async Task Should_return_InvalidParams_for_malformed_Logs_GetEngine_params()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(
            lifetime,
            RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)),
            LifecycleServiceFixture.CreateLogFileReader(),
            NullLogger.Instance);

        // params is a JSON string, not the expected object shape
        var badParams = JsonSerializer.SerializeToElement("not-an-object");
        var request = new JsonRpcRequest
        {
            Method = Protocol.Messages.Logs.LogsMethods.GetEngine,
            Id = JsonSerializer.SerializeToElement(1),
            Params = badParams,
        };

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.NotNull(result.Response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidParams, result.Response.Error!.Code));
    }

    [Fact]
    public async Task Should_return_InvalidParams_when_Logs_GetEngine_LastN_is_negative()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(
            lifetime,
            RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)),
            LifecycleServiceFixture.CreateLogFileReader(),
            NullLogger.Instance);

        var badParams = JsonSerializer.SerializeToElement(
            new Protocol.Messages.Logs.LogsGetEngineParams { LastN = -1 },
            Protocol.Serialization.ProtocolJsonContext.Default.LogsGetEngineParams);
        var request = new JsonRpcRequest
        {
            Method = Protocol.Messages.Logs.LogsMethods.GetEngine,
            Id = JsonSerializer.SerializeToElement(1),
            Params = badParams,
        };

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.NotNull(result.Response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidParams, result.Response.Error!.Code));
    }
}
