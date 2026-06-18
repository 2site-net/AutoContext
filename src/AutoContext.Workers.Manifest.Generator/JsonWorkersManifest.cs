namespace AutoContext.Workers.Manifest.Generator;

/// <summary>
/// The build-generated <c>workers.json</c> envelope: the per-worker rows the
/// engine reads to resolve each bundled worker's launch target. The manifest is a
/// pure build output — the generator aggregates each worker's hand-authored
/// <c>.autocontext-worker.json</c> descriptor verbatim, never deriving or
/// reshaping the fields.
/// </summary>
internal sealed class JsonWorkersManifest(IReadOnlyList<JsonWorkerEntry> workers)
{
    /// <summary>Gets the per-worker rows, ordered by <see cref="JsonWorkerEntry.Id"/>.</summary>
    public IReadOnlyList<JsonWorkerEntry> Workers { get; } = workers;
}
