namespace AutoContext.Engine.Core.Logging;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AutoContext.Engine.Core.Machine;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Forward-pass NDJSON reader over the engine's active
/// <c>engine.log</c> file. Backs the unary <c>Logs.GetEngine</c>
/// RPC: applies the request's <c>since</c> / <c>lastN</c> filters,
/// returns the matching records in chronological order, and
/// computes the design's <c>truncated</c> flag.
/// </summary>
/// <remarks>
/// <para>
/// Concurrency: the file is being appended to in parallel by
/// <see cref="LogFileSinkService"/>. The reader opens the file
/// with <see cref="FileShare.ReadWrite"/> + <see cref="FileShare.Delete"/>
/// so the writer's append and the rotation rename both proceed
/// unblocked. A partial last line (a record the writer was still
/// flushing when the read began) parses to a <see cref="JsonException"/>
/// and is silently dropped — the next call will see the full
/// line.
/// </para>
/// <para>
/// Filter order: <see cref="JsonLogsGetEngineParams.Since"/> is
/// applied first (records strictly older than the cutoff are
/// excluded); <see cref="JsonLogsGetEngineParams.LastN"/> is applied
/// second, taking the most recent <c>N</c> records of what
/// remains. The result list is in chronological order
/// (oldest first), matching the design's
/// <c>{ records: LogRecord[] }</c> shape.
/// </para>
/// <para>
/// Truncation semantics: <see cref="EngineLogReadResult.Truncated"/>
/// is <see langword="true"/> when the caller supplied
/// <see cref="JsonLogsGetEngineParams.Since"/> and the active file's
/// earliest record has a timestamp strictly later than that
/// cutoff — i.e. records that would have satisfied the request
/// have been rotated past the active file. <see cref="JsonLogsGetEngineParams.LastN"/>
/// is the caller's explicit cap and does not by itself mark the
/// result truncated.
/// </para>
/// </remarks>
internal sealed class EngineLogFileReader
{
    private readonly EngineCacheLayout _cacheLayout;

    /// <summary>
    /// Creates a new <see cref="EngineLogFileReader"/> targeted at
    /// the engine log file resolved by <paramref name="cacheLayout"/>.
    /// </summary>
    /// <param name="cacheLayout">Resolved engine cache-root layout
    /// the active <c>engine.log</c> path is read from via
    /// <see cref="EngineCacheLayout.EngineLogFilePath"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cacheLayout"/> is <see langword="null"/>.
    /// </exception>
    public EngineLogFileReader(EngineCacheLayout cacheLayout)
    {
        ArgumentNullException.ThrowIfNull(cacheLayout);
        _cacheLayout = cacheLayout;
    }

    /// <summary>
    /// Reads the active <c>engine.log</c>, applies the request's
    /// filters, and returns the matching records together with
    /// the truncated flag. Missing-file is treated as
    /// <see cref="EngineLogReadResult.Empty"/>; the design's
    /// "the engine's own log file always exists for the current
    /// process" wording holds at steady state but does not yet
    /// during the pre-first-record cold start, and the handler
    /// must answer correctly in either case.
    /// </summary>
    /// <param name="parameters">Optional request filters.
    /// <see langword="null"/> means "no filters" — every record in
    /// the active file is returned.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Ordered records satisfying the filter and the
    /// associated truncation flag.</returns>
    /// <remarks>
    /// <para>
    /// Preconditions: <see cref="JsonLogsGetEngineParams.LastN"/>, when
    /// supplied, must be non-negative. The caller (the
    /// <c>Logs.GetEngine</c> dispatch handler) rejects negative
    /// values with <c>InvalidParams</c> before reaching the reader;
    /// the reader itself enforces the contract with
    /// <see cref="ArgumentOutOfRangeException"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="parameters"/>'s <c>LastN</c> is negative.
    /// </exception>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Per-line JSON parse failures (partial flushes, malformed bytes) are dropped and the read continues — one corrupt line must not abort the entire snapshot.")]
    public async Task<EngineLogReadResult> ReadAsync(
        JsonLogsGetEngineParams? parameters,
        CancellationToken cancellationToken)
    {
        var lastN = parameters?.LastN;
        var since = parameters?.Since;

        if (lastN is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parameters),
                lastN,
                "LastN must be non-negative.");
        }

        if (lastN == 0)
        {
            // The truncated flag is still meaningful — a caller
            // polling for "is there anything older than my
            // watermark that rotated away" can ask with LastN=0 and
            // Since=watermark. When Since is null, truncated is
            // definitionally false (no cutoff to compare against),
            // so we can skip the file open entirely. Otherwise we
            // only need the first record's timestamp to decide the
            // flag — stop the scan after that.
            if (since is null)
            {
                return EngineLogReadResult.Empty;
            }

            var (_, probedTruncated) = await ReadRecordsAsync(
                    since,
                    stopAfterFirstRecord: true,
                    cancellationToken)
                .ConfigureAwait(false);

            return new EngineLogReadResult([], probedTruncated);
        }

        var (allRecords, truncatedFlag) = await ReadRecordsAsync(
                since,
                stopAfterFirstRecord: false,
                cancellationToken)
            .ConfigureAwait(false);

        if (lastN is { } cap && allRecords.Count > cap)
        {
            var tail = new List<JsonLogRecord>(cap);

            for (var i = allRecords.Count - cap; i < allRecords.Count; i++)
            {
                tail.Add(allRecords[i]);
            }

            return new EngineLogReadResult(tail, truncatedFlag);
        }

        return new EngineLogReadResult(allRecords, truncatedFlag);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Per-line JSON parse failures (partial flushes, malformed bytes) are dropped and the read continues — one corrupt line must not abort the entire snapshot.")]
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The FileStream is owned by the surrounding await-using block and is disposed deterministically; the analyzer cannot model the ConfigureAwait wrapper pattern.")]
    private async Task<(List<JsonLogRecord> Records, bool Truncated)> ReadRecordsAsync(
        DateTimeOffset? since,
        bool stopAfterFirstRecord,
        CancellationToken cancellationToken)
    {
        var path = _cacheLayout.EngineLogFilePath;
        if (!File.Exists(path))
        {
            return ([], false);
        }

        var records = new List<JsonLogRecord>();
        DateTimeOffset? firstRecordTimestamp = null;

        try
        {
            var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);

            await using (stream.ConfigureAwait(false))
            {
                using var reader = new StreamReader(stream);

                while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)
                    is { } line)
                {
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    JsonLogRecord? record;

                    try
                    {
                        record = JsonSerializer.Deserialize(
                            line,
                            ProtocolJsonContext.Default.JsonLogRecord);
                    }
                    catch (JsonException)
                    {
                        // Partial flush at end-of-file or a corrupt
                        // mid-stream line. Drop and continue.
                        continue;
                    }
                    catch (Exception)
                    {
                        // Defensive — any other parse-time failure
                        // (e.g. encoding) drops the line rather than
                        // aborting the snapshot.
                        continue;
                    }

                    if (record is null)
                    {
                        continue;
                    }

                    firstRecordTimestamp ??= record.Timestamp;

                    if (stopAfterFirstRecord)
                    {
                        // Caller only needs the first record's
                        // timestamp to compute the truncated flag
                        // (the LastN=0 + Since fast path). Skip the
                        // rest of the file.
                        break;
                    }

                    if (since is { } cutoff && record.Timestamp < cutoff)
                    {
                        continue;
                    }

                    records.Add(record);
                }
            }
        }
        catch (FileNotFoundException)
        {
            // Rotation can rename the file out from under us
            // between the Exists check and the open. Treat the
            // race the same as the no-file branch.
            return ([], false);
        }
        catch (DirectoryNotFoundException)
        {
            // The per-instance logs/ directory does not yet exist
            // — the engine has not produced any records since
            // startup. Same shape as the no-file branch.
            return ([], false);
        }

        var truncated = since is { } sinceValue
            && firstRecordTimestamp is { } first
            && first > sinceValue;

        return (records, truncated);
    }
}
