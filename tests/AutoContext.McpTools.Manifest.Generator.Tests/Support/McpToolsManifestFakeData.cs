namespace AutoContext.McpTools.Manifest.Generator.Tests.Support;

using AutoContext.McpTools.Manifest.Generator;

internal static class McpToolsManifestFakeData
{
    internal static JsonMcpToolsCatalog CreateCatalog(params JsonMcpToolEntry[] tools)
        => new("1", tools);

    internal static JsonMcpToolEntry CreateEntry(
        string name = "tool_one",
        string description = "Tool one.",
        params string[] tasks)
        => new(name, description, tasks.Select(static task => new JsonMcpTaskEntry(task)).ToArray());
}
