namespace AutoContext.Engine.Core.McpServer.Tools.Registry;

using System.Collections.Generic;

using AutoContext.Engine.Core.Features.McpTools;
using AutoContext.Engine.Core.Rpc.Handlers;

/// <summary>
/// The worker-backed tool family: one <see cref="RegistryMcpTool"/> per entry
/// in the immutable MCP-tools registry. Read on demand so the snapshot loaded
/// during host startup is fully populated by the time the tools are first
/// requested.
/// </summary>
internal sealed class RegistryToolSource : IMcpToolSource
{
    private readonly IRpcMethodHandler _mcpToolsHandler;
    private readonly IMcpToolsRegistryAccessor _registryAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryToolSource"/>
    /// class.
    /// </summary>
    /// <param name="registryAccessor">Read seam over the MCP-tools registry
    /// snapshot.</param>
    /// <param name="mcpToolsHandler">The <c>McpTools.*</c> capability handler
    /// every registry tool dispatches through.</param>
    /// <exception cref="ArgumentNullException">Any argument is
    /// <see langword="null"/>.</exception>
    public RegistryToolSource(
        IMcpToolsRegistryAccessor registryAccessor, McpToolsRpcHandler mcpToolsHandler)
    {
        ArgumentNullException.ThrowIfNull(registryAccessor);
        ArgumentNullException.ThrowIfNull(mcpToolsHandler);

        _registryAccessor = registryAccessor;
        _mcpToolsHandler = mcpToolsHandler;
    }

    /// <inheritdoc />
    public IReadOnlyList<IMcpTool> GetTools()
    {
        var registry = _registryAccessor.Current;
        var tools = new List<IMcpTool>(registry.Tools.Count);

        foreach (var entry in registry.Tools)
        {
            tools.Add(new RegistryMcpTool(entry, _mcpToolsHandler));
        }

        return tools;
    }
}
