namespace AutoContext.Engine.Core.Tests.Rpc.Handlers;

using System.Text.Json;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Rpc;
using AutoContext.Engine.Core.Rpc.Handlers;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Tests.Support.Logging;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class WriteLogRpcHandlerTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_channel()
    {
        Assert.Throws<ArgumentNullException>(() => new WriteLogRpcHandler(
            channel: null!,
            logger: NullLogger<WriteLogRpcHandler>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        Assert.Throws<ArgumentNullException>(() => new WriteLogRpcHandler(
            channel: new LogChannel(),
            logger: null!));
    }

    [Fact]
    public void Should_serve_only_the_Engine_WriteLog_method()
    {
        var handler = new WriteLogRpcHandler(new LogChannel(), NullLogger<WriteLogRpcHandler>.Instance);

        var method = Assert.Single(handler.Methods);
        Assert.Equal(ProtocolMethods.WriteLog, method);
    }

    [Fact]
    public async Task Should_enqueue_record_and_return_a_notification_result()
    {
        // Arrange
        var channel = new LogChannel();
        var handler = new WriteLogRpcHandler(channel, NullLogger<WriteLogRpcHandler>.Instance);
        var record = LogRecordFakeData.CreateLogRecord(
            category: "worker.dotnet.RoslynAnalyzer", message: "hello from worker");
        var request = JsonRpcRequestTestFactory.BuildRequest(
            ProtocolMethods.WriteLog, record, ProtocolJsonContext.Default.JsonLogRecord);

        // Act
        var result = await handler.InvokeAsync(request, TestContext.Current.CancellationToken);

        // Assert — the record reached the channel and the handler
        // answered with a no-response notification outcome that
        // keeps the connection serving.
        var records = await DrainAsync(channel);
        var notification = Assert.IsType<NotificationHandlerResult>(result);
        var single = Assert.Single(records);
        Assert.Multiple(
            () => Assert.Equal(Continuation.Continue, notification.Continuation),
            () => Assert.Null(notification.PostFlush),
            () => Assert.Equal("worker.dotnet.RoslynAnalyzer", single.Category),
            () => Assert.Equal("hello from worker", single.Message));
    }

    [Fact]
    public async Task Should_drop_record_and_return_a_notification_when_params_are_malformed()
    {
        // Arrange — params is a JSON string, not the record object
        // shape. A notification carries no id, so the handler must
        // not reply with an error; it drops the record and keeps
        // serving.
        var channel = new LogChannel();
        var handler = new WriteLogRpcHandler(channel, NullLogger<WriteLogRpcHandler>.Instance);
        var request = new JsonRpcRequest
        {
            Method = ProtocolMethods.WriteLog,
            Params = JsonSerializer.SerializeToElement("not-an-object"),
        };

        // Act
        var result = await handler.InvokeAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var records = await DrainAsync(channel);
        Assert.IsType<NotificationHandlerResult>(result);
        Assert.Empty(records);
    }

    [Fact]
    public async Task Should_drop_record_and_return_a_notification_when_params_are_absent()
    {
        // Arrange
        var channel = new LogChannel();
        var handler = new WriteLogRpcHandler(channel, NullLogger<WriteLogRpcHandler>.Instance);
        var request = JsonRpcRequestTestFactory.BuildRequest(ProtocolMethods.WriteLog);

        // Act
        var result = await handler.InvokeAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var records = await DrainAsync(channel);
        Assert.IsType<NotificationHandlerResult>(result);
        Assert.Empty(records);
    }

    private static async Task<List<JsonLogRecord>> DrainAsync(LogChannel channel)
    {
        channel.Complete();
        var records = new List<JsonLogRecord>();
        await foreach (var record in channel.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            records.Add(record);
        }

        return records;
    }
}
