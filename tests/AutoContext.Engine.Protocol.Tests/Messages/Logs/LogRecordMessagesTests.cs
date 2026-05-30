namespace AutoContext.Engine.Protocol.Tests.Messages.Logs;

using System.Collections.Generic;
using System.Text.Json;

using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;

public sealed class LogRecordMessagesTests
{
    [Fact]
    public void Should_expose_lowercase_level_constants_matching_design()
    {
        Assert.Multiple(
            () => Assert.Equal("trace", LogLevels.Trace),
            () => Assert.Equal("debug", LogLevels.Debug),
            () => Assert.Equal("information", LogLevels.Information),
            () => Assert.Equal("warning", LogLevels.Warning),
            () => Assert.Equal("error", LogLevels.Error),
            () => Assert.Equal("critical", LogLevels.Critical));
    }

    [Fact]
    public void Should_serialize_minimal_record_with_camelCase_and_omit_optionals()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero);
        var record = new JsonLogRecord
        {
            Timestamp = timestamp,
            Category = "engine.lifecycle",
            Level = LogLevels.Information,
            Message = "engine started",
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            record, ProtocolJsonContext.Default.JsonLogRecord);

        // Assert
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        Assert.Multiple(
            () => Assert.Equal(timestamp, root.GetProperty("timestamp").GetDateTimeOffset()),
            () => Assert.Equal("engine.lifecycle", root.GetProperty("category").GetString()),
            () => Assert.Equal("information", root.GetProperty("level").GetString()),
            () => Assert.Equal("engine started", root.GetProperty("message").GetString()),
            () => Assert.False(root.TryGetProperty("eventId", out _)),
            () => Assert.False(root.TryGetProperty("properties", out _)),
            () => Assert.False(root.TryGetProperty("exception", out _)));
    }

    [Fact]
    public void Should_round_trip_full_record_with_eventId_properties_and_nested_exception()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero);
        var properties = new Dictionary<string, JsonElement>
        {
            ["requestId"] = JsonSerializer.SerializeToElement("abc-123"),
            ["attempt"] = JsonSerializer.SerializeToElement(2),
        };
        var inner = new JsonLogExceptionInfo
        {
            Type = "System.IO.FileNotFoundException",
            Message = "could not find 'foo.txt'",
            StackTrace = "   at Foo()",
        };
        var outer = new JsonLogExceptionInfo
        {
            Type = "System.IO.IOException",
            Message = "open failed",
            StackTrace = "   at Bar()",
            Inner = inner,
        };
        var record = new JsonLogRecord
        {
            Timestamp = timestamp,
            Category = "engine.rpc.Instructions.Get",
            Level = LogLevels.Error,
            EventId = new JsonLogEventId { Id = 42, Name = "OpenFailed" },
            Message = "open failed",
            Properties = properties,
            Exception = outer,
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            record, ProtocolJsonContext.Default.JsonLogRecord);
        var roundTripped = JsonSerializer.Deserialize(
            bytes, ProtocolJsonContext.Default.JsonLogRecord);

        // Assert
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        Assert.Multiple(
            () => Assert.Equal(42, root.GetProperty("eventId").GetProperty("id").GetInt32()),
            () => Assert.Equal("OpenFailed", root.GetProperty("eventId").GetProperty("name").GetString()),
            () => Assert.Equal("abc-123", root.GetProperty("properties").GetProperty("requestId").GetString()),
            () => Assert.Equal(2, root.GetProperty("properties").GetProperty("attempt").GetInt32()),
            () => Assert.Equal("System.IO.IOException", root.GetProperty("exception").GetProperty("type").GetString()),
            () => Assert.Equal("   at Bar()", root.GetProperty("exception").GetProperty("stackTrace").GetString()),
            () => Assert.Equal(
                "System.IO.FileNotFoundException",
                root.GetProperty("exception").GetProperty("inner").GetProperty("type").GetString()),
            () => Assert.False(
                root.GetProperty("exception").GetProperty("inner").TryGetProperty("inner", out _)),
            () => Assert.NotNull(roundTripped),
            () => Assert.Equal(timestamp, roundTripped!.Timestamp),
            () => Assert.Equal("engine.rpc.Instructions.Get", roundTripped!.Category),
            () => Assert.Equal(LogLevels.Error, roundTripped!.Level),
            () => Assert.Equal(42, roundTripped!.EventId!.Id),
            () => Assert.Equal("OpenFailed", roundTripped!.EventId!.Name),
            () => Assert.Equal("open failed", roundTripped!.Message),
            () => Assert.NotNull(roundTripped!.Properties),
            () => Assert.Equal("abc-123", roundTripped!.Properties!["requestId"].GetString()),
            () => Assert.Equal(2, roundTripped!.Properties!["attempt"].GetInt32()),
            () => Assert.NotNull(roundTripped!.Exception),
            () => Assert.Equal("System.IO.IOException", roundTripped!.Exception!.Type),
            () => Assert.NotNull(roundTripped!.Exception!.Inner),
            () => Assert.Equal("System.IO.FileNotFoundException", roundTripped!.Exception!.Inner!.Type),
            () => Assert.Null(roundTripped!.Exception!.Inner!.Inner));
    }

    [Fact]
    public void Should_omit_optional_inner_exception_when_absent()
    {
        // Arrange
        var ex = new JsonLogExceptionInfo
        {
            Type = "System.InvalidOperationException",
            Message = "bad state",
            StackTrace = "   at Baz()",
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            ex, ProtocolJsonContext.Default.JsonLogExceptionInfo);

        // Assert
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        Assert.Multiple(
            () => Assert.Equal("System.InvalidOperationException", root.GetProperty("type").GetString()),
            () => Assert.Equal("bad state", root.GetProperty("message").GetString()),
            () => Assert.Equal("   at Baz()", root.GetProperty("stackTrace").GetString()),
            () => Assert.False(root.TryGetProperty("inner", out _)));
    }

    [Fact]
    public void Should_omit_optional_name_on_event_id_when_absent()
    {
        // Arrange
        var eventId = new JsonLogEventId { Id = 7 };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            eventId, ProtocolJsonContext.Default.JsonLogEventId);

        // Assert
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        Assert.Multiple(
            () => Assert.Equal(7, root.GetProperty("id").GetInt32()),
            () => Assert.False(root.TryGetProperty("name", out _)));
    }
}
