namespace AutoContext.Engine.Core.Workers.Format;

using System.Text.Json.Serialization;

/// <summary>
/// System.Text.Json source-generation context for the build-generated
/// <c>workers.json</c> side-car the engine reads at composition time.
/// CamelCase property names match the manifest the
/// <c>AutoContext.Workers.Manifest.Generator</c> emits, so the engine
/// deserialises it without per-property name attributes and without
/// reflection-based serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(JsonWorkersManifest))]
internal sealed partial class WorkersManifestJsonContext : JsonSerializerContext
{
}
