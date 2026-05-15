namespace AutoContext.Engine.Core;

/// <summary>
/// MCP-server capability mode for the engine, mirroring the
/// <c>--mcp-server</c> CLI switch per
/// <c>design § Engine options &gt; --mcp-server</c>. The switch is a
/// <i>capability</i> selector, not a transport selector — the value
/// shape leaves room for additional modes (e.g. an HTTP transport)
/// without renaming the switch.
/// </summary>
public enum EngineMcpServerMode
{
    /// <summary>
    /// MCP-server capability is not registered.
    /// </summary>
    Off = 0,

    /// <summary>
    /// Register the MCP server with the stdio transport
    /// (<c>WithStdioServerTransport</c>). When this mode is active
    /// stdout is reserved for the MCP JSON-RPC channel; the argv
    /// parser rejects any switch that could cause a stray stdout
    /// write.
    /// </summary>
    WithStdio = 1,
}
