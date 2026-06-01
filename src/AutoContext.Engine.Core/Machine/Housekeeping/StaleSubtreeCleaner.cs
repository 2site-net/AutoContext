namespace AutoContext.Engine.Core.Machine.Housekeeping;

using System.Diagnostics.CodeAnalysis;

using AutoContext.Engine.Core.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// Pattern-matches over <see cref="SubtreeRegistryStatus"/> and
/// deletes any subtree whose retention window has elapsed. Pure
/// consumer of the classification produced by
/// <see cref="CacheRootScanner"/> — no scanning, no liveness probe,
/// no registry I/O of its own. The cleaner is the only piece of
/// the housekeeping pipeline that calls <see cref="Directory.Delete(string, bool)"/>
/// on cache-root subtrees.
/// </summary>
/// <remarks>
/// <para>
/// Retention windows resolve per arm as
/// <c>design § Housekeeping</c> mandates:
/// </para>
/// <list type="bullet">
///   <item><see cref="SubtreeRegistryStatus.Registered"/> — never
///   deleted; a live peer owns the subtree.</item>
///   <item><see cref="SubtreeRegistryStatus.StaleRegistration"/> —
///   compared against
///   <see cref="Protocol.Messages.Registry.JsonRegistryEntry.Retention"/>
///   anchored at <see cref="Protocol.Messages.Registry.JsonRegistryEntry.StartedAt"/>,
///   so each peer's <c>--retention</c> follows its leftover subtree.</item>
///   <item><see cref="SubtreeRegistryStatus.Unregistered"/> and
///   <see cref="SubtreeRegistryStatus.Foreign"/> — compared against
///   this engine's <c>--retention</c> floor via
///   <see cref="RetentionPolicy.Window"/>, anchored at the
///   subtree's filesystem creation timestamp.</item>
/// </list>
/// <para>
/// Concurrent-sweep tolerance per
/// <c>design § Housekeeping &gt; Concurrent sweeps</c>: a
/// <see cref="DirectoryNotFoundException"/> mid-delete (or on the
/// pre-delete timestamp probe) is treated as success — a peer
/// engine reaped the subtree first, which is exactly the contract
/// the registry-mediated housekeeping promises.
/// </para>
/// </remarks>
internal sealed partial class StaleSubtreeCleaner
{
    private readonly ILogger<StaleSubtreeCleaner> _logger;
    private readonly RetentionPolicy _retentionPolicy;

    /// <summary>
    /// Creates a new <see cref="StaleSubtreeCleaner"/>.
    /// </summary>
    /// <param name="retentionPolicy">Single reader of the
    /// engine's <c>--retention</c> floor used for the
    /// <see cref="SubtreeRegistryStatus.Unregistered"/> and
    /// <see cref="SubtreeRegistryStatus.Foreign"/> arms.</param>
    /// <param name="logger">Diagnostic sink. Per-delete failures
    /// that survive the success-on-race
    /// <see cref="DirectoryNotFoundException"/> path are logged
    /// and swallowed so one bad subtree never aborts the sweep.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public StaleSubtreeCleaner(
        RetentionPolicy retentionPolicy,
        ILogger<StaleSubtreeCleaner> logger)
    {
        ArgumentNullException.ThrowIfNull(retentionPolicy);
        ArgumentNullException.ThrowIfNull(logger);

        _retentionPolicy = retentionPolicy;
        _logger = logger;
    }

    /// <summary>
    /// Walks <paramref name="classifications"/> once and deletes
    /// every subtree whose retention window has elapsed.
    /// <see cref="SubtreeRegistryStatus.Registered"/> entries are
    /// always skipped. Cancellation is honoured between entries
    /// — an in-flight <see cref="Directory.Delete(string, bool)"/>
    /// is not aborted, so a sweep can overshoot a tight deadline
    /// by at most one large subtree.
    /// </summary>
    /// <param name="classifications">Per-subtree classification
    /// produced by <see cref="CacheRootScanner.ScanAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the sweep between
    /// entries.</param>
    /// <returns>The number of subtrees the sweep deleted (success
    /// races with peers count as deletions). Diagnostic only — the
    /// caller does not need to act on the count.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="classifications"/> is <see langword="null"/>.
    /// </exception>
    public int Sweep(
        IReadOnlyList<SubtreeRegistryStatus> classifications,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(classifications);

        var deleted = 0;

        foreach (var status in classifications)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryDelete(status))
            {
                deleted++;
            }
        }

        return deleted;
    }

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Reaped stale subtree {SubtreePath} (arm: {Arm}).")]
    private static partial void LogDeleted(ILogger logger, string subtreePath, string arm);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Failed to delete stale subtree {SubtreePath}; the next peer's sweep will retry.")]
    private static partial void LogDeleteFailed(ILogger logger, string subtreePath, Exception exception);

    private static DateTimeOffset? TryGetCreationTime(string subtreePath)
    {
        try
        {
            return new DateTimeOffset(Directory.GetCreationTimeUtc(subtreePath), TimeSpan.Zero);
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            // GetCreationTimeUtc can surface FileNotFoundException
            // on some platforms when the directory vanished mid-call.
            return null;
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Per-subtree delete failures are logged and swallowed so one wedged directory (permission revoked, path too long, transient I/O) never aborts the rest of the sweep — the next peer's graceful shutdown retries.")]
    private bool TryDelete(SubtreeRegistryStatus status)
    {
        if (!ShouldDelete(status))
        {
            return false;
        }

        try
        {
            Directory.Delete(status.SubtreePath, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // Peer engine won the race — success.
            return true;
        }
        catch (Exception ex)
        {
            LogDeleteFailed(_logger, status.SubtreePath, ex);
            return false;
        }

        LogDeleted(_logger, status.SubtreePath, ArmName(status));
        return true;
    }

    private static string ArmName(SubtreeRegistryStatus status)
        => status switch
        {
            SubtreeRegistryStatus.Registered => nameof(SubtreeRegistryStatus.Registered),
            SubtreeRegistryStatus.StaleRegistration => nameof(SubtreeRegistryStatus.StaleRegistration),
            SubtreeRegistryStatus.Unregistered => nameof(SubtreeRegistryStatus.Unregistered),
            SubtreeRegistryStatus.Foreign => nameof(SubtreeRegistryStatus.Foreign),
            _ => "Unknown",
        };

    private bool ShouldDelete(SubtreeRegistryStatus status)
    {
        switch (status)
        {
            case SubtreeRegistryStatus.Registered:
                return false;

            case SubtreeRegistryStatus.StaleRegistration stale:
                return _retentionPolicy.IsExpired(stale.Entry.StartedAt, stale.Entry.Retention);

            case SubtreeRegistryStatus.Unregistered:
            case SubtreeRegistryStatus.Foreign:
                var createdAt = TryGetCreationTime(status.SubtreePath);
                if (createdAt is null)
                {
                    // Directory vanished between scan and check —
                    // treat as already-deleted (peer won the race).
                    return false;
                }

                return _retentionPolicy.IsExpired(createdAt.Value);

            default:
                return false;
        }
    }
}
