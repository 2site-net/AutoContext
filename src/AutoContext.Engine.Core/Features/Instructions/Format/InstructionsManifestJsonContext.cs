namespace AutoContext.Engine.Core.Features.Instructions.Format;

using System.Text.Json.Serialization;

/// <summary>
/// System.Text.Json source-generation context for the two build-time
/// instruction side-cars the engine reads at startup: the hand-authored
/// <c>instructions-catalog.json</c> curatorial layer and the generated
/// <c>instructions-manifest.json</c> fact index. CamelCase property names
/// match the generator that emits the manifest and the schema the catalog
/// is authored against, so the engine deserialises both without
/// per-property name attributes and without reflection-based
/// serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(JsonInstructionsManifest))]
[JsonSerializable(typeof(JsonInstructionsCatalog))]
internal sealed partial class InstructionsManifestJsonContext : JsonSerializerContext
{
}
