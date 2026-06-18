namespace AutoContext.Engine.Core.Workers;

using System.Text.Json;

using AutoContext.Engine.Core.Infrastructure;
using AutoContext.Engine.Core.Workers.Format;

/// <summary>
/// Reads the build-generated <c>workers.json</c> side-car shipped beside
/// the engine binary into an immutable <see cref="JsonWorkersManifest"/>.
/// </summary>
/// <remarks>
/// The side-car is an engine build artifact, not user input: a missing
/// file or malformed JSON is a packaging defect, so the loader throws
/// rather than degrading to a partial manifest. The loader runs inside
/// <see cref="WorkerProcessService.StartAsync"/>, so a defective side-car
/// fails host start loudly — before any worker can be launched. Field-level
/// validation (required ids/commands, duplicate ids) lives in
/// <see cref="WorkerProcessInfoResolver"/>, which consumes this read-model.
/// </remarks>
internal static class WorkersManifestLoader
{
    /// <summary>The build-generated worker-manifest side-car file name.</summary>
    public const string ManifestFileName = "workers.json";

    /// <summary>
    /// Reads and parses the worker-manifest side-car from
    /// <paramref name="resources"/>.
    /// </summary>
    /// <param name="resources">The resources directory holding
    /// <c>workers.json</c> (an override copy shadows the bundled one).
    /// Must not be <see langword="null"/>.</param>
    /// <returns>The parsed, immutable manifest read-model.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="resources"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileNotFoundException">The side-car is
    /// missing.</exception>
    /// <exception cref="InvalidOperationException">The side-car is not
    /// valid JSON or deserialises to <see langword="null"/>.</exception>
    public static JsonWorkersManifest Load(EngineResourcesDirectory resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var manifestPath = resources.ResolveFile(ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                $"Bundled workers manifest not found: '{manifestPath}'.", manifestPath);
        }

        var json = File.ReadAllText(manifestPath);

        JsonWorkersManifest? manifest;

        try
        {
            manifest = JsonSerializer.Deserialize(
                json, WorkersManifestJsonContext.Default.JsonWorkersManifest);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Bundled workers manifest '{manifestPath}' is not valid JSON.", exception);
        }

        return manifest
            ?? throw new InvalidOperationException(
                $"Bundled workers manifest '{manifestPath}' is malformed: it deserialised to null.");
    }
}
