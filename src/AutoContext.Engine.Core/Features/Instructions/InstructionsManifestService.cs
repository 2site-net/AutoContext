namespace AutoContext.Engine.Core.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service that loads the bundled instructions corpus at engine
/// start and holds the immutable snapshot every <c>Instructions.*</c>
/// RPC handler reads through the
/// <see cref="IInstructionsManifestAccessor"/> seam. The corpus is
/// shipped beside the engine binary as two build-time side-cars (the
/// generated manifest and the hand-authored catalog) and never changes at
/// runtime, so the load is a one-shot read in
/// <see cref="StartAsync(CancellationToken)"/>; there is no watcher and
/// <see cref="StopAsync(CancellationToken)"/> has nothing to tear down.
/// Registered before the RPC pipes so the snapshot is populated before
/// the first connection can land.
/// </summary>
internal sealed partial class InstructionsManifestService : IHostedService, IInstructionsManifestAccessor
{
    private readonly ILogger<InstructionsManifestService> _logger;
    private readonly string _resourcesDirectory;
    private volatile InstructionsManifestSnapshot _current = InstructionsManifestSnapshot.Empty;

    /// <summary>
    /// Creates a service that loads the corpus from
    /// <paramref name="resourcesDirectory"/>.
    /// </summary>
    /// <param name="resourcesDirectory">Absolute path of the directory
    /// holding the two side-cars. Must not be <see langword="null"/>,
    /// empty, or whitespace.</param>
    /// <param name="logger">Diagnostic sink.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="resourcesDirectory"/> is <see langword="null"/>,
    /// empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="logger"/> is <see langword="null"/>.</exception>
    public InstructionsManifestService(
        string resourcesDirectory,
        ILogger<InstructionsManifestService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcesDirectory);
        ArgumentNullException.ThrowIfNull(logger);

        _resourcesDirectory = resourcesDirectory;
        _logger = logger;
    }

    /// <inheritdoc />
    public InstructionsManifestSnapshot Current => _current;

    /// <summary>
    /// Loads the corpus snapshot from the side-cars and publishes it to
    /// <see cref="Current"/>. Throws — failing host start — when the
    /// bundled side-cars are missing or malformed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var snapshot = await InstructionsManifestLoader
            .LoadAsync(_resourcesDirectory, cancellationToken)
            .ConfigureAwait(false);

        _current = snapshot;
        LogCorpusLoaded(_logger, snapshot.Files.Count, _resourcesDirectory);
    }

    /// <summary>
    /// No-op: the corpus is read-only in-memory state with nothing to
    /// release.
    /// </summary>
    /// <param name="cancellationToken">Ignored.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Loaded instruction corpus: {Count} files from '{ResourcesDirectory}'.")]
    private static partial void LogCorpusLoaded(ILogger logger, int count, string resourcesDirectory);
}
