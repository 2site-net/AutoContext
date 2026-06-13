namespace AutoContext.Workers.Manifest.Generator;

using System.Text.Json;

/// <summary>
/// Aggregates the worker manifest by reading the per-worker
/// <c>.autocontext-worker.json</c> descriptor in each <c>AutoContext.Worker.*</c>
/// project directory. The generator is a dumb aggregator: it copies each
/// descriptor's <c>id</c>, <c>type</c>, optional <c>label</c>, and <c>command</c>
/// verbatim — it does not expand the <c>${root}</c> placeholder, derive any
/// launch behaviour, or probe the project. A worker directory without a
/// descriptor, an unparsable or incomplete descriptor, an unknown <c>type</c>, or
/// two workers declaring the same id all fail the build.
/// </summary>
internal sealed class WorkerDescriptorScanner : IWorkerDescriptorScanner
{
    private const string DescriptorFileName = ".autocontext-worker.json";
    private const string WorkerProjectPrefix = "AutoContext.Worker.";

    private static readonly HashSet<string> KnownTypes =
        new(StringComparer.Ordinal) { "executable", "script", "library" };

    /// <inheritdoc />
    public JsonWorkersManifest Scan(string sourceDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceDirectory);

        var root = Path.GetFullPath(sourceDirectory);

        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException($"[workers.json] worker source directory not found: {root}");
        }

        var byId = new Dictionary<string, JsonWorkerEntry>(StringComparer.Ordinal);

        foreach (var directory in Directory.EnumerateDirectories(root, $"{WorkerProjectPrefix}*"))
        {
            var entry = ReadDescriptor(directory);

            if (!byId.TryAdd(entry.Id, entry))
            {
                throw new InvalidOperationException(
                    $"[workers.json] duplicate worker id '{entry.Id}' declared by '{Path.GetFileName(directory)}'.");
            }
        }

        var workers = byId.Values
            .OrderBy(static worker => worker.Id, StringComparer.Ordinal)
            .ToArray();

        return new JsonWorkersManifest(workers);
    }

    private static JsonWorkerEntry ReadDescriptor(string directory)
    {
        var name = Path.GetFileName(directory);
        var descriptorPath = Path.Combine(directory, DescriptorFileName);

        if (!File.Exists(descriptorPath))
        {
            throw new InvalidOperationException(
                $"[workers.json] worker '{name}' is missing its '{DescriptorFileName}' descriptor.");
        }

        JsonWorkerEntry? entry;

        try
        {
            entry = JsonSerializer.Deserialize(
                File.ReadAllText(descriptorPath),
                WorkersManifestJsonContext.Default.JsonWorkerEntry);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"[workers.json] worker '{name}' has an unparsable '{DescriptorFileName}': {exception.Message}");
        }

        if (entry is null)
        {
            throw new InvalidOperationException(
                $"[workers.json] worker '{name}' has an empty '{DescriptorFileName}'.");
        }

        Validate(name, entry);

        return entry;
    }

    private static void Validate(string name, JsonWorkerEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Id))
        {
            throw new InvalidOperationException($"[workers.json] worker '{name}' descriptor is missing 'id'.");
        }

        if (string.IsNullOrWhiteSpace(entry.Type))
        {
            throw new InvalidOperationException($"[workers.json] worker '{name}' descriptor is missing 'type'.");
        }

        if (!KnownTypes.Contains(entry.Type))
        {
            throw new InvalidOperationException(
                $"[workers.json] worker '{name}' descriptor has unknown type '{entry.Type}' (expected executable, script, or library).");
        }

        if (string.IsNullOrWhiteSpace(entry.Command))
        {
            throw new InvalidOperationException($"[workers.json] worker '{name}' descriptor is missing 'command'.");
        }
    }
}
