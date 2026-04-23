namespace AutoContext.Mcp.Server.Dispatch;

using System.Text.Json;

using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Wire;

/// <summary>
/// Builds the per-tool dispatch closures consumed by the MCP SDK
/// registration step. Each delegate captures one
/// <see cref="McpWorker"/> + <see cref="McpToolDefinition"/> pair and
/// invokes <see cref="ToolInvoker"/> on call, returning the serialized
/// result envelope as a JSON string.
/// </summary>
public static class ToolDelegateFactory
{
    /// <summary>
    /// Builds the per-tool delegate map keyed by <c>tool.name</c>. Throws
    /// when two tools across the registry share the same name — the
    /// registry validator should have caught this at startup.
    /// </summary>
    public static IReadOnlyDictionary<string, ToolHandler> Build(McpWorkersCatalog registry, ToolInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(invoker);

        var delegates = new Dictionary<string, ToolHandler>(StringComparer.Ordinal);

        foreach (var worker in registry.Workers)
        {
            foreach (var tool in worker.Tools)
            {
                var name = tool.Name;

                if (delegates.ContainsKey(name))
                {
                    throw new InvalidOperationException(
                        $"Registry contains duplicate MCP Tool name '{name}'.");
                }

                var capturedWorker = worker;
                var capturedTool = tool;

                delegates[name] = async (data, ct) =>
                {
                    var envelope = await invoker
                        .InvokeAsync(capturedWorker, capturedTool, data, ct)
                        .ConfigureAwait(false);

                    return JsonSerializer.Serialize(envelope, WireJsonOptions.Instance);
                };
            }
        }

        return delegates;
    }
}
