namespace AutoContext.Workers.Core.Tests.Logging;

using System.Linq;

using AutoContext.Framework.Tests.Support.Pipes;
using AutoContext.Workers.Core.Logging;
using AutoContext.Workers.Core.Tests.Support.Logging;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class EngineLogIngestRingTests
{
    [Fact]
    public async Task Should_buffer_records_while_the_engine_is_unavailable_then_drain_them_in_order()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = PipeTestServer.UniqueName("actx-ring");
        await using var client = new EngineWriteLogClient(pipeName, NullLogger<EngineWriteLogClient>.Instance);
        await using var ring = new EngineLogIngestRing(client, "dotnet", TimeProvider.System);

        // Act
        ring.Post(JsonLogRecordFakeData.CreateRecord(message: "first"));
        ring.Post(JsonLogRecordFakeData.CreateRecord(message: "second"));
        ring.Post(JsonLogRecordFakeData.CreateRecord(message: "third"));

        await using var engine = new FakeEngineRpcServer(pipeName);
        engine.Start();

        // Assert
        var received = await engine.WaitForRecordsAsync(3, cancellationToken);
        string[] expected = ["first", "second", "third"];
        Assert.Equal(expected, received.Select(record => record.Message).ToArray());
    }

    [Fact]
    public async Task Should_drop_oldest_and_report_the_batch_when_overflowing()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = PipeTestServer.UniqueName("actx-ring");
        var standardError = new StringWriter();
        await using var client = new EngineWriteLogClient(pipeName, NullLogger<EngineWriteLogClient>.Instance);
        var ring = new EngineLogIngestRing(
            client, "dotnet", TimeProvider.System, capacity: 2, maxBytes: 1_000_000, standardError: standardError);

        // Act
        for (var i = 1; i <= 6; i++)
        {
            ring.Post(JsonLogRecordFakeData.CreateRecord(message: $"m{i}"));
        }

        await using var engine = new FakeEngineRpcServer(pipeName);
        engine.Start();

        var received = await engine.WaitUntilAsync(record => record.Message == "m6", cancellationToken);
        await ring.DisposeAsync();

        // Assert
        Assert.Multiple(
            () => Assert.Contains(received, record => record.Category == "worker.dotnet.engine.logging"),
            () => Assert.Contains(received, record => record.Message.Contains("worker log records", StringComparison.Ordinal)),
            () => Assert.Contains("engine log dropped", standardError.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_deliver_a_record_once_the_engine_becomes_reachable()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = PipeTestServer.UniqueName("actx-ring");
        await using var engine = new FakeEngineRpcServer(pipeName);
        engine.Start();
        await using var client = new EngineWriteLogClient(pipeName, NullLogger<EngineWriteLogClient>.Instance);
        await using var ring = new EngineLogIngestRing(client, "web", TimeProvider.System);

        // Act
        ring.Post(JsonLogRecordFakeData.CreateRecord(message: "single"));

        // Assert
        var received = await engine.WaitForRecordsAsync(1, cancellationToken);
        Assert.Equal("single", Assert.Single(received).Message);
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_a_whitespace_worker_id()
    {
        await using var client = new EngineWriteLogClient(string.Empty, NullLogger<EngineWriteLogClient>.Instance);

        Assert.Throws<ArgumentException>(
            () => new EngineLogIngestRing(client, "  ", TimeProvider.System));
    }
}
