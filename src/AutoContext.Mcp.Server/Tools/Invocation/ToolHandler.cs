namespace AutoContext.Mcp.Server.Tools.Invocation;

using System.Text.Json;

/// <summary>
/// The per-tool closure invoked by the MCP SDK. Receives the tool's
/// raw <c>data</c> payload and returns the serialized
/// <see cref="Results.ToolResultEnvelope"/> as a JSON string.
/// </summary>
public delegate Task<string> ToolHandler(JsonElement data, CancellationToken ct);
