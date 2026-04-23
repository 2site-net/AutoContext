namespace AutoContext.Mcp.Server.Tools.Results;

using AutoContext.Mcp.Server.Workers.Protocol;

/// <summary>
/// Per-task input to <see cref="ToolResultComposer.Compose"/>:
/// the worker's wire response paired with the elapsed time the client
/// observed for that task.
/// </summary>
public sealed record ToolResultComposerInput
{
    public required TaskResponse Response { get; init; }

    public required int ElapsedMs { get; init; }
}
