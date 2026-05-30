namespace AutoContext.Mcp.Server.Tools.Results;

using AutoContext.Mcp.Server.Workers.Protocol;

/// <summary>
/// Composes per-task worker responses into the uniform tool-result
/// envelope. Pure logic — no IO. Status rollup is deterministic:
/// <c>ok</c> if every task succeeded, <c>error</c> if every task
/// failed, <c>partial</c> otherwise.
/// </summary>
public static class ToolResultComposer
{
    /// <summary>
    /// Composes a result envelope from per-task worker responses. Status
    /// is "ok" if every task succeeded, "error" if every task failed,
    /// "partial" otherwise.
    /// </summary>
    public static JsonToolResultEnvelope Compose(
        string tool,
        IReadOnlyList<ToolResultComposerInput> entries,
        int elapsedMs)
    {
        ArgumentException.ThrowIfNullOrEmpty(tool);
        ArgumentNullException.ThrowIfNull(entries);

        if (elapsedMs < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedMs),
                elapsedMs,
                "Elapsed milliseconds must be non-negative.");
        }

        var resultEntries = new JsonToolResultEntry[entries.Count];
        var successCount = 0;

        for (var i = 0; i < entries.Count; i++)
        {
            var input = entries[i];

            ArgumentNullException.ThrowIfNull(input.Response);

            if (input.ElapsedMs < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(entries),
                    input.ElapsedMs,
                    "Per-task elapsed milliseconds must be non-negative.");
            }

            var ok = string.Equals(
                input.Response.Status,
                JsonTaskResponse.StatusOk,
                StringComparison.Ordinal);

            if (ok)
            {
                successCount++;
            }

            resultEntries[i] = new JsonToolResultEntry
            {
                Task = input.Response.McpTask,
                Status = ok ? JsonToolResultEnvelope.StatusOk : JsonToolResultEnvelope.StatusError,
                ElapsedMs = input.ElapsedMs,
                Output = ok ? input.Response.Output : null,
                Error = ok ? string.Empty : input.Response.Error,
            };
        }

        var failureCount = entries.Count - successCount;

        return new JsonToolResultEnvelope
        {
            Tool = tool,
            Status = RollUp(entries.Count, successCount, failureCount),
            Summary = new JsonToolResultSummary
            {
                TaskCount = entries.Count,
                SuccessCount = successCount,
                FailureCount = failureCount,
                ElapsedMs = elapsedMs,
            },
            Result = resultEntries,
            Errors = [],
        };
    }

    /// <summary>
    /// Composes an envelope-level failure envelope (dispatch never happened).
    /// <see cref="JsonToolResultEnvelope.Result"/> is empty;
    /// <see cref="JsonToolResultEnvelope.Errors"/> carries the supplied codes.
    /// </summary>
    public static JsonToolResultEnvelope ComposeFailure(
        string tool,
        IReadOnlyList<JsonToolResultError> errors,
        int elapsedMs)
    {
        ArgumentException.ThrowIfNullOrEmpty(tool);
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException(
                "Envelope-level failure must carry at least one error.",
                nameof(errors));
        }

        if (elapsedMs < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedMs),
                elapsedMs,
                "Elapsed milliseconds must be non-negative.");
        }

        return new JsonToolResultEnvelope
        {
            Tool = tool,
            Status = JsonToolResultEnvelope.StatusError,
            Summary = new JsonToolResultSummary
            {
                TaskCount = 0,
                SuccessCount = 0,
                FailureCount = 0,
                ElapsedMs = elapsedMs,
            },
            Result = [],
            Errors = errors,
        };
    }

    private static string RollUp(int taskCount, int successCount, int failureCount)
    {
        if (taskCount == 0)
        {
            return JsonToolResultEnvelope.StatusError;
        }

        if (failureCount == 0)
        {
            return JsonToolResultEnvelope.StatusOk;
        }

        if (successCount == 0)
        {
            return JsonToolResultEnvelope.StatusError;
        }

        return JsonToolResultEnvelope.StatusPartial;
    }
}
