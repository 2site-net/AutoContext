namespace AutoContext.Engine.Core.Workers.Format;

/// <summary>
/// Disk read-model for the build-generated <c>workers.json</c> side-car
/// the engine ships beside its binary: a flat list of worker rows, in
/// document order. Mirrors the envelope written by the
/// <c>AutoContext.Workers.Manifest.Generator</c>. This is the engine's own
/// loader-side copy; it deliberately does not reference the generator
/// project's internal types.
/// </summary>
/// <param name="Workers">The worker rows, in document order.</param>
internal sealed record JsonWorkersManifest(
    IReadOnlyList<JsonWorkerEntry>? Workers = null);
