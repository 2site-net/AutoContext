namespace AutoContext.Engine.Core.Tests.Logging;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support.Logging;
using AutoContext.Engine.Protocol.Messages.Logs;

using Microsoft.Extensions.Logging;

public sealed class EngineLoggerTests
{
    private static readonly DateTimeOffset KnownNow =
        new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_throw_when_constructed_with_null_category()
    {
        Assert.Throws<ArgumentNullException>(() => new EngineLogger(
            category: null!,
            channel: new LogChannel(),
            timeProvider: TimeProvider.System));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_channel()
    {
        Assert.Throws<ArgumentNullException>(() => new EngineLogger(
            category: "engine.test",
            channel: null!,
            timeProvider: TimeProvider.System));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_time_provider()
    {
        Assert.Throws<ArgumentNullException>(() => new EngineLogger(
            category: "engine.test",
            channel: new LogChannel(),
            timeProvider: null!));
    }

    [Fact]
    public void Should_throw_when_log_invoked_with_null_formatter()
    {
        // Arrange
        var (logger, _) = EngineLoggerTestFactory.Create(KnownNow);

        // Act + Assert
        Assert.Throws<ArgumentNullException>(() => logger.Log(
            LogLevel.Information,
            new EventId(0),
            state: "irrelevant",
            exception: null,
            formatter: null!));
    }

    [Fact]
    public void Should_report_IsEnabled_false_only_for_None()
    {
        // Arrange
        var (logger, _) = EngineLoggerTestFactory.Create(KnownNow);

        // Assert
        Assert.Multiple(
            () => Assert.False(logger.IsEnabled(LogLevel.None)),
            () => Assert.True(logger.IsEnabled(LogLevel.Trace)),
            () => Assert.True(logger.IsEnabled(LogLevel.Debug)),
            () => Assert.True(logger.IsEnabled(LogLevel.Information)),
            () => Assert.True(logger.IsEnabled(LogLevel.Warning)),
            () => Assert.True(logger.IsEnabled(LogLevel.Error)),
            () => Assert.True(logger.IsEnabled(LogLevel.Critical)));
    }

    [Fact]
    public async Task Should_not_write_record_for_level_None()
    {
        // Arrange
        var (logger, channel) = EngineLoggerTestFactory.Create(KnownNow);

        // Act
        logger.Log(
            LogLevel.None,
            new EventId(0),
            state: "ignored",
            exception: null,
            formatter: static (s, _) => s);
        channel.Complete();

        // Assert
        var drained = await LogChannelTestDrainer.DrainAsync(channel);
        Assert.Empty(drained);
    }

    [Fact]
    public async Task Should_not_write_record_when_message_is_empty_and_exception_is_null()
    {
        // Arrange
        var (logger, channel) = EngineLoggerTestFactory.Create(KnownNow);

        // Act
        logger.Log(
            LogLevel.Information,
            new EventId(0),
            state: string.Empty,
            exception: null,
            formatter: static (s, _) => s);
        channel.Complete();

        // Assert
        var drained = await LogChannelTestDrainer.DrainAsync(channel);
        Assert.Empty(drained);
    }

    [Fact]
    public async Task Should_write_record_with_category_message_and_timestamp_from_clock()
    {
        // Arrange
        var (logger, channel) = EngineLoggerTestFactory.Create(KnownNow);

        // Act
        logger.Log(
            LogLevel.Information,
            new EventId(0),
            state: "hello world",
            exception: null,
            formatter: static (s, _) => s);
        channel.Complete();

        // Assert
        var drained = await LogChannelTestDrainer.DrainAsync(channel);
        var single = Assert.Single(drained);
        Assert.Multiple(
            () => Assert.Equal("engine.test", single.Category),
            () => Assert.Equal("hello world", single.Message),
            () => Assert.Equal(KnownNow, single.Timestamp),
            () => Assert.Equal(LogLevels.Information, single.Level));
    }

    [Theory]
    [InlineData(LogLevel.Trace, LogLevels.Trace)]
    [InlineData(LogLevel.Debug, LogLevels.Debug)]
    [InlineData(LogLevel.Information, LogLevels.Information)]
    [InlineData(LogLevel.Warning, LogLevels.Warning)]
    [InlineData(LogLevel.Error, LogLevels.Error)]
    [InlineData(LogLevel.Critical, LogLevels.Critical)]
    public async Task Should_map_log_level_to_matching_wire_constant(LogLevel level, string expected)
    {
        // Arrange
        var (logger, channel) = EngineLoggerTestFactory.Create(KnownNow);

        // Act
        logger.Log(
            level,
            new EventId(0),
            state: "msg",
            exception: null,
            formatter: static (s, _) => s);
        channel.Complete();

        // Assert
        var drained = await LogChannelTestDrainer.DrainAsync(channel);
        var single = Assert.Single(drained);
        Assert.Equal(expected, single.Level);
    }

    [Fact]
    public async Task Should_omit_event_id_when_default()
    {
        // Arrange
        var (logger, channel) = EngineLoggerTestFactory.Create(KnownNow);

        // Act — the lower-level Log overload with a
        // default(EventId) is what the framework's LogInformation
        // extension also passes, so the provider must omit the
        // wire field rather than emit { id: 0 }.
        logger.Log(
            LogLevel.Information,
            new EventId(0),
            state: "no event id",
            exception: null,
            formatter: static (s, _) => s);
        channel.Complete();

        // Assert
        var drained = await LogChannelTestDrainer.DrainAsync(channel);
        var single = Assert.Single(drained);
        Assert.Null(single.EventId);
    }

    [Fact]
    public async Task Should_project_numeric_event_id_to_wire_shape()
    {
        // Arrange
        var (logger, channel) = EngineLoggerTestFactory.Create(KnownNow);

        // Act
        logger.Log(
            LogLevel.Information,
            new EventId(42),
            state: "with numeric event id",
            exception: null,
            formatter: static (s, _) => s);
        channel.Complete();

        // Assert
        var drained = await LogChannelTestDrainer.DrainAsync(channel);
        var single = Assert.Single(drained);
        Assert.NotNull(single.EventId);
        Assert.Multiple(
            () => Assert.Equal(42, single.EventId!.Id),
            () => Assert.Null(single.EventId.Name));
    }

    [Fact]
    public async Task Should_project_named_event_id_to_wire_shape()
    {
        // Arrange
        var (logger, channel) = EngineLoggerTestFactory.Create(KnownNow);

        // Act
        logger.Log(
            LogLevel.Information,
            new EventId(7, "WorkspaceMounted"),
            state: "with named event id",
            exception: null,
            formatter: static (s, _) => s);
        channel.Complete();

        // Assert
        var drained = await LogChannelTestDrainer.DrainAsync(channel);
        var single = Assert.Single(drained);
        Assert.NotNull(single.EventId);
        Assert.Multiple(
            () => Assert.Equal(7, single.EventId!.Id),
            () => Assert.Equal("WorkspaceMounted", single.EventId.Name));
    }

    [Fact]
    public async Task Should_flatten_exception_to_wire_shape()
    {
        // Arrange
        var (logger, channel) = EngineLoggerTestFactory.Create(KnownNow);
        Exception thrown;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException ex)
        {
            thrown = ex;
        }

        // Act
        logger.Log(
            LogLevel.Error,
            new EventId(0),
            state: "failed to do thing",
            exception: thrown,
            formatter: static (s, _) => s);
        channel.Complete();

        // Assert
        var drained = await LogChannelTestDrainer.DrainAsync(channel);
        var single = Assert.Single(drained);
        Assert.NotNull(single.Exception);
        Assert.Multiple(
            () => Assert.Equal("System.InvalidOperationException", single.Exception!.Type),
            () => Assert.Equal("boom", single.Exception.Message),
            () => Assert.False(string.IsNullOrEmpty(single.Exception.StackTrace)),
            () => Assert.Null(single.Exception.Inner));
    }

    [Fact]
    public async Task Should_walk_inner_exception_chain_depth_first()
    {
        // Arrange
        var (logger, channel) = EngineLoggerTestFactory.Create(KnownNow);
        var inner = new ArgumentException("inner-msg");
        var outer = new InvalidOperationException("outer-msg", inner);

        // Act
        logger.Log(
            LogLevel.Error,
            new EventId(0),
            state: "wrapped failure",
            exception: outer,
            formatter: static (s, _) => s);
        channel.Complete();

        // Assert
        var drained = await LogChannelTestDrainer.DrainAsync(channel);
        var single = Assert.Single(drained);
        Assert.NotNull(single.Exception);
        Assert.NotNull(single.Exception!.Inner);
        Assert.Multiple(
            () => Assert.Equal("System.InvalidOperationException", single.Exception.Type),
            () => Assert.Equal("System.ArgumentException", single.Exception.Inner!.Type),
            () => Assert.Equal("inner-msg", single.Exception.Inner.Message),
            () => Assert.Null(single.Exception.Inner.Inner));
    }

    [Fact]
    public async Task Should_write_record_when_message_is_empty_but_exception_is_present()
    {
        // Arrange
        var (logger, channel) = EngineLoggerTestFactory.Create(KnownNow);
        var error = new InvalidOperationException("bare");

        // Act
        logger.Log(
            LogLevel.Error,
            new EventId(0),
            state: string.Empty,
            exception: error,
            formatter: static (_, _) => string.Empty);
        channel.Complete();

        // Assert
        var drained = await LogChannelTestDrainer.DrainAsync(channel);
        var single = Assert.Single(drained);
        Assert.NotNull(single.Exception);
        Assert.Equal("bare", single.Exception!.Message);
    }

    [Fact]
    public void Should_return_no_op_disposable_from_BeginScope()
    {
        // Arrange
        var (logger, _) = EngineLoggerTestFactory.Create(KnownNow);

        // Act
        var first = logger.BeginScope("scope-a");
        var second = logger.BeginScope("scope-b");

        // Assert — single shared NullScope across calls; disposing
        // it is harmless.
        Assert.Multiple(
            () => Assert.NotNull(first),
            () => Assert.Same(first, second));
        first!.Dispose();
        second!.Dispose();
    }
}
