namespace AutoContext.McpTools.Manifest.Generator;

/// <summary>
/// One worker group in <c>mcp-tools-registry.json</c>. The projection only reads
/// the tools the worker owns; the worker's own <c>id</c> and <c>name</c> are
/// dispatch metadata the wire-shape catalog does not carry, so they stay
/// unmapped.
/// </summary>
internal sealed class JsonRegistryWorker
{
    /// <summary>Gets the tools this worker owns.</summary>
    public IReadOnlyList<JsonRegistryTool>? Tools { get; init; }
}
