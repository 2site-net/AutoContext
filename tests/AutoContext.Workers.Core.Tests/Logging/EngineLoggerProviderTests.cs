namespace AutoContext.Workers.Core.Tests.Logging;

using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Framework.Tests.Support.Pipes;
using AutoContext.Workers.Core.Logging;
using AutoContext.Workers.Core.Tests.Support.Logging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class EngineLoggerProviderTests
{
    [Fact]
    public async Task Should_stamp_the_worker_category_and_deliver_through_the_ring()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = PipeTestServer.UniqueName("actx-provider");
        await using var engine = new FakeEngineRpcServer(pipeName);
        engine.Start();
        await using var client = new EngineWriteLogClient(pipeName, NullLogger<EngineWriteLogClient>.Instance);
        await using var ring = new EngineLogIngestRing(client, "dotnet", TimeProvider.System);
        using var provider = new EngineLoggerProvider("dotnet", ring, TimeProvider.System);
        var logger = provider.CreateLogger("Sample.Category");

        // Act
        logger.Log(LogLevel.Information, default, "hello from provider", null, static (state, _) => state);

        // Assert
        var received = await engine.WaitForRecordsAsync(1, cancellationToken);
        var single = Assert.Single(received);
        Assert.Multiple(
            () => Assert.Equal("worker.dotnet.Sample.Category", single.Category),
            () => Assert.Equal("hello from provider", single.Message),
            () => Assert.Equal(LogLevels.Information, single.Level));
    }

    [Fact]
    public async Task Should_throw_when_constructed_with_a_whitespace_worker_id()
    {
        await using var client = new EngineWriteLogClient(string.Empty, NullLogger<EngineWriteLogClient>.Instance);
        await using var ring = new EngineLogIngestRing(client, "dotnet", TimeProvider.System);

        Assert.Throws<ArgumentException>(
            () => new EngineLoggerProvider("  ", ring, TimeProvider.System));
    }
}
