namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// System.Text.Json source-generation context for the generator's two on-disk
/// shapes: the generated <c>instructions-manifest.json</c> fact index it writes,
/// and the hand-authored <c>instructions-catalog.json</c> taxonomy it reads back
/// to cross-validate. Indentation (two spaces), newline (<c>\n</c>), camelCase
/// property names, and null-omission are pinned here so the generated manifest is
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
[JsonSerializable(typeof(JsonInstructionsManifest))]
[JsonSerializable(typeof(JsonInstructionsCatalog))]
internal sealed partial class InstructionsManifestJsonContext : JsonSerializerContext
{
}
