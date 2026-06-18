namespace AutoContext.Workers.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// System.Text.Json source-generation context for the generated
/// <c>workers.json</c> manifest. Indentation (two spaces), newline (<c>\n</c>),
/// camelCase property names, and null-omission are pinned here so the manifest is
/// emitted deterministically — matching the engine's other source-generated JSON
/// contexts instead of a hand-rolled writer. The only layout knob set at runtime
/// is the relaxed encoder, which cannot be expressed as an attribute constant.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
    IndentCharacter = ' ',
    IndentSize = 2,
    NewLine = "\n",
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(JsonWorkersManifest))]
[JsonSerializable(typeof(JsonWorkerEntry))]
internal sealed partial class WorkersManifestJsonContext : JsonSerializerContext
{
}
