namespace AutoContext.Engine.Core.Tests.Rpc.Policies;

using System.Text.Json;

using AutoContext.Engine.Core.Infrastructure;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Rpc;
using AutoContext.Engine.Core.Rpc.Policies;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Core.Tests.Support.Registry;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Core.Tests.Support.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Tests.Support.Workspace.Context;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Core.Workspace.Context;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Protocol.Messages.Workspace;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Engine.Tests.Support.Logging;

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
            () => new DispatchPolicy(null!, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), LifecycleServiceFixture.CreateConfigUpdater(), LifecycleServiceFixture.CreateConfigBroadcaster(), LifecycleServiceFixture.CreateWorkspaceAccessor(), LifecycleServiceFixture.CreateInstructionsManifestAccessor(), LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), LifecycleServiceFixture.CreateInstructionsBodyProjector(), LifecycleServiceFixture.CreateInstructionsFileReader(), LifecycleServiceFixture.CreateInstructionsSearchService(), LifecycleServiceFixture.CreateInstructionsBroadcaster(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_registryReader()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, registryReader: null!, LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), LifecycleServiceFixture.CreateConfigUpdater(), LifecycleServiceFixture.CreateConfigBroadcaster(), LifecycleServiceFixture.CreateWorkspaceAccessor(), LifecycleServiceFixture.CreateInstructionsManifestAccessor(), LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), LifecycleServiceFixture.CreateInstructionsBodyProjector(), LifecycleServiceFixture.CreateInstructionsFileReader(), LifecycleServiceFixture.CreateInstructionsSearchService(), LifecycleServiceFixture.CreateInstructionsBroadcaster(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_log_file_reader()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), logFileReader: null!, LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), LifecycleServiceFixture.CreateConfigUpdater(), LifecycleServiceFixture.CreateConfigBroadcaster(), LifecycleServiceFixture.CreateWorkspaceAccessor(), LifecycleServiceFixture.CreateInstructionsManifestAccessor(), LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), LifecycleServiceFixture.CreateInstructionsBodyProjector(), LifecycleServiceFixture.CreateInstructionsFileReader(), LifecycleServiceFixture.CreateInstructionsSearchService(), LifecycleServiceFixture.CreateInstructionsBroadcaster(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logsBroadcaster()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), logsBroadcaster: null!, LifecycleServiceFixture.CreateConfigAccessor(), LifecycleServiceFixture.CreateConfigUpdater(), LifecycleServiceFixture.CreateConfigBroadcaster(), LifecycleServiceFixture.CreateWorkspaceAccessor(), LifecycleServiceFixture.CreateInstructionsManifestAccessor(), LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), LifecycleServiceFixture.CreateInstructionsBodyProjector(), LifecycleServiceFixture.CreateInstructionsFileReader(), LifecycleServiceFixture.CreateInstructionsSearchService(), LifecycleServiceFixture.CreateInstructionsBroadcaster(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), LifecycleServiceFixture.CreateConfigUpdater(), LifecycleServiceFixture.CreateConfigBroadcaster(), LifecycleServiceFixture.CreateWorkspaceAccessor(), LifecycleServiceFixture.CreateInstructionsManifestAccessor(), LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), LifecycleServiceFixture.CreateInstructionsBodyProjector(), LifecycleServiceFixture.CreateInstructionsFileReader(), LifecycleServiceFixture.CreateInstructionsSearchService(), LifecycleServiceFixture.CreateInstructionsBroadcaster(), logger: null!));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_configAccessor()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), configAccessor: null!, LifecycleServiceFixture.CreateConfigUpdater(), LifecycleServiceFixture.CreateConfigBroadcaster(), LifecycleServiceFixture.CreateWorkspaceAccessor(), LifecycleServiceFixture.CreateInstructionsManifestAccessor(), LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), LifecycleServiceFixture.CreateInstructionsBodyProjector(), LifecycleServiceFixture.CreateInstructionsFileReader(), LifecycleServiceFixture.CreateInstructionsSearchService(), LifecycleServiceFixture.CreateInstructionsBroadcaster(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_configUpdater()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), configUpdater: null!, LifecycleServiceFixture.CreateConfigBroadcaster(), LifecycleServiceFixture.CreateWorkspaceAccessor(), LifecycleServiceFixture.CreateInstructionsManifestAccessor(), LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), LifecycleServiceFixture.CreateInstructionsBodyProjector(), LifecycleServiceFixture.CreateInstructionsFileReader(), LifecycleServiceFixture.CreateInstructionsSearchService(), LifecycleServiceFixture.CreateInstructionsBroadcaster(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_configBroadcaster()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), LifecycleServiceFixture.CreateConfigUpdater(), configBroadcaster: null!, LifecycleServiceFixture.CreateWorkspaceAccessor(), LifecycleServiceFixture.CreateInstructionsManifestAccessor(), LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), LifecycleServiceFixture.CreateInstructionsBodyProjector(), LifecycleServiceFixture.CreateInstructionsFileReader(), LifecycleServiceFixture.CreateInstructionsSearchService(), LifecycleServiceFixture.CreateInstructionsBroadcaster(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_workspaceAccessor()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), LifecycleServiceFixture.CreateConfigUpdater(), LifecycleServiceFixture.CreateConfigBroadcaster(), workspaceAccessor: null!, LifecycleServiceFixture.CreateInstructionsManifestAccessor(), LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), LifecycleServiceFixture.CreateInstructionsBodyProjector(), LifecycleServiceFixture.CreateInstructionsFileReader(), LifecycleServiceFixture.CreateInstructionsSearchService(), LifecycleServiceFixture.CreateInstructionsBroadcaster(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_manifestAccessor()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), LifecycleServiceFixture.CreateConfigUpdater(), LifecycleServiceFixture.CreateConfigBroadcaster(), LifecycleServiceFixture.CreateWorkspaceAccessor(), manifestAccessor: null!, LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), LifecycleServiceFixture.CreateInstructionsBodyProjector(), LifecycleServiceFixture.CreateInstructionsFileReader(), LifecycleServiceFixture.CreateInstructionsSearchService(), LifecycleServiceFixture.CreateInstructionsBroadcaster(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_overridesAccessor()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), LifecycleServiceFixture.CreateConfigUpdater(), LifecycleServiceFixture.CreateConfigBroadcaster(), LifecycleServiceFixture.CreateWorkspaceAccessor(), LifecycleServiceFixture.CreateInstructionsManifestAccessor(), overridesAccessor: null!, LifecycleServiceFixture.CreateInstructionsBodyProjector(), LifecycleServiceFixture.CreateInstructionsFileReader(), LifecycleServiceFixture.CreateInstructionsSearchService(), LifecycleServiceFixture.CreateInstructionsBroadcaster(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_bodyProjector()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), LifecycleServiceFixture.CreateConfigUpdater(), LifecycleServiceFixture.CreateConfigBroadcaster(), LifecycleServiceFixture.CreateWorkspaceAccessor(), LifecycleServiceFixture.CreateInstructionsManifestAccessor(), LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), bodyProjector: null!, LifecycleServiceFixture.CreateInstructionsFileReader(), LifecycleServiceFixture.CreateInstructionsSearchService(), LifecycleServiceFixture.CreateInstructionsBroadcaster(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_fileReader()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), LifecycleServiceFixture.CreateConfigUpdater(), LifecycleServiceFixture.CreateConfigBroadcaster(), LifecycleServiceFixture.CreateWorkspaceAccessor(), LifecycleServiceFixture.CreateInstructionsManifestAccessor(), LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), LifecycleServiceFixture.CreateInstructionsBodyProjector(), fileReader: null!, LifecycleServiceFixture.CreateInstructionsSearchService(), LifecycleServiceFixture.CreateInstructionsBroadcaster(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_searchService()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), LifecycleServiceFixture.CreateConfigUpdater(), LifecycleServiceFixture.CreateConfigBroadcaster(), LifecycleServiceFixture.CreateWorkspaceAccessor(), LifecycleServiceFixture.CreateInstructionsManifestAccessor(), LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), LifecycleServiceFixture.CreateInstructionsBodyProjector(), LifecycleServiceFixture.CreateInstructionsFileReader(), searchService: null!, LifecycleServiceFixture.CreateInstructionsBroadcaster(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_instructionsBroadcaster()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), LifecycleServiceFixture.CreateConfigUpdater(), LifecycleServiceFixture.CreateConfigBroadcaster(), LifecycleServiceFixture.CreateWorkspaceAccessor(), LifecycleServiceFixture.CreateInstructionsManifestAccessor(), LifecycleServiceFixture.CreateInstructionsOverridesAccessor(), LifecycleServiceFixture.CreateInstructionsBodyProjector(), LifecycleServiceFixture.CreateInstructionsFileReader(), LifecycleServiceFixture.CreateInstructionsSearchService(), instructionsBroadcaster: null!, NullLogger.Instance));
    }

    [Fact]
    public void Should_expose_Rpc_EndpointKind_and_Recover_FrameFailurePolicy()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(lifetime);

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
        var policy = DispatchPolicyTestFactory.Create(lifetime, logger: recorder);
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
        var policy = DispatchPolicyTestFactory.Create(lifetime, logger: recorder);

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
        var policy = DispatchPolicyTestFactory.Create(lifetime, logger: recorder);

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
        RegistryFileTestWriter.Write(registryPath, seeded);
        var reader = RegistryFileReaderTestFactory.Create(registryPath);
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(lifetime, registryReader: reader);
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
        RegistryFileTestWriter.Write(registryPath, RegistryEntryFakeData.CreateValidEntry());
        var readerOptions = new RegistryFileReaderOptions
        {
            MaxAttempts = 1,
            InitialRetryDelay = TimeSpan.FromMilliseconds(1),
            MaxRetryDelay = TimeSpan.FromMilliseconds(1),
        };
        var reader = new RegistryFileReader(registryPath, NullLogger<RegistryFileReader>.Instance, readerOptions);
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(lifetime, registryReader: reader);
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
        var policy = DispatchPolicyTestFactory.Create(lifetime);
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
        var policy = DispatchPolicyTestFactory.Create(lifetime);
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
        var policy = DispatchPolicyTestFactory.Create(lifetime);
        var request = JsonRpcRequestTestFactory.BuildRequest(LogsMethods.GetEngine);

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
            ProtocolJsonContext.Default.JsonLogsGetEngineResult);
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
        var policy = DispatchPolicyTestFactory.Create(lifetime);

        // params is a JSON string, not the expected object shape
        var badParams = JsonSerializer.SerializeToElement("not-an-object");
        var request = new JsonRpcRequest
        {
            Method = LogsMethods.GetEngine,
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
        var policy = DispatchPolicyTestFactory.Create(lifetime);

        var badParams = JsonSerializer.SerializeToElement(
            new JsonLogsGetEngineParams { LastN = -1 },
            ProtocolJsonContext.Default.JsonLogsGetEngineParams);
        var request = new JsonRpcRequest
        {
            Method = LogsMethods.GetEngine,
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
    public async Task Should_stream_record_frames_until_broadcaster_completes_for_Logs_TailEngine()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var broadcaster = LifecycleServiceFixture.CreateLogsBroadcaster();
        var policy = DispatchPolicyTestFactory.Create(lifetime, logsBroadcaster: broadcaster);
        var request = JsonRpcRequestTestFactory.BuildRequest(LogsMethods.TailEngine);

        // Act — invoke the handler to enrol a subscriber, then
        // pump two records through and complete the broadcaster so
        // the stream terminates cleanly.
        var result = await policy.InvokeAsync(request, TestContext.Current.CancellationToken);
        var streaming = Assert.IsType<StreamingHandlerResult>(result);

        var first = new JsonLogRecord
        {
            Timestamp = DateTimeOffset.UnixEpoch,
            Category = "test",
            Level = LogLevels.Information,
            Message = "hello",
        };
        var second = first with
        {
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(1),
            Message = "world",
        };
        Assert.True(broadcaster.TryPublish(first));
        Assert.True(broadcaster.TryPublish(second));
        broadcaster.Complete();

        var frames = new List<JsonElement>();
        await foreach (var frame in streaming.Payloads
            .WithCancellation(TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        // PostFlush is handler-supplied cleanup (subscription
        // disposal) — invoke it to mirror what the processor's
        // finally block does after a streaming response completes.
        Assert.NotNull(streaming.PostFlush);
        await streaming.PostFlush!();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Complete, streaming.Continuation),
            () => Assert.Equal(2, frames.Count),
            () => Assert.Equal("record", frames[0].GetProperty("kind").GetString()),
            () => Assert.Equal("hello", frames[0].GetProperty("record").GetProperty("message").GetString()),
            () => Assert.Equal("record", frames[1].GetProperty("kind").GetString()),
            () => Assert.Equal("world", frames[1].GetProperty("record").GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Should_stream_snapshot_frames_seeded_with_current_state_for_Config_Subscribe()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var broadcaster = LifecycleServiceFixture.CreateConfigBroadcaster();

        // Prime the broadcaster so the cold-start frame carries the
        // current state — a late subscriber needs no separate Get.
        broadcaster.Prime(new JsonConfigSnapshot { Version = "seed" });
        var policy = DispatchPolicyTestFactory.Create(lifetime, configBroadcaster: broadcaster);
        var request = JsonRpcRequestTestFactory.BuildRequest(ConfigMethods.Subscribe);

        // Act — invoke the handler to enrol a subscriber, then
        // publish an updated snapshot and complete the broadcaster
        // so the stream terminates cleanly.
        var result = await policy.InvokeAsync(request, TestContext.Current.CancellationToken);
        var streaming = Assert.IsType<StreamingHandlerResult>(result);

        Assert.True(broadcaster.TryPublish(new JsonConfigSnapshot { Version = "next" }));
        broadcaster.Complete();

        var frames = new List<JsonElement>();
        await foreach (var frame in streaming.Payloads
            .WithCancellation(TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        Assert.NotNull(streaming.PostFlush);
        await streaming.PostFlush!();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Complete, streaming.Continuation),
            () => Assert.Equal(2, frames.Count),
            () => Assert.Equal("snapshot", frames[0].GetProperty("kind").GetString()),
            () => Assert.Equal("seed", frames[0].GetProperty("snapshot").GetProperty("version").GetString()),
            () => Assert.Equal("snapshot", frames[1].GetProperty("kind").GetString()),
            () => Assert.Equal("next", frames[1].GetProperty("snapshot").GetProperty("version").GetString()));
    }

    [Fact]
    public async Task Should_stream_snapshot_frames_seeded_with_current_listing_for_Instructions_Subscribe()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var broadcaster = LifecycleServiceFixture.CreateInstructionsBroadcaster();

        // Prime the broadcaster so the cold-start frame carries the
        // current listing — a late subscriber needs no separate List.
        IReadOnlyList<JsonInstructionsListRow> seed = [new JsonInstructionsListRow { Key = "seed" }];
        broadcaster.Prime(seed);
        var policy = DispatchPolicyTestFactory.Create(lifetime, instructionsBroadcaster: broadcaster);
        var request = JsonRpcRequestTestFactory.BuildRequest(InstructionsMethods.Subscribe);

        // Act — invoke the handler to enrol a subscriber, then
        // publish an updated listing and complete the broadcaster so
        // the stream terminates cleanly.
        var result = await policy.InvokeAsync(request, TestContext.Current.CancellationToken);
        var streaming = Assert.IsType<StreamingHandlerResult>(result);

        IReadOnlyList<JsonInstructionsListRow> next = [new JsonInstructionsListRow { Key = "next" }];
        Assert.True(broadcaster.TryPublish(next));
        broadcaster.Complete();

        var frames = new List<JsonElement>();
        await foreach (var frame in streaming.Payloads
            .WithCancellation(TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        Assert.NotNull(streaming.PostFlush);
        await streaming.PostFlush!();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Complete, streaming.Continuation),
            () => Assert.Equal(2, frames.Count),
            () => Assert.Equal("snapshot", frames[0].GetProperty("kind").GetString()),
            () => Assert.Equal("seed", frames[0].GetProperty("files")[0].GetProperty("key").GetString()),
            () => Assert.Equal("snapshot", frames[1].GetProperty("kind").GetString()),
            () => Assert.Equal("next", frames[1].GetProperty("files")[0].GetProperty("key").GetString()));
    }

    [Fact]
    public async Task Should_return_Continue_with_empty_snapshot_for_Config_Get_when_source_is_empty()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            configAccessor: new FakeConfigSnapshotAccessor(),
            configUpdater: new FakeConfigSnapshotAccessor());
        var request = JsonRpcRequestTestFactory.BuildRequest(ConfigMethods.Get);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var snapshot = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonConfigSnapshot);
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.Null(result.Response.Error),
            () => Assert.NotNull(snapshot),
            () => Assert.Null(snapshot!.Version),
            () => Assert.Null(snapshot!.Diagnostic),
            () => Assert.Empty(snapshot!.Instructions),
            () => Assert.Empty(snapshot!.McpTools));
    }

    [Fact]
    public async Task Should_return_Continue_with_current_snapshot_for_Config_Get()
    {
        // Arrange
        var config = new ConfigSnapshot
        {
            Version = "9.9.9",
            Diagnostic = new ConfigDiagnostic { WarnOnMissingId = true },
            Instructions =
            [
                new ConfigInstructionsFile
                {
                    Name = "lang-csharp",
                    Version = "1.2",
                    Disabled = true,
                    Rules =
                    [
                        new ConfigInstructionsFile.InstructionsRule
                        {
                            Id = "no-var",
                            Disabled = true,
                        },
                    ],
                },
            ],
            McpTools =
            [
                new ConfigMcpTool
                {
                    Name = "analyze_csharp_code",
                    Version = "2.0",
                    Disabled = false,
                    Tasks =
                    [
                        new ConfigMcpTool.McpTask
                        {
                            Name = "lint",
                            Disabled = true,
                        },
                    ],
                },
            ],
        };
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            configAccessor: new FakeConfigSnapshotAccessor { Current = config },
            configUpdater: new FakeConfigSnapshotAccessor());
        var request = JsonRpcRequestTestFactory.BuildRequest(ConfigMethods.Get);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var snapshot = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonConfigSnapshot);
        Assert.NotNull(snapshot);
        var instructions = Assert.Single(snapshot!.Instructions);
        var rule = Assert.Single(instructions.Rules);
        var tool = Assert.Single(snapshot.McpTools);
        var task = Assert.Single(tool.Tasks);
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.Null(result.Response.Error),
            () => Assert.Equal("9.9.9", snapshot.Version),
            () => Assert.True(snapshot.Diagnostic!.WarnOnMissingId),
            () => Assert.Equal("lang-csharp", instructions.Name),
            () => Assert.Equal("1.2", instructions.Version),
            () => Assert.True(instructions.Disabled),
            () => Assert.Equal("no-var", rule.Id),
            () => Assert.True(rule.Disabled),
            () => Assert.Equal("analyze_csharp_code", tool.Name),
            () => Assert.Equal("2.0", tool.Version),
            () => Assert.False(tool.Disabled),
            () => Assert.Equal("lint", task.Name),
            () => Assert.True(task.Disabled));
    }

    [Fact]
    public async Task Should_disable_untracked_file_and_return_snapshot_for_Config_ToggleFile()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var store = new FakeConfigSnapshotAccessor();
        var policy = CreateConfigPolicy(lifetime, store);
        var request = BuildToggleFileRequest("lang-csharp");

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var snapshot = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonConfigSnapshot);
        var file = Assert.Single(snapshot!.Instructions);
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.Null(result.Response.Error),
            () => Assert.Equal("lang-csharp", file.Name),
            () => Assert.True(file.Disabled),
            () => Assert.True(Assert.Single(store.Current.Instructions).Disabled));
    }

    [Fact]
    public async Task Should_re_enable_and_prune_disabled_file_for_Config_ToggleFile()
    {
        // Arrange — a file present only because it is disabled
        // becomes empty once re-enabled and is dropped entirely.
        using var lifetime = new FakeHostApplicationLifetime();
        var store = new FakeConfigSnapshotAccessor
        {
            Current = ConfigSnapshot.Empty with
            {
                Instructions = [new ConfigInstructionsFile { Name = "lang-csharp", Disabled = true }],
            },
        };
        var policy = CreateConfigPolicy(lifetime, store);
        var request = BuildToggleFileRequest("lang-csharp");

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var snapshot = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonConfigSnapshot);
        Assert.Multiple(
            () => Assert.Null(result.Response.Error),
            () => Assert.Empty(snapshot!.Instructions),
            () => Assert.Empty(store.Current.Instructions));
    }

    [Fact]
    public async Task Should_return_InvalidParams_when_Config_ToggleFile_name_is_missing()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var store = new FakeConfigSnapshotAccessor();
        var policy = CreateConfigPolicy(lifetime, store);
        var request = BuildToggleFileRequest("   ");

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.NotNull(result.Response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidParams, result.Response.Error!.Code),
            () => Assert.Empty(store.Current.Instructions));
    }

    [Fact]
    public async Task Should_return_InvalidParams_for_malformed_Config_ToggleFile_params()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var store = new FakeConfigSnapshotAccessor();
        var policy = CreateConfigPolicy(lifetime, store);
        var request = new JsonRpcRequest
        {
            Method = ConfigMethods.ToggleFile,
            Id = JsonSerializer.SerializeToElement(1),
            Params = JsonSerializer.SerializeToElement("not-an-object"),
        };

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(result.Response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidParams, result.Response.Error!.Code));
    }

    [Fact]
    public async Task Should_disable_rule_within_file_for_Config_ToggleRule()
    {
        // Arrange — file already disabled at the whole-file level;
        // toggling a rule records the rule without touching the file flag.
        using var lifetime = new FakeHostApplicationLifetime();
        var store = new FakeConfigSnapshotAccessor
        {
            Current = ConfigSnapshot.Empty with
            {
                Instructions = [new ConfigInstructionsFile { Name = "lang-csharp", Disabled = true }],
            },
        };
        var policy = CreateConfigPolicy(lifetime, store);
        var request = BuildToggleRuleRequest("lang-csharp", "no-var");

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var snapshot = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonConfigSnapshot);
        var file = Assert.Single(snapshot!.Instructions);
        var rule = Assert.Single(file.Rules);
        Assert.Multiple(
            () => Assert.Null(result.Response.Error),
            () => Assert.True(file.Disabled),
            () => Assert.Equal("no-var", rule.Id),
            () => Assert.True(rule.Disabled));
    }

    [Fact]
    public async Task Should_re_enable_rule_and_prune_empty_file_for_Config_ToggleRule()
    {
        // Arrange — file present only because of one disabled rule;
        // re-enabling that rule empties the file, which is dropped.
        using var lifetime = new FakeHostApplicationLifetime();
        var store = new FakeConfigSnapshotAccessor
        {
            Current = ConfigSnapshot.Empty with
            {
                Instructions =
                [
                    new ConfigInstructionsFile
                    {
                        Name = "lang-csharp",
                        Rules = [new ConfigInstructionsFile.InstructionsRule { Id = "no-var", Disabled = true }],
                    },
                ],
            },
        };
        var policy = CreateConfigPolicy(lifetime, store);
        var request = BuildToggleRuleRequest("lang-csharp", "no-var");

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var snapshot = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonConfigSnapshot);
        Assert.Multiple(
            () => Assert.Null(result.Response.Error),
            () => Assert.Empty(snapshot!.Instructions),
            () => Assert.Empty(store.Current.Instructions));
    }

    [Fact]
    public async Task Should_return_InvalidParams_when_Config_ToggleRule_ruleId_is_missing()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var store = new FakeConfigSnapshotAccessor();
        var policy = CreateConfigPolicy(lifetime, store);
        var request = BuildToggleRuleRequest("lang-csharp", "   ");

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(result.Response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidParams, result.Response.Error!.Code),
            () => Assert.Empty(store.Current.Instructions));
    }

    [Fact]
    public async Task Should_return_InternalError_when_Config_ToggleFile_update_fails()
    {
        // Arrange — the updater faults while publishing the edit.
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            configAccessor: new FakeConfigSnapshotAccessor(),
            configUpdater: new ThrowingConfigUpdater(new IOException("disk full")));
        var request = BuildToggleFileRequest("lang-csharp");

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
    public async Task Should_return_Continue_with_empty_detection_for_Workspace_Detect_when_result_is_empty()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            workspaceAccessor: new FakeWorkspaceContextAccessor());
        var request = JsonRpcRequestTestFactory.BuildRequest(WorkspaceMethods.Detect);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var detect = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonWorkspaceDetectResult);
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.Null(result.Response.Error),
            () => Assert.NotNull(detect),
            () => Assert.Empty(detect!.Extensions),
            () => Assert.False(detect!.Flags.HasCSharp),
            () => Assert.False(detect!.Flags.HasNodeJs));
    }

    [Fact]
    public async Task Should_return_Continue_with_current_detection_for_Workspace_Detect()
    {
        // Arrange
        var detection = new WorkspaceDetectionResult
        {
            Extensions = ["cs", "ts"],
            Flags = new HashSet<string> { "hasCSharp", "hasNodeJs" },
        };
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            workspaceAccessor: new FakeWorkspaceContextAccessor { Current = detection });
        var request = JsonRpcRequestTestFactory.BuildRequest(WorkspaceMethods.Detect);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var detect = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonWorkspaceDetectResult);
        Assert.NotNull(detect);
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.Null(result.Response.Error),
            () => Assert.Equal(["cs", "ts"], detect!.Extensions),
            () => Assert.True(detect!.Flags.HasCSharp),
            () => Assert.True(detect!.Flags.HasNodeJs),
            () => Assert.False(detect!.Flags.HasPython));
    }

    [Fact]
    public async Task Should_omit_overrides_field_from_Workspace_Detect_envelope()
    {
        // Arrange — the detector is blind to override content: a
        // workspace with files under .github/instructions/ produces
        // the same Detect envelope as one without. The wire shape
        // carries no overrides field (negative-shape contract).
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            workspaceAccessor: new FakeWorkspaceContextAccessor
            {
                Current = new WorkspaceDetectionResult
                {
                    Extensions = ["cs"],
                    Flags = new HashSet<string> { "hasCSharp" },
                },
            });
        var request = JsonRpcRequestTestFactory.BuildRequest(WorkspaceMethods.Detect);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var envelope = result.Response.Result!.Value;
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.Null(result.Response.Error),
            () => Assert.True(envelope.TryGetProperty("extensions", out _)),
            () => Assert.True(envelope.TryGetProperty("flags", out _)),
            () => Assert.False(envelope.TryGetProperty("overrides", out _)));
    }

    [Fact]
    public async Task Should_return_Continue_with_current_workspace_info_for_Workspace_Info()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var accessor = new FakeWorkspaceContextAccessor
        {
            EngineInfo = new FakeWorkspaceEngineInfo
            {
                IdleTimeout = TimeSpan.FromMinutes(5),
                InstanceId = instanceId,
                InstanceLabel = "primary",
            },
            Revision = 42,
        };
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(lifetime, workspaceAccessor: accessor);
        var request = JsonRpcRequestTestFactory.BuildRequest(WorkspaceMethods.Info);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var info = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonWorkspaceInfoResult);
        Assert.NotNull(info);
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, result.Continuation),
            () => Assert.Null(result.Response.Error),
            () => Assert.Equal(EngineVersion.Value, info!.EngineVersion),
            () => Assert.Equal(TimeSpan.FromMinutes(5), info!.IdleTimeout),
            () => Assert.Equal(instanceId, info!.InstanceId),
            () => Assert.Equal("primary", info!.InstanceLabel),
            () => Assert.Equal(42L, info!.Revision));
    }

    [Fact]
    public async Task Should_keep_empty_workspace_info_label_as_empty_string()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            workspaceAccessor: new FakeWorkspaceContextAccessor
            {
                EngineInfo = new FakeWorkspaceEngineInfo { InstanceLabel = string.Empty },
            });
        var request = JsonRpcRequestTestFactory.BuildRequest(WorkspaceMethods.Info);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var info = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonWorkspaceInfoResult);
        Assert.Multiple(
            () => Assert.Null(result.Response.Error),
            () => Assert.NotNull(info),
            () => Assert.Equal(string.Empty, info!.InstanceLabel));
    }

    private static JsonRpcRequest BuildToggleFileRequest(string name) =>
        new()
        {
            Method = ConfigMethods.ToggleFile,
            Id = JsonSerializer.SerializeToElement(1),
            Params = JsonSerializer.SerializeToElement(
                new JsonConfigToggleFileParams { Name = name },
                ProtocolJsonContext.Default.JsonConfigToggleFileParams),
        };

    private static JsonRpcRequest BuildToggleRuleRequest(string name, string ruleId) =>
        new()
        {
            Method = ConfigMethods.ToggleRule,
            Id = JsonSerializer.SerializeToElement(1),
            Params = JsonSerializer.SerializeToElement(
                new JsonConfigToggleRuleParams { Name = name, RuleId = ruleId },
                ProtocolJsonContext.Default.JsonConfigToggleRuleParams),
        };

    private static DispatchPolicy CreateConfigPolicy(
        FakeHostApplicationLifetime lifetime, FakeConfigSnapshotAccessor store) =>
        DispatchPolicyTestFactory.Create(
            lifetime,
            configAccessor: store,
            configUpdater: store);
}
