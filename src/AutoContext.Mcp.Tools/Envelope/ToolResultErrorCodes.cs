namespace AutoContext.Mcp.Tools.Envelope;

/// <summary>
/// Fixed vocabulary for <see cref="ToolResultError.Code"/>. Mirrors the
/// table in <c>docs/architecture-centralized-mcp.md</c>.
/// </summary>
public static class ToolResultErrorCodes
{
    /// <summary>Input <c>data</c> failed JSON Schema validation against the tool's <c>parameters</c>.</summary>
    public const string SchemaValidation = "schemaValidation";

    /// <summary>Manifest could not be loaded or validated.</summary>
    public const string ManifestError = "manifestError";

    /// <summary>Pipe connect / write / read failed before any task ran.</summary>
    public const string PipeFailure = "pipeFailure";

    /// <summary>Task name in the tool's <c>tasks[]</c> not declared (caught at startup, not runtime).</summary>
    public const string TaskNotFound = "taskNotFound";

    /// <summary>Every task failed; envelope is <c>error</c> for caller convenience.</summary>
    public const string AllTasksFailed = "allTasksFailed";
}
