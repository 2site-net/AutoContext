namespace AutoContext.Mcp.Tools.Dispatch;

using System.Text.Json;

using AutoContext.Mcp.Tools.Manifest;
using AutoContext.Mcp.Tools.Wire;

/// <summary>
/// Builds the per-tool dispatch closures consumed by the MCP SDK
/// registration step. Each delegate captures one
/// <see cref="ManifestGroup"/> + <see cref="ManifestTool"/> pair and
/// invokes <see cref="ToolInvoker"/> on call, returning the serialized
/// result envelope as a JSON string.
/// </summary>
public static class ToolDelegateFactory
{
    /// <summary>
    /// Builds the per-tool delegate map keyed by
    /// <c>tool.definition.name</c>. Throws when two tools across the
    /// manifest share the same name — the manifest validator should have
    /// caught this at startup.
    /// </summary>
    public static IReadOnlyDictionary<string, ToolHandler> Build(Manifest manifest, ToolInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(invoker);

        var delegates = new Dictionary<string, ToolHandler>(StringComparer.Ordinal);

        foreach (var groups in manifest.Workers.Values)
        {
            foreach (var group in groups)
            {
                foreach (var tool in group.Tools)
                {
                    var name = tool.Definition.Name;

                    if (delegates.ContainsKey(name))
                    {
                        throw new InvalidOperationException(
                            $"Manifest contains duplicate MCP Tool name '{name}'.");
                    }

                    delegates[name] = async (data, ct) =>
                    {
                        var envelope = await invoker
                            .InvokeAsync(group, tool, data, ct)
                            .ConfigureAwait(false);

                        return JsonSerializer.Serialize(envelope, WireJsonOptions.Instance);
                    };
                }
            }
        }

        return delegates;
    }
}
