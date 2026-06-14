namespace AutoContext.McpTools.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// System.Text.Json source-generation context for the build-time mcp-tools
/// projection: it deserialises the hand-authored <c>mcp-tools-registry.json</c>
/// (<see cref="JsonRegistryDocument"/>) and serialises the generated
/// <c>mcp-tools.json</c> catalog (<see cref="JsonMcpToolsCatalog"/>). Indentation
/// (two spaces), newline (<c>\n</c>), camelCase property names, and null-omission
/// are pinned here so the catalog is emitted deterministically — matching the
/// engine's other source-generated JSON contexts instead of a hand-rolled writer.
/// The only layout knob set at runtime is the relaxed encoder, which cannot be
/// expressed as an attribute constant.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
    IndentCharacter = ' ',
    IndentSize = 2,
    NewLine = "\n",
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(JsonRegistryDocument))]
[JsonSerializable(typeof(JsonMcpToolsCatalog))]
internal sealed partial class McpToolsManifestJsonContext : JsonSerializerContext
{
}
