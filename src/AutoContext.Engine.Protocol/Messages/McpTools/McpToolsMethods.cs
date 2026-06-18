namespace AutoContext.Engine.Protocol.Messages.McpTools;

/// <summary>
/// JSON-RPC method-name constants for the <c>McpTools.*</c> family —
/// the engine's authority over the MCP tool catalog it would advertise
/// to an MCP client, plus the pipe-side counterpart of MCP's
/// <c>tools/call</c>. Grouped here so handlers and transports share one
/// spelling of each dotted method name per <c>design § RPC surface
/// (McpTools.*)</c>.
/// </summary>
public static class McpToolsMethods
{
    /// <summary>
    /// Listing RPC. Surfaces the engine's MCP tool catalog — one
    /// identity <see cref="JsonMcpToolsListRow"/> per tool — for hosts
    /// that want to introspect what the engine would advertise to an
    /// MCP client. Rows are projected from the embedded
    /// <c>mcp-tools-registry.json</c> and carry the engine-resolved
    /// per-tool disabled state. Takes no params; returns
    /// <see cref="JsonMcpToolsListResult"/>.
    /// </summary>
    public const string List = "McpTools.List";

    /// <summary>
    /// The pipe-RPC counterpart of MCP's <c>tools/call</c>. Validates
    /// <see cref="JsonMcpToolsInvokeParams.Arguments"/> against the
    /// tool's <c>inputSchema</c>, dispatches to the engine-internal
    /// worker, and marshals the worker response into the discriminated
    /// <see cref="JsonMcpToolsInvokeResult"/> — <c>ok</c> /
    /// <c>tool-error</c> / <c>schema-error</c> / <c>disabled</c> /
    /// <c>not-found</c> per <c>design § P2</c>. Shares one handler with
    /// the MCP/stdio <c>tools/call</c> path so both surfaces serialise
    /// the same <c>content</c> bytes (P1). Takes
    /// <see cref="JsonMcpToolsInvokeParams"/>.
    /// </summary>
    public const string Invoke = "McpTools.Invoke";
}
