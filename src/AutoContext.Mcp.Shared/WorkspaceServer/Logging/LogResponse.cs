namespace AutoContext.Mcp.Shared.WorkspaceServer.Logging;

/// <summary>
/// Acknowledgement response for a log request. Contains no data — the
/// protocol requires a response to complete the request/response cycle.
/// </summary>
internal sealed record LogResponse;
