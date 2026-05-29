namespace AutoContext.Engine.Core.Tests.Rpc;

using System.Text;
using System.Text.Json;

using AutoContext.Engine.Core.Rpc;
using AutoContext.Engine.Core.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class RpcConnectionProcessorTests
{
    [Fact]
    public async Task Should_return_true_when_handler_returns_Complete()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);

        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) => ValueTask.FromResult<RpcHandlerResult>(new UnaryHandlerResult(
                Response: JsonRpcResponseFakeData.BuildOkResponse(),
                Continuation: Continuation.Complete)),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Act
        await JsonRpcTestClient.WriteRequestAsync(clientCodec, id: 1, method: "Test.Done", TestContext.Current.CancellationToken);
        var response = await JsonRpcTestClient.ReadResponseAsync(clientCodec, TestContext.Current.CancellationToken);
        var result = await processorTask;

        // Assert
        Assert.Multiple(
            () => Assert.True(result),
            () => Assert.Null(response.Error),
            () => Assert.Equal(1, response.Id.GetInt32()));
    }

    [Fact]
    public async Task Should_return_false_when_handler_returns_Abort()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);

        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) => ValueTask.FromResult<RpcHandlerResult>(new UnaryHandlerResult(
                Response: JsonRpcResponseFakeData.BuildErrorResponse(-1, "nope"),
                Continuation: Continuation.Abort)),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Act
        await JsonRpcTestClient.WriteRequestAsync(clientCodec, id: 2, method: "Test.Abort", TestContext.Current.CancellationToken);
        var response = await JsonRpcTestClient.ReadResponseAsync(clientCodec, TestContext.Current.CancellationToken);
        var result = await processorTask;

        // Assert
        Assert.Multiple(
            () => Assert.False(result),
            () => Assert.NotNull(response.Error),
            () => Assert.Equal(-1, response.Error!.Code));
    }

    [Fact]
    public async Task Should_keep_serving_when_handler_returns_Continue()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);

        var invocations = 0;
        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) =>
            {
                Interlocked.Increment(ref invocations);
                return ValueTask.FromResult<RpcHandlerResult>(new UnaryHandlerResult(
                    Response: JsonRpcResponseFakeData.BuildOkResponse(),
                    Continuation: Continuation.Continue));
            },
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Act — three back-to-back requests on the same connection.
        await JsonRpcTestClient.WriteRequestAsync(clientCodec, id: 10, method: "Test.Continue", TestContext.Current.CancellationToken);
        var first = await JsonRpcTestClient.ReadResponseAsync(clientCodec, TestContext.Current.CancellationToken);
        await JsonRpcTestClient.WriteRequestAsync(clientCodec, id: 11, method: "Test.Continue", TestContext.Current.CancellationToken);
        var second = await JsonRpcTestClient.ReadResponseAsync(clientCodec, TestContext.Current.CancellationToken);
        await JsonRpcTestClient.WriteRequestAsync(clientCodec, id: 12, method: "Test.Continue", TestContext.Current.CancellationToken);
        var third = await JsonRpcTestClient.ReadResponseAsync(clientCodec, TestContext.Current.CancellationToken);
        await clientStream.DisposeAsync();
        var result = await processorTask;

        // Assert
        Assert.Multiple(
            () => Assert.False(result),
            () => Assert.Equal(3, invocations),
            () => Assert.Equal(10, first.Id.GetInt32()),
            () => Assert.Equal(11, second.Id.GetInt32()),
            () => Assert.Equal(12, third.Id.GetInt32()),
            () => Assert.True(policy.ConnectionClosedByPeerCount >= 1));
    }

    [Fact]
    public async Task Should_return_false_and_log_peer_close_on_clean_EOF()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var serverGuard = serverStream;
        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) => throw new InvalidOperationException(
                "Handler must not be called when no frame arrives."),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Act
        await clientStream.DisposeAsync();
        var result = await processorTask;

        // Assert
        Assert.Multiple(
            () => Assert.False(result),
            () => Assert.Equal(1, policy.ConnectionClosedByPeerCount));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_reply_ParseError_then_apply_frame_failure_policy_on_malformed_JSON(
        bool recover)
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);

        var followUpServed = false;
        var policy = new FakeRpcConnectionPolicy
        {
            FrameFailurePolicy = recover
                ? FrameFailurePolicy.Recover
                : FrameFailurePolicy.Terminate,
            OnInvoke = (_, _) =>
            {
                followUpServed = true;
                return ValueTask.FromResult<RpcHandlerResult>(new UnaryHandlerResult(
                    Response: JsonRpcResponseFakeData.BuildOkResponse(),
                    Continuation: Continuation.Continue));
            },
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Act
        await clientCodec.WriteAsync(
            Encoding.UTF8.GetBytes("not-json"), TestContext.Current.CancellationToken);
        var errorResponse = await JsonRpcTestClient.ReadResponseAsync(clientCodec, TestContext.Current.CancellationToken);

        var followUpResponse = recover
            ? await JsonRpcTestClient.DriveFollowUpAsync(clientCodec)
            : null;

        await clientStream.DisposeAsync();
        var result = await processorTask;

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(errorResponse.Error),
            () => Assert.Equal(JsonRpcErrorCodes.ParseError, errorResponse.Error!.Code),
            () => Assert.Equal(JsonValueKind.Null, errorResponse.Id.ValueKind),
            () => Assert.Equal(1, policy.ParseFaultCount),
            () => Assert.False(result),
            () => Assert.Equal(recover, followUpServed),
            () => Assert.Equal(recover, followUpResponse is not null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_reply_InvalidRequest_then_apply_frame_failure_policy_on_wrong_jsonrpc_version(
        bool recover)
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);

        var followUpServed = false;
        var policy = new FakeRpcConnectionPolicy
        {
            FrameFailurePolicy = recover
                ? FrameFailurePolicy.Recover
                : FrameFailurePolicy.Terminate,
            OnInvoke = (_, _) =>
            {
                followUpServed = true;
                return ValueTask.FromResult<RpcHandlerResult>(new UnaryHandlerResult(
                    Response: JsonRpcResponseFakeData.BuildOkResponse(),
                    Continuation: Continuation.Continue));
            },
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Act — valid JSON, but wrong "jsonrpc" version.
        var bogus = Encoding.UTF8.GetBytes("""{"jsonrpc":"1.0","id":3,"method":"x"}""");
        await clientCodec.WriteAsync(bogus, TestContext.Current.CancellationToken);
        var errorResponse = await JsonRpcTestClient.ReadResponseAsync(clientCodec, TestContext.Current.CancellationToken);

        var followUpResponse = recover
            ? await JsonRpcTestClient.DriveFollowUpAsync(clientCodec)
            : null;

        await clientStream.DisposeAsync();
        var result = await processorTask;

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(errorResponse.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidRequest, errorResponse.Error!.Code),
            () => Assert.Equal(3, errorResponse.Id.GetInt32()),
            () => Assert.Equal(1, policy.InvalidRequestCount),
            () => Assert.False(result),
            () => Assert.Equal(recover, followUpServed),
            () => Assert.Equal(recover, followUpResponse is not null));
    }

    [Fact]
    public async Task Should_reply_InvalidRequest_with_Null_id_when_request_lacks_id()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);

        var policy = new FakeRpcConnectionPolicy
        {
            FrameFailurePolicy = FrameFailurePolicy.Terminate,
            OnInvoke = (_, _) => throw new InvalidOperationException(
                "Handler must not be reached for a JSON-RPC-invalid frame."),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Act — valid JSON, wrong jsonrpc version, no id at all.
        var bogus = Encoding.UTF8.GetBytes("""{"jsonrpc":"1.0","method":"x"}""");
        await clientCodec.WriteAsync(bogus, TestContext.Current.CancellationToken);
        var errorResponse = await JsonRpcTestClient.ReadResponseAsync(clientCodec, TestContext.Current.CancellationToken);
        var result = await processorTask;

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(errorResponse.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidRequest, errorResponse.Error!.Code),
            () => Assert.Equal(JsonValueKind.Null, errorResponse.Id.ValueKind),
            () => Assert.False(result));
    }

    [Fact]
    public async Task Should_normalize_response_id_from_request_when_handler_leaves_it_Undefined()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);

        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) => ValueTask.FromResult<RpcHandlerResult>(new UnaryHandlerResult(
                Response: new JsonRpcResponse { Result = JsonDocument.Parse("{}").RootElement },
                Continuation: Continuation.Complete)),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Act
        await JsonRpcTestClient.WriteRequestAsync(clientCodec, id: 77, method: "Test.Echo", TestContext.Current.CancellationToken);
        var response = await JsonRpcTestClient.ReadResponseAsync(clientCodec, TestContext.Current.CancellationToken);
        var result = await processorTask;

        // Assert
        Assert.Multiple(
            () => Assert.True(result),
            () => Assert.Equal(77, response.Id.GetInt32()));
    }

    [Fact]
    public async Task Should_normalize_response_id_to_Null_when_request_omits_id_and_handler_leaves_it_Undefined()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);

        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) => ValueTask.FromResult<RpcHandlerResult>(new UnaryHandlerResult(
                Response: new JsonRpcResponse { Result = JsonDocument.Parse("{}").RootElement },
                Continuation: Continuation.Complete)),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Act — valid JSON-RPC frame with the id field absent.
        var noIdRequest = Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","method":"Test.Echo"}""");
        await clientCodec.WriteAsync(noIdRequest, TestContext.Current.CancellationToken);
        var response = await JsonRpcTestClient.ReadResponseAsync(clientCodec, TestContext.Current.CancellationToken);
        var result = await processorTask;

        // Assert
        Assert.Multiple(
            () => Assert.True(result),
            () => Assert.Equal(JsonValueKind.Null, response.Id.ValueKind));
    }

    [Fact]
    public async Task Should_invoke_PostFlush_after_response_is_written()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);

        var postFlushInvoked = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) => ValueTask.FromResult<RpcHandlerResult>(new UnaryHandlerResult(
                Response: JsonRpcResponseFakeData.BuildOkResponse(),
                Continuation: Continuation.Complete,
                PostFlush: () =>
                {
                    postFlushInvoked.TrySetResult();
                    return Task.CompletedTask;
                })),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Act
        await JsonRpcTestClient.WriteRequestAsync(clientCodec, id: 5, method: "Test.PostFlush", TestContext.Current.CancellationToken);
        var response = await JsonRpcTestClient.ReadResponseAsync(clientCodec, TestContext.Current.CancellationToken);
        await postFlushInvoked.Task.WaitAsync(
            TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var result = await processorTask;

        // Assert
        Assert.Multiple(
            () => Assert.True(result),
            () => Assert.Equal(5, response.Id.GetInt32()),
            () => Assert.True(postFlushInvoked.Task.IsCompletedSuccessfully));
    }

    [Fact]
    public async Task Should_swallow_PostFlush_exception_and_honour_Continuation()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);
        var recorder = new FakeRecordingLogger();

        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) => ValueTask.FromResult<RpcHandlerResult>(new UnaryHandlerResult(
                Response: JsonRpcResponseFakeData.BuildOkResponse(),
                Continuation: Continuation.Complete,
                PostFlush: () => throw new InvalidOperationException("post-flush boom"))),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, recorder, TestContext.Current.CancellationToken);

        // Act
        await JsonRpcTestClient.WriteRequestAsync(clientCodec, id: 6, method: "Test.PostFlush", TestContext.Current.CancellationToken);
        _ = await JsonRpcTestClient.ReadResponseAsync(clientCodec, TestContext.Current.CancellationToken);
        var result = await processorTask;

        // Assert
        var faultEntry = Assert.Single(
            recorder.Entries,
            e => e.Exception is InvalidOperationException);
        Assert.Multiple(
            () => Assert.True(result),
            () => Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, faultEntry.Level),
            () => Assert.Equal(1, faultEntry.EventId.Id));
    }

    [Fact]
    public async Task Should_return_false_when_handler_throws_OperationCanceledException_under_cancellation()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = async (_, ct) =>
            {
                await cts.CancelAsync();
                ct.ThrowIfCancellationRequested();
                return new UnaryHandlerResult(JsonRpcResponseFakeData.BuildOkResponse());
            },
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, cts.Token);

        // Act
        await JsonRpcTestClient.WriteRequestAsync(clientCodec, id: 8, method: "Test.Cancel", TestContext.Current.CancellationToken);
        var result = await processorTask;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Should_emit_Error_log_when_handler_returns_unknown_Continuation_value()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);
        var recorder = new FakeRecordingLogger();

        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) => ValueTask.FromResult<RpcHandlerResult>(new UnaryHandlerResult(
                Response: JsonRpcResponseFakeData.BuildOkResponse(),
                Continuation: (Continuation)99)),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, recorder, TestContext.Current.CancellationToken);

        // Act
        await JsonRpcTestClient.WriteRequestAsync(clientCodec, id: 9, method: "Test.Bogus", TestContext.Current.CancellationToken);
        _ = await JsonRpcTestClient.ReadResponseAsync(clientCodec, TestContext.Current.CancellationToken);
        var result = await processorTask;

        // Assert
        var unknownEntry = Assert.Single(
            recorder.Entries,
            e => e.EventId.Id == 2);
        Assert.Multiple(
            () => Assert.False(result),
            () => Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, unknownEntry.Level));
    }

    // -- Server-streaming response path -----------------------

    [Fact]
    public async Task Should_emit_Next_frames_then_Complete_for_streaming_handler()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);

        var payloads = new[]
        {
            JsonSerializer.SerializeToElement(new { seq = 1 }),
            JsonSerializer.SerializeToElement(new { seq = 2 }),
            JsonSerializer.SerializeToElement(new { seq = 3 }),
        };

        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) => ValueTask.FromResult<RpcHandlerResult>(
                new StreamingHandlerResult(AsyncEnumerableConverter.FromEnumerable(payloads))),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Act
        await JsonRpcTestClient.WriteRequestAsync(
            clientCodec, id: 42, method: "Test.Stream", TestContext.Current.CancellationToken);
        var frame1 = await JsonRpcTestClient.ReadStreamFrameAsync(clientCodec, TestContext.Current.CancellationToken);
        var frame2 = await JsonRpcTestClient.ReadStreamFrameAsync(clientCodec, TestContext.Current.CancellationToken);
        var frame3 = await JsonRpcTestClient.ReadStreamFrameAsync(clientCodec, TestContext.Current.CancellationToken);
        var terminator = await JsonRpcTestClient.ReadStreamFrameAsync(clientCodec, TestContext.Current.CancellationToken);
        var result = await processorTask;

        // Assert
        Assert.Multiple(
            () => Assert.True(result),
            () => Assert.IsType<JsonRpcStreamNext>(frame1),
            () => Assert.IsType<JsonRpcStreamNext>(frame2),
            () => Assert.IsType<JsonRpcStreamNext>(frame3),
            () => Assert.IsType<JsonRpcStreamComplete>(terminator),
            // id-correlator invariant: every frame echoes the
            // request id verbatim.
            () => Assert.Equal(42, frame1.Id.GetInt32()),
            () => Assert.Equal(42, frame2.Id.GetInt32()),
            () => Assert.Equal(42, frame3.Id.GetInt32()),
            () => Assert.Equal(42, terminator.Id.GetInt32()),
            () => Assert.Equal(1, ((JsonRpcStreamNext)frame1).Result.GetProperty("seq").GetInt32()),
            () => Assert.Equal(2, ((JsonRpcStreamNext)frame2).Result.GetProperty("seq").GetInt32()),
            () => Assert.Equal(3, ((JsonRpcStreamNext)frame3).Result.GetProperty("seq").GetInt32()));
    }

    [Fact]
    public async Task Should_emit_terminal_Error_frame_when_streaming_handler_throws_midstream()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);

        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) => ValueTask.FromResult<RpcHandlerResult>(
                new StreamingHandlerResult(ThrowAfterOne())),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Act
        await JsonRpcTestClient.WriteRequestAsync(
            clientCodec, id: 7, method: "Test.StreamFault", TestContext.Current.CancellationToken);
        var first = await JsonRpcTestClient.ReadStreamFrameAsync(clientCodec, TestContext.Current.CancellationToken);
        var terminator = await JsonRpcTestClient.ReadStreamFrameAsync(clientCodec, TestContext.Current.CancellationToken);
        var result = await processorTask;

        // Assert
        var errorFrame = Assert.IsType<JsonRpcStreamError>(terminator);
        Assert.Multiple(
            () => Assert.False(result),
            () => Assert.IsType<JsonRpcStreamNext>(first),
            () => Assert.Equal(7, first.Id.GetInt32()),
            () => Assert.Equal(7, errorFrame.Id.GetInt32()),
            () => Assert.Equal(JsonRpcErrorCodes.InternalError, errorFrame.Error.Code));

        static async IAsyncEnumerable<JsonElement> ThrowAfterOne()
        {
            yield return JsonSerializer.SerializeToElement(new { seq = 1 });
            await Task.Yield();
            throw new InvalidOperationException("midstream boom");
        }
    }

    [Fact]
    public async Task Should_return_false_when_cancellation_fires_midstream()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        var firstYielded = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) => ValueTask.FromResult<RpcHandlerResult>(
                new StreamingHandlerResult(YieldThenBlock(firstYielded, cts.Token))),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, cts.Token);

        // Act
        await JsonRpcTestClient.WriteRequestAsync(
            clientCodec, id: 13, method: "Test.StreamCancel", TestContext.Current.CancellationToken);
        var first = await JsonRpcTestClient.ReadStreamFrameAsync(clientCodec, TestContext.Current.CancellationToken);
        await firstYielded.Task.WaitAsync(
            TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        var result = await processorTask;

        // Assert — cancellation tears the stream down without a
        // terminal frame; first frame already reached the client.
        Assert.Multiple(
            () => Assert.False(result),
            () => Assert.IsType<JsonRpcStreamNext>(first),
            () => Assert.Equal(13, first.Id.GetInt32()));

        static async IAsyncEnumerable<JsonElement> YieldThenBlock(
            TaskCompletionSource gate,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            yield return JsonSerializer.SerializeToElement(new { seq = 1 });
            gate.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
            yield return JsonSerializer.SerializeToElement(new { seq = 2 });
        }
    }

    [Fact]
    public async Task Should_invoke_PostFlush_after_streaming_Complete()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);

        var postFlushInvoked = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) => ValueTask.FromResult<RpcHandlerResult>(
                new StreamingHandlerResult(
                    AsyncEnumerableConverter.FromEnumerable([JsonSerializer.SerializeToElement(new { seq = 1 })]),
                    PostFlush: () =>
                    {
                        postFlushInvoked.TrySetResult();
                        return Task.CompletedTask;
                    })),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Act
        await JsonRpcTestClient.WriteRequestAsync(
            clientCodec, id: 21, method: "Test.StreamPostFlush", TestContext.Current.CancellationToken);
        _ = await JsonRpcTestClient.ReadStreamFrameAsync(clientCodec, TestContext.Current.CancellationToken);
        _ = await JsonRpcTestClient.ReadStreamFrameAsync(clientCodec, TestContext.Current.CancellationToken);
        await postFlushInvoked.Task.WaitAsync(
            TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var result = await processorTask;

        // Assert
        Assert.Multiple(
            () => Assert.True(result),
            () => Assert.True(postFlushInvoked.Task.IsCompletedSuccessfully));
    }

    [Fact]
    public async Task Should_invoke_PostFlush_when_streaming_cancellation_fires_midstream()
    {
        // Arrange
        var (clientStream, serverStream) = FakeDuplexStreamFactory.Create();
        await using var clientGuard = clientStream;
        await using var serverGuard = serverStream;
        var clientCodec = new LengthPrefixedFrameCodec(clientStream);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        var firstYielded = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var postFlushInvoked = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var policy = new FakeRpcConnectionPolicy
        {
            OnInvoke = (_, _) => ValueTask.FromResult<RpcHandlerResult>(
                new StreamingHandlerResult(
                    YieldThenBlock(firstYielded, cts.Token),
                    PostFlush: () =>
                    {
                        postFlushInvoked.TrySetResult();
                        return Task.CompletedTask;
                    })),
        };

        var processorTask = RpcConnectionProcessor.RunAsync(
            serverStream, policy, NullLogger.Instance, cts.Token);

        // Act
        await JsonRpcTestClient.WriteRequestAsync(
            clientCodec, id: 31, method: "Test.StreamCancelPostFlush",
            TestContext.Current.CancellationToken);
        _ = await JsonRpcTestClient.ReadStreamFrameAsync(clientCodec, TestContext.Current.CancellationToken);
        await firstYielded.Task.WaitAsync(
            TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        var result = await processorTask;

        // Assert — PostFlush still runs (in finally), even though
        // cancellation tore the stream down before completion.
        await postFlushInvoked.Task.WaitAsync(
            TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.False(result),
            () => Assert.True(postFlushInvoked.Task.IsCompletedSuccessfully));

        static async IAsyncEnumerable<JsonElement> YieldThenBlock(
            TaskCompletionSource gate,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            yield return JsonSerializer.SerializeToElement(new { seq = 1 });
            gate.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
            yield return JsonSerializer.SerializeToElement(new { seq = 2 });
        }
    }
}
