namespace AutoContext.Mcp.Server.EditorConfig;

/// <summary>
/// Result of one batched EditorConfig resolve. Even on resolution failure
/// the per-task slices are present (all empty) so dispatch can proceed —
/// matching the architecture-doc rule that EditorConfig failures degrade
/// gracefully rather than cancel the whole tool invocation.
/// </summary>
public sealed record EditorConfigBatchResult
{
    /// <summary>Per-task EditorConfig dictionaries, keyed by task name.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Slices { get; init; }

    /// <summary>True when the workspace pipe call returned an error response.</summary>
    public required bool ResolutionFailed { get; init; }

    /// <summary>Failure message from the worker, when <see cref="ResolutionFailed"/>.</summary>
    public string? FailureMessage { get; init; }
}
