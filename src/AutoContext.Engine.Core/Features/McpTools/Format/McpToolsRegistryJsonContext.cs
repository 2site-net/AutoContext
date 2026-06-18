namespace AutoContext.Engine.Core.Features.McpTools.Format;

using System.Text.Json.Serialization;

/// <summary>
/// System.Text.Json source-generation context for the bundled
/// <c>mcp-tools-registry.json</c> side-car the engine reads at startup.
/// CamelCase property names match the hand-authored registry and the
/// schema it is authored against, so the engine deserialises it without
/// per-property name attributes and without reflection-based
/// serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(JsonMcpToolsRegistry))]
internal sealed partial class McpToolsRegistryJsonContext : JsonSerializerContext
{
}
