namespace AutoContext.Engine.Core.Tests.Rpc.Policies;

using System.Text.Json;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Rpc;
using AutoContext.Engine.Core.Rpc.Policies;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Core.Tests.Support.Registry;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Core.Tests.Support.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Protocol.Serialization;

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
            () => new DispatchPolicy(null!, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_registryReader()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, registryReader: null!, LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_log_file_reader()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), logFileReader: null!, LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logsBroadcaster()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), logsBroadcaster: null!, LifecycleServiceFixture.CreateConfigAccessor(), NullLogger.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), logger: null!));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_configAccessor()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), configAccessor: null!, NullLogger.Instance));
    }

    [Fact]
    public void Should_expose_Rpc_EndpointKind_and_Recover_FrameFailurePolicy()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), NullLogger.Instance);

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
        var policy = new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), recorder);
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
        var policy = new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), recorder);

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
        var policy = new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), recorder);

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
        var policy = new DispatchPolicy(lifetime, reader, LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), NullLogger.Instance);
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
        var policy = new DispatchPolicy(lifetime, reader, LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), NullLogger.Instance);
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
        var policy = new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), NullLogger.Instance);
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
        var policy = new DispatchPolicy(lifetime, RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)), LifecycleServiceFixture.CreateLogFileReader(), LifecycleServiceFixture.CreateLogsBroadcaster(), LifecycleServiceFixture.CreateConfigAccessor(), NullLogger.Instance);
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
            LifecycleServiceFixture.CreateLogsBroadcaster(),
            LifecycleServiceFixture.CreateConfigAccessor(),
            NullLogger.Instance);
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
        var policy = new DispatchPolicy(
            lifetime,
            RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)),
            LifecycleServiceFixture.CreateLogFileReader(),
            LifecycleServiceFixture.CreateLogsBroadcaster(),
            LifecycleServiceFixture.CreateConfigAccessor(),
            NullLogger.Instance);

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
        var policy = new DispatchPolicy(
            lifetime,
            RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)),
            LifecycleServiceFixture.CreateLogFileReader(),
            LifecycleServiceFixture.CreateLogsBroadcaster(),
            LifecycleServiceFixture.CreateConfigAccessor(),
            NullLogger.Instance);

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
        var policy = new DispatchPolicy(
            lifetime,
            RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)),
            LifecycleServiceFixture.CreateLogFileReader(),
            broadcaster,
            LifecycleServiceFixture.CreateConfigAccessor(),
            NullLogger.Instance);
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
    public async Task Should_return_Continue_with_empty_snapshot_for_Config_Get_when_source_is_empty()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = new DispatchPolicy(
            lifetime,
            RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)),
            LifecycleServiceFixture.CreateLogFileReader(),
            LifecycleServiceFixture.CreateLogsBroadcaster(),
            new FakeConfigSnapshotAccessor(),
            NullLogger.Instance);
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
        var policy = new DispatchPolicy(
            lifetime,
            RegistryFileReaderTestFactory.Create(tempDirectory.CreatePath(RegistryFileName)),
            LifecycleServiceFixture.CreateLogFileReader(),
            LifecycleServiceFixture.CreateLogsBroadcaster(),
            new FakeConfigSnapshotAccessor { Current = config },
            NullLogger.Instance);
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
}
