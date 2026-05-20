namespace AutoContext.Engine.Core.Tests.Rpc.Policies;

using System.Text.Json;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Testing.Fakes;
using AutoContext.Engine.Core.Tests.Testing.Fixtures;
using AutoContext.Engine.Core.Tests.Testing.Utils;
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
            () => new DispatchPolicy(null!, CreateReader(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_registryReader()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, registryReader: null!, NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, CreateReader(), logger: null!));
    }

    [Fact]
    public void Should_expose_Rpc_EndpointKind_and_Recover_FrameFailurePolicy()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(lifetime, CreateReader(), NullLogger.Instance);

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
        var recorder = new RecordingLoggerFake();
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(lifetime, CreateReader(), recorder);
        var boom = new InvalidOperationException("framing");

        // Act
        InvokeHook(policy, hook, boom);

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
        var recorder = new RecordingLoggerFake();
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(lifetime, CreateReader(), recorder);

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
        var recorder = new RecordingLoggerFake();
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(lifetime, CreateReader(), recorder);

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
        var policy = new DispatchPolicy(lifetime, reader, NullLogger.Instance);
        var request = BuildRequest(RegistryMethods.RegistryEntries);

        // Act
        var result = await policy.InvokeAsync(request, TestContext.Current.CancellationToken);

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
        var policy = new DispatchPolicy(lifetime, reader, NullLogger.Instance);
        var request = BuildRequest(RegistryMethods.RegistryEntries);
        using var lockedHandle = new FileStream(
            registryPath, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act
        var result = await policy.InvokeAsync(request, TestContext.Current.CancellationToken);

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
        var policy = new DispatchPolicy(lifetime, CreateReader(), NullLogger.Instance);
        var request = BuildRequest(ProtocolMethods.Shutdown);

        // Act
        var result = await policy.InvokeAsync(request, TestContext.Current.CancellationToken);
        Assert.NotNull(result.PostFlush);
        await result.PostFlush!();

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
        var policy = new DispatchPolicy(lifetime, CreateReader(), NullLogger.Instance);
        var request = BuildRequest("Engine.WhoKnows");

        // Act
        var result = await policy.InvokeAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.NotNull(result.Response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.MethodNotFound, result.Response.Error!.Code));
    }

    private RegistryFileReader CreateReader()
    {
        var registryPath = tempDirectory.CreatePath(RegistryFileName);
        return new RegistryFileReader(registryPath);
    }

    private static JsonRpcRequest BuildRequest(string method) =>
        new()
        {
            Method = method,
            Id = JsonDocument.Parse("1").RootElement,
        };

    private static void InvokeHook(DispatchPolicy policy, string hook, Exception exception)
    {
        switch (hook)
        {
            case nameof(IRpcConnectionPolicy.LogFrameReadFault):
                policy.LogFrameReadFault(exception);
                break;
            case nameof(IRpcConnectionPolicy.LogFrameWriteFault):
                policy.LogFrameWriteFault(exception);
                break;
            default:
                policy.LogFrameParseFault(exception);
                break;
        }
    }
}
