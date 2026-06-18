namespace AutoContext.Engine.Core.Features.McpTools;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;

/// <summary>
/// Read-only seam over the in-memory MCP-tools registry snapshot.
/// Decouples snapshot readers — the <c>McpTools.*</c> RPC handlers —
/// from the stateful <see cref="McpToolsRegistryService"/> so they
/// depend only on the ability to read the current registry, not on its
/// hosted-service lifecycle.
/// </summary>
internal interface IMcpToolsRegistryAccessor
{
    /// <summary>
    /// The registry snapshot currently held in memory. Each read returns
    /// an immutable value that is safe to use without locking. Before the
    /// startup load completes this is <see cref="McpToolsRegistry.Empty"/>.
    /// </summary>
    McpToolsRegistry Current { get; }
}
