namespace AutoContext.Mcp.Tools.Envelope;

using AutoContext.Mcp.Tools.Wire;

/// <summary>
/// Composes per-task wire responses into the uniform tool-result envelope.
/// Pure logic — no IO. Status rollup is deterministic and matches the
/// table in <c>docs/architecture-centralized-mcp.md</c>.
/// </summary>
public static class ToolEnvelopeComposer
{
    /// <summary>
    /// Composes a successful-dispatch envelope from per-task wire responses.
    /// Status is "ok" if every task succeeded, "error" if every task failed,
    /// "partial" otherwise.
    /// </summary>
    public static ToolResultEnvelope Compose(
        string tool,
        IReadOnlyList<ToolEnvelopeComposerInput> entries,
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

        var resultEntries = new ToolResultEntry[entries.Count];
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
                TaskWireResponse.StatusOk,
                StringComparison.Ordinal);

            if (ok)
            {
                successCount++;
            }

            resultEntries[i] = new ToolResultEntry
            {
                Task = input.Response.McpTask,
                Status = ok ? ToolResultEnvelope.StatusOk : ToolResultEnvelope.StatusError,
                ElapsedMs = input.ElapsedMs,
                Output = ok ? input.Response.Output : null,
                Error = ok ? string.Empty : input.Response.Error,
            };
        }

        var failureCount = entries.Count - successCount;

        return new ToolResultEnvelope
        {
            Tool = tool,
            Status = RollUp(entries.Count, successCount, failureCount),
            Summary = new ToolResultSummary
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
    /// <see cref="ToolResultEnvelope.Result"/> is empty;
    /// <see cref="ToolResultEnvelope.Errors"/> carries the supplied codes.
    /// </summary>
    public static ToolResultEnvelope ComposeFailure(
        string tool,
        IReadOnlyList<ToolResultError> errors,
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

        return new ToolResultEnvelope
        {
            Tool = tool,
            Status = ToolResultEnvelope.StatusError,
            Summary = new ToolResultSummary
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
            return ToolResultEnvelope.StatusError;
        }

        if (failureCount == 0)
        {
            return ToolResultEnvelope.StatusOk;
        }

        if (successCount == 0)
        {
            return ToolResultEnvelope.StatusError;
        }

        return ToolResultEnvelope.StatusPartial;
    }
}
