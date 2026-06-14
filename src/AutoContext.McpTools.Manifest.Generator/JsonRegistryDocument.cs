namespace AutoContext.McpTools.Manifest.Generator;

/// <summary>
/// The root of the hand-authored <c>mcp-tools-registry.json</c> as the generator
/// reads it. Only the fields the build-time projection needs are modelled — the
/// per-worker dispatch metadata, per-tool input <c>parameters</c>, and per-task
/// <c>editorconfig</c> bindings the registry also carries are deliberately
/// unmapped, since <c>mcp-tools.json</c> is a wire-shape catalog (no schemas, no
/// dispatch data).
/// </summary>
internal sealed class JsonRegistryDocument
{
    /// <summary>Gets the registry schema version, passed through to the catalog.</summary>
    public string? SchemaVersion { get; init; }

    /// <summary>Gets the worker groups whose tools the projection flattens.</summary>
    public IReadOnlyList<JsonRegistryWorker>? Workers { get; init; }
}
