namespace AutoContext.Engine.Core.Tests.Logging;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Logging.Primitives;
using AutoContext.Engine.Core.Tests.Support.Shared;

using Microsoft.Extensions.Logging;

public sealed class EngineLoggerProviderTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_channel()
    {
        Assert.Throws<ArgumentNullException>(() => new EngineLoggerProvider(
            channel: null!,
            timeProvider: TimeProvider.System));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_time_provider()
    {
        Assert.Throws<ArgumentNullException>(() => new EngineLoggerProvider(
            channel: new LogChannel(),
            timeProvider: null!));
    }

    [Fact]
    public void Should_return_same_logger_instance_for_same_category()
    {
        // Arrange
        using var provider = new EngineLoggerProvider(new LogChannel(), TimeProvider.System);

        // Act
        var first = provider.CreateLogger("engine.test");
        var second = provider.CreateLogger("engine.test");

        // Assert
        Assert.Same(first, second);
    }

    [Fact]
    public void Should_return_distinct_logger_instances_for_different_categories()
    {
        // Arrange
        using var provider = new EngineLoggerProvider(new LogChannel(), TimeProvider.System);

        // Act
        var lifecycle = provider.CreateLogger("engine.lifecycle");
        var rpc = provider.CreateLogger("engine.rpc");

        // Assert
        Assert.NotSame(lifecycle, rpc);
    }

    [Fact]
    public void Should_treat_null_category_as_empty_string()
    {
        // Arrange — ILoggerFactory does not promise non-null
        // category names, so the provider must not throw.
        using var provider = new EngineLoggerProvider(new LogChannel(), TimeProvider.System);

        // Act
        var fromNull = provider.CreateLogger(null!);
        var fromEmpty = provider.CreateLogger(string.Empty);

        // Assert
        Assert.Same(fromNull, fromEmpty);
    }

    [Fact]
    public async Task Should_not_dispose_underlying_channel_when_provider_is_disposed()
    {
        // Arrange
        var channel = new LogChannel();
        var provider = new EngineLoggerProvider(channel, new FakeTimeProvider(DateTimeOffset.UnixEpoch));
        var logger = provider.CreateLogger("engine.test");

        // Act
        provider.Dispose();
        logger.Log(
            LogLevel.Information,
            new EventId(0),
            state: "after-dispose",
            exception: null,
            formatter: static (s, _) => s);
        channel.Complete();

        // Assert — the record posted after the provider's Dispose
        // still reaches the channel because the provider never
        // owned it; pre-existing loggers stay functional for
        // in-flight callers that captured them before Dispose.
        var drained = new List<string>();
        await foreach (var record in channel.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            drained.Add(record.Message);
        }

        Assert.Single(drained, "after-dispose");
    }

    [Fact]
    public void Should_allow_dispose_to_be_called_more_than_once()
    {
        // Arrange
        var provider = new EngineLoggerProvider(new LogChannel(), TimeProvider.System);

        // Act + Assert — second call must be a no-op, not throw.
        provider.Dispose();
        provider.Dispose();
    }
}
