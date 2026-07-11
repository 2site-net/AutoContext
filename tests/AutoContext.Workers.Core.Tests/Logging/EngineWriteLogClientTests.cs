namespace AutoContext.Workers.Core.Tests.Logging;

using AutoContext.Engine.Protocol;
using AutoContext.Framework.Tests.Support.Pipes;
using AutoContext.Workers.Core.Logging;
using AutoContext.Workers.Core.Tests.Support.Logging;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class EngineWriteLogClientTests
{
    [Fact]
    public async Task Should_handshake_then_deliver_the_record_as_a_notification()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = PipeTestServer.UniqueName("actx-writelog");
        await using var engine = new FakeEngineRpcServer(pipeName);
        engine.Start();
        await using var client = new EngineWriteLogClient(pipeName, NullLogger<EngineWriteLogClient>.Instance);
        var record = JsonLogRecordFakeData.CreateRecord(category: "worker.dotnet.Sample", message: "hi engine");

        // Act
        var sent = await client.TrySendAsync(record, cancellationToken);

        // Assert
        var received = await engine.WaitForRecordsAsync(1, cancellationToken);
        var single = Assert.Single(received);
        Assert.Multiple(
            () => Assert.True(sent),
            () => Assert.Equal("hi engine", single.Message),
            () => Assert.Equal("worker.dotnet.Sample", single.Category));
    }

    [Fact]
    public async Task Should_return_false_when_the_engine_address_is_empty()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var client = new EngineWriteLogClient(string.Empty, NullLogger<EngineWriteLogClient>.Instance);

        // Act
        var sent = await client.TrySendAsync(JsonLogRecordFakeData.CreateRecord(), cancellationToken);

        // Assert
        Assert.False(sent);
    }

    [Fact]
    public async Task Should_return_false_without_throwing_when_the_engine_address_is_unusable()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var client = new EngineWriteLogClient("   ", NullLogger<EngineWriteLogClient>.Instance);

        // Act
        var sent = await client.TrySendAsync(JsonLogRecordFakeData.CreateRecord(), cancellationToken);

        // Assert
        Assert.False(sent);
    }

    [Fact]
    public async Task Should_return_false_when_the_handshake_reports_a_version_mismatch()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = PipeTestServer.UniqueName("actx-writelog");
        await using var engine = new FakeEngineRpcServer(pipeName, handshakeProtocolVersion: ProtocolVersion.Current + 1);
        engine.Start();
        await using var client = new EngineWriteLogClient(pipeName, NullLogger<EngineWriteLogClient>.Instance);

        // Act
        var sent = await client.TrySendAsync(JsonLogRecordFakeData.CreateRecord(), cancellationToken);

        // Assert
        Assert.False(sent);
    }

    [Fact]
    public void Should_throw_when_constructed_with_a_null_address()
        => Assert.Throws<ArgumentNullException>(
            () => new EngineWriteLogClient(null!, NullLogger<EngineWriteLogClient>.Instance));

    [Fact]
    public void Should_throw_when_constructed_with_a_null_logger()
        => Assert.Throws<ArgumentNullException>(
            () => new EngineWriteLogClient("some-address", null!));
}
