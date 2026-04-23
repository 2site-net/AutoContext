namespace AutoContext.Mcp.Server.Envelope;

using AutoContext.Mcp.Server.Wire;

/// <summary>
/// Per-task input to <see cref="ToolEnvelopeComposer.Compose"/>:
/// the worker's wire response paired with the elapsed time the client
/// observed for that task.
/// </summary>
public sealed record ToolEnvelopeComposerInput
{
    public required TaskWireResponse Response { get; init; }

    public required int ElapsedMs { get; init; }
}
