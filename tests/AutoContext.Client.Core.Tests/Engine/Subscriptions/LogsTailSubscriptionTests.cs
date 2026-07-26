namespace AutoContext.Client.Core.Tests.Engine.Subscriptions;

using AutoContext.Client.Core.Engine.Subscriptions;
using AutoContext.Client.Core.Tests.Support.Engine.Subscriptions;
using AutoContext.Client.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;

public sealed class LogsTailSubscriptionTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connector()
        => Assert.Throws<ArgumentNullException>(() => new LogsTailSubscription(connector: null!));

    [Fact]
    public async Task Should_yield_each_record()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = SubscriptionTestContext.ForRpc(async (peer, ct) =>
        {
            var subscribe = await peer.ReadRequestAsync(ct);
            var frame = JsonElementTestFactory.FromValue(
                new JsonLogRecordFrame(new JsonLogRecord
                {
                    Timestamp = DateTimeOffset.UnixEpoch,
                    Category = "engine.rpc",
                    Level = LogLevels.Information,
                    Message = "hello",
                }),
                ProtocolJsonContext.Default.JsonLogStreamFrame);
            await peer.WriteStreamNextAsync(subscribe.Id, frame, ct);
        });
        var subscription = new LogsTailSubscription(context.Connector);
        var received = new List<JsonLogRecord>();

        // Act
        await foreach (var record in subscription.SubscribeAsync(cancellationToken))
        {
            received.Add(record);
            break;
        }

        // Assert
        Assert.Multiple(
            () => Assert.Single(received),
            () => Assert.Equal("hello", received[0].Message));
    }
}
