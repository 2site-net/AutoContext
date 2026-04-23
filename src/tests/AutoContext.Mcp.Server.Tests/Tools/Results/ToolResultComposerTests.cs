namespace AutoContext.Mcp.Server.Tests.Tools.Results;

using System.Text.Json;

using AutoContext.Mcp.Server.Tools.Results;
using AutoContext.Mcp.Server.Workers.Protocol;

public sealed class ToolEnvelopeComposerTests
{
    [Fact]
    public void Should_roll_up_to_ok_when_every_task_succeeded()
    {
        // Arrange
        var entries = new[]
        {
            Input(OkResponse("task_a", JsonElementFrom(@"{""hits"":1}")), elapsedMs: 12),
            Input(OkResponse("task_b", JsonElementFrom(@"{""hits"":2}")), elapsedMs: 25),
        };

        // Act
        var envelope = ToolResultComposer.Compose("analyze_csharp_code", entries, elapsedMs: 40);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("analyze_csharp_code", envelope.Tool),
            () => Assert.Equal(ToolResultEnvelope.StatusOk, envelope.Status),
            () => Assert.Equal(2, envelope.Summary.TaskCount),
            () => Assert.Equal(2, envelope.Summary.SuccessCount),
            () => Assert.Equal(0, envelope.Summary.FailureCount),
            () => Assert.Equal(40, envelope.Summary.ElapsedMs),
            () => Assert.Equal(2, envelope.Result.Count),
            () => Assert.Empty(envelope.Errors));
    }

    [Fact]
    public void Should_roll_up_to_error_when_every_task_failed()
    {
        // Arrange
        var entries = new[]
        {
            Input(ErrorResponse("task_a", "boom"), elapsedMs: 10),
            Input(ErrorResponse("task_b", "kaboom"), elapsedMs: 11),
        };

        // Act
        var envelope = ToolResultComposer.Compose("analyze_csharp_code", entries, elapsedMs: 22);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusError, envelope.Status),
            () => Assert.Equal(0, envelope.Summary.SuccessCount),
            () => Assert.Equal(2, envelope.Summary.FailureCount),
            () => Assert.All(envelope.Result, entry => Assert.Null(entry.Output)),
            () => Assert.All(envelope.Result, entry => Assert.NotEmpty(entry.Error)));
    }

    [Fact]
    public void Should_roll_up_to_partial_when_one_task_failed()
    {
        // Arrange
        var entries = new[]
        {
            Input(OkResponse("task_a", JsonElementFrom(@"{""hits"":1}")), elapsedMs: 10),
            Input(ErrorResponse("task_b", "boom"), elapsedMs: 11),
        };

        // Act
        var envelope = ToolResultComposer.Compose("analyze_csharp_code", entries, elapsedMs: 22);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusPartial, envelope.Status),
            () => Assert.Equal(1, envelope.Summary.SuccessCount),
            () => Assert.Equal(1, envelope.Summary.FailureCount));
    }

    [Fact]
    public void Should_normalize_per_task_entry_invariants()
    {
        // Arrange
        var okWithStrayError = new TaskResponse
        {
            McpTask = "task_ok",
            Status = TaskResponse.StatusOk,
            Output = JsonElementFrom(@"{""hits"":1}"),
            Error = "should be dropped",
        };
        var errorWithStrayOutput = new TaskResponse
        {
            McpTask = "task_err",
            Status = TaskResponse.StatusError,
            Output = JsonElementFrom(@"{""hits"":99}"),
            Error = "real error",
        };

        // Act
        var envelope = ToolResultComposer.Compose(
            "tool",
            [Input(okWithStrayError, elapsedMs: 1), Input(errorWithStrayOutput, elapsedMs: 2)],
            elapsedMs: 3);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusOk, envelope.Result[0].Status),
            () => Assert.NotNull(envelope.Result[0].Output),
            () => Assert.Equal(string.Empty, envelope.Result[0].Error),
            () => Assert.Equal(ToolResultEnvelope.StatusError, envelope.Result[1].Status),
            () => Assert.Null(envelope.Result[1].Output),
            () => Assert.Equal("real error", envelope.Result[1].Error));
    }

    [Fact]
    public void Should_propagate_per_task_elapsed_ms()
    {
        // Act
        var envelope = ToolResultComposer.Compose(
            "tool",
            [Input(OkResponse("task_a", JsonElementFrom("null")), elapsedMs: 7)],
            elapsedMs: 9);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(7, envelope.Result[0].ElapsedMs),
            () => Assert.Equal(9, envelope.Summary.ElapsedMs));
    }

    [Fact]
    public void Should_carry_envelope_level_errors_with_empty_result_on_failure()
    {
        // Arrange
        var errors = new[]
        {
            new ToolResultError { Code = ToolResultErrorCodes.PipeFailure, Message = "boom" },
        };

        // Act
        var envelope = ToolResultComposer.ComposeFailure("tool", errors, elapsedMs: 5);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusError, envelope.Status),
            () => Assert.Empty(envelope.Result),
            () => Assert.Single(envelope.Errors),
            () => Assert.Equal(ToolResultErrorCodes.PipeFailure, envelope.Errors[0].Code),
            () => Assert.Equal(0, envelope.Summary.TaskCount));
    }

    [Fact]
    public void Should_reject_empty_errors_list_on_failure()
    {
        Assert.Throws<ArgumentException>(
            () => ToolResultComposer.ComposeFailure("tool", [], elapsedMs: 1));
    }

    [Fact]
    public void Should_serialize_to_canonical_camel_case_json()
    {
        // Arrange
        var envelope = ToolResultComposer.Compose(
            "analyze_csharp_code",
            [Input(OkResponse("task_a", JsonElementFrom(@"{""ok"":true}")), elapsedMs: 10)],
            elapsedMs: 12);

        // Act
        var json = JsonSerializer.SerializeToElement(envelope);

        // Assert
        Assert.Multiple(
            () => Assert.True(json.TryGetProperty("tool", out _)),
            () => Assert.True(json.TryGetProperty("status", out _)),
            () => Assert.True(json.GetProperty("summary").TryGetProperty("taskCount", out _)),
            () => Assert.True(json.GetProperty("summary").TryGetProperty("successCount", out _)),
            () => Assert.True(json.GetProperty("summary").TryGetProperty("failureCount", out _)),
            () => Assert.True(json.GetProperty("summary").TryGetProperty("elapsedMs", out _)),
            () => Assert.True(json.GetProperty("result")[0].TryGetProperty("task", out _)),
            () => Assert.True(json.GetProperty("result")[0].TryGetProperty("elapsedMs", out _)),
            () => Assert.True(json.TryGetProperty("errors", out _)));
    }

    [Fact]
    public void Should_throw_for_negative_elapsed_ms()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ToolResultComposer.Compose("tool", [], elapsedMs: -1));
    }

    [Fact]
    public void Should_roll_up_to_error_when_entries_is_empty()
    {
        // Act
        var envelope = ToolResultComposer.Compose("tool", [], elapsedMs: 0);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusError, envelope.Status),
            () => Assert.Equal(0, envelope.Summary.TaskCount),
            () => Assert.Equal(0, envelope.Summary.SuccessCount),
            () => Assert.Equal(0, envelope.Summary.FailureCount),
            () => Assert.Empty(envelope.Result),
            () => Assert.Empty(envelope.Errors));
    }

    private static ToolResultComposerInput Input(TaskResponse response, int elapsedMs) =>
        new() { Response = response, ElapsedMs = elapsedMs };

    private static TaskResponse OkResponse(string name, JsonElement output) => new()
    {
        McpTask = name,
        Status = TaskResponse.StatusOk,
        Output = output,
        Error = string.Empty,
    };

    private static TaskResponse ErrorResponse(string name, string error) => new()
    {
        McpTask = name,
        Status = TaskResponse.StatusError,
        Output = null,
        Error = error,
    };

    private static JsonElement JsonElementFrom(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);
}
