namespace AutoContext.Workers.Manifest.Generator;

/// <summary>
/// Aggregates the per-worker <c>.autocontext-worker.json</c> descriptors under
/// the <c>AutoContext.Worker.*</c> project directories into a worker manifest.
/// </summary>
internal interface IWorkerDescriptorScanner
{
    /// <summary>
    /// Reads every <c>AutoContext.Worker.*</c> descriptor under
    /// <paramref name="sourceDirectory"/> and aggregates them into a manifest,
    /// sorted by id.
    /// </summary>
    /// <param name="sourceDirectory">The directory holding the worker projects.</param>
    /// <returns>The aggregated manifest.</returns>
    /// <exception cref="ArgumentException"><paramref name="sourceDirectory"/> is
    /// <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidOperationException">The directory is missing, a
    /// worker has no descriptor, a descriptor is invalid, or two workers declare
    /// the same id.</exception>
    JsonWorkersManifest Scan(string sourceDirectory);
}
