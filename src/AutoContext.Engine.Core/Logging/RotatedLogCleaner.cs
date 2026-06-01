namespace AutoContext.Engine.Core.Logging;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.Extensions.Logging;

/// <summary>
/// Deletes rotated log files whose filename timestamp falls
/// outside the engine's <see cref="RetentionPolicy"/> window.
/// Invoked by <see cref="LogFileSinkService"/> on the rotation
/// event itself (the cheap directory scan described in
/// <c>design § Housekeeping &gt; Rotated-file retention</c>); no
/// separate timer is needed.
/// </summary>
/// <remarks>
/// <para>
/// Only files matching the canonical rotated-log pattern
/// <c>{baseName}-yyyyMMddTHHmmssZ.log</c> are considered — the
/// active file (<c>{baseName}.log</c>), unrelated artefacts, and
/// anything whose timestamp segment fails to parse are
/// untouched. The same scanner serves any base name, so the
/// engine's own <c>engine</c> base and worker bases
/// (<c>worker-&lt;workerId&gt;</c>) share one implementation.
/// </para>
/// <para>
/// Sweeps are tolerant of races: a file that vanishes between
/// the enumeration and the delete is treated as success (a
/// concurrent peer engine may have reaped it first; a hand-edit
/// from outside the engine could equally cause this).
/// </para>
/// </remarks>
internal sealed partial class RotatedLogCleaner
{
    /// <summary>
    /// Filename timestamp format used for both producing and
    /// parsing rotated-log names. UTC, basic-format ISO 8601 with
    /// <c>:</c> stripped to keep the filename portable across
    /// every host platform per
    /// <c>design § Housekeeping &gt; Log rotation</c>.
    /// </summary>
    internal const string TimestampFormat = "yyyyMMddTHHmmssZ";

    private readonly ILogger<RotatedLogCleaner> _logger;
    private readonly RetentionPolicy _retentionPolicy;

    /// <summary>
    /// Creates a new <see cref="RotatedLogCleaner"/>.
    /// </summary>
    /// <param name="retentionPolicy">Policy consulted for the
    /// retention window.</param>
    /// <param name="logger">Diagnostic sink for delete failures
    /// that survive the per-file
    /// <c>FileNotFoundException</c>/<c>DirectoryNotFoundException</c>
    /// success path.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public RotatedLogCleaner(
        RetentionPolicy retentionPolicy,
        ILogger<RotatedLogCleaner> logger)
    {
        ArgumentNullException.ThrowIfNull(retentionPolicy);
        ArgumentNullException.ThrowIfNull(logger);

        _retentionPolicy = retentionPolicy;
        _logger = logger;
    }

    /// <summary>
    /// Composes the rotated-file name produced by a rotation
    /// event stamped <paramref name="rotatedAt"/>. The result
    /// always carries the UTC timestamp format defined by
    /// <see cref="TimestampFormat"/>.
    /// </summary>
    /// <param name="baseName">Stable basename of the active log
    /// (e.g. <c>"engine"</c>).</param>
    /// <param name="rotatedAt">Timestamp of the rotation
    /// event.</param>
    /// <returns>The rotated-file basename
    /// (<c>{baseName}-{ts}.log</c>) ready to combine with the
    /// logs directory.</returns>
    public static string ComposeRotatedFileName(string baseName, DateTimeOffset rotatedAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseName);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{baseName}-{rotatedAt.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture)}.log");
    }

    /// <summary>
    /// Scans <paramref name="logsDirectory"/> for files matching
    /// the rotated-log pattern for <paramref name="baseName"/>
    /// and deletes every entry whose embedded timestamp is older
    /// than <see cref="RetentionPolicy.Window"/>.
    /// </summary>
    /// <param name="logsDirectory">Directory to scan. A missing
    /// directory is treated as "nothing to clean" — no
    /// exception.</param>
    /// <param name="baseName">Active-file basename whose rotated
    /// siblings should be considered (e.g.
    /// <c>"engine"</c>).</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="baseName"/> is <see langword="null"/> or
    /// empty.
    /// </exception>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Per-file delete and directory-enumeration failures are logged and swallowed so one bad entry (or a wedged directory enumerator) never aborts the rest of the sweep — the next rotation will retry every survivor.")]
    public void DeleteExpired(string logsDirectory, string baseName)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseName);

        if (string.IsNullOrEmpty(logsDirectory) || !Directory.Exists(logsDirectory))
        {
            return;
        }

        var searchPattern = string.Create(
            CultureInfo.InvariantCulture,
            $"{baseName}-*.log");
        var prefix = string.Create(
            CultureInfo.InvariantCulture,
            $"{baseName}-");
        const string Suffix = ".log";

        // Materialise the enumeration eagerly so any I/O failure
        // surfaces inside this try/catch rather than partway
        // through the foreach below — Directory.EnumerateFiles is
        // lazy and can throw on every MoveNext, not just on the
        // call itself.
        List<string> candidates;

        try
        {
            candidates = [.. Directory.EnumerateFiles(logsDirectory, searchPattern)];
        }
        catch (DirectoryNotFoundException)
        {
            // Concurrent housekeeping may have reaped the whole
            // logs subtree between the Directory.Exists probe
            // and EnumerateFiles. That is success.
            return;
        }
        catch (Exception ex)
        {
            // Permission revoked, path too long, transient I/O
            // — log once and treat the sweep as a no-op. The
            // next rotation retries.
            LogEnumerateFailed(_logger, logsDirectory, ex);
            return;
        }

        foreach (var path in candidates)
        {
            var fileName = Path.GetFileName(path);

            if (!TryParseRotationTimestamp(fileName, prefix, Suffix, out var rotatedAt))
            {
                continue;
            }

            if (!_retentionPolicy.IsExpired(rotatedAt))
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (FileNotFoundException)
            {
                // Peer engine won the race — success.
            }
            catch (DirectoryNotFoundException)
            {
                // The whole subtree disappeared mid-sweep —
                // also success.
                return;
            }
            catch (Exception ex)
            {
                LogDeleteFailed(_logger, path, ex);
            }
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Failed to delete expired rotated log file {FilePath}.")]
    private static partial void LogDeleteFailed(ILogger logger, string filePath, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Failed to enumerate rotated log files in {Directory}; skipping this sweep.")]
    private static partial void LogEnumerateFailed(ILogger logger, string directory, Exception exception);

    private static bool TryParseRotationTimestamp(
        string fileName,
        string prefix,
        string suffix,
        out DateTimeOffset rotatedAt)
    {
        rotatedAt = default;

        if (!fileName.StartsWith(prefix, StringComparison.Ordinal)
            || !fileName.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var timestampSegment = fileName.AsSpan(
            prefix.Length,
            fileName.Length - prefix.Length - suffix.Length);

        if (!DateTimeOffset.TryParseExact(
            timestampSegment,
            TimestampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out rotatedAt))
        {
            return false;
        }

        return true;
    }
}
