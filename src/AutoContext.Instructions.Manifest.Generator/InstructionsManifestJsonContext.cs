namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// System.Text.Json source-generation context for both build-time catalogues:
/// the wire-shape <c>instructions-files.json</c> manifest and the catalogue-only
/// <c>instructions-files-metadata.json</c> index. Indentation (two spaces),
/// newline (<c>\n</c>), camelCase property names, and null-omission are pinned
/// here so the generator emits both catalogues deterministically — matching the
/// engine's other source-generated JSON contexts instead of a hand-rolled
/// writer. The only layout knob set at runtime is the relaxed encoder, which
/// cannot be expressed as an attribute constant.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
    IndentCharacter = ' ',
    IndentSize = 2,
    NewLine = "\n",
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(InstructionsManifest))]
[JsonSerializable(typeof(InstructionsMetadata))]
internal sealed partial class InstructionsManifestJsonContext : JsonSerializerContext
{
}
