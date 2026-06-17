namespace AutoContext.Engine.Core.Workers;

using System.Text.Json;

using AutoContext.Engine.Core.Workers.Format;

/// <summary>
/// Reads the build-generated <c>workers.json</c> side-car shipped beside
/// the engine binary into an immutable <see cref="JsonWorkersManifest"/>.
/// </summary>
/// <remarks>
/// The side-car is an engine build artifact, not user input: a missing
/// file or malformed JSON is a packaging defect, so the loader throws
/// rather than degrading to a partial manifest. Failing here fails engine
/// composition loudly, which is the intended behaviour. Field-level
/// validation (required ids/commands, duplicate ids) lives in
/// <see cref="WorkerProcessInfoResolver"/>, which consumes this read-model.
/// </remarks>
internal static class WorkersManifestLoader
{
    /// <summary>The build-generated worker-manifest side-car file name.</summary>
    public const string ManifestFileName = "workers.json";

    /// <summary>
    /// Reads and parses the worker-manifest side-car in
    /// <paramref name="resourcesDirectory"/>.
    /// </summary>
    /// <param name="resourcesDirectory">Absolute path of the directory
    /// holding <c>workers.json</c>. Must not be <see langword="null"/>,
    /// empty, or whitespace.</param>
    /// <returns>The parsed, immutable manifest read-model.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="resourcesDirectory"/> is <see langword="null"/>,
    /// empty, or whitespace.</exception>
    /// <exception cref="FileNotFoundException">The side-car is
    /// missing.</exception>
    /// <exception cref="InvalidOperationException">The side-car is not
    /// valid JSON or deserialises to <see langword="null"/>.</exception>
    public static JsonWorkersManifest Load(string resourcesDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcesDirectory);

        var manifestPath = Path.Combine(resourcesDirectory, ManifestFileName);

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
