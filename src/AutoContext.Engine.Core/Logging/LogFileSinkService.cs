namespace AutoContext.Engine.Core.Logging;

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Machine;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Background hosted service that drains <see cref="LogChannel"/>
/// and appends every record to the on-disk log its
/// <c>category</c> routes to under the per-instance subtree
/// <c>&lt;cacheRoot&gt;/&lt;workspaceHash&gt;/&lt;instanceId&gt;/logs/</c>:
/// records whose category begins <c>worker.&lt;workerId&gt;.</c>
/// land in that worker's <c>worker-&lt;workerId&gt;.log</c>, and
/// every other record lands in <c>engine.log</c>. Per-worker
/// appenders are opened lazily on first use and rotate
/// independently.
/// </summary>
/// <remarks>
/// <para>
/// Each <see cref="JsonLogRecord"/> is written as one NDJSON line
/// serialised through <see cref="ProtocolJsonContext"/>
/// (the source-generated, AOT-safe converter the protocol owns).
/// The on-disk byte shape matches the wire shape on the
/// <c>logs</c> pipe and the <c>Logs.Tail*</c> RPC stream — a
/// single record envelope is shared across in-process producers,
/// the wire, and disk.
/// </para>
/// <para>
/// Lifecycle:
/// <list type="bullet">
///   <item><c>StartAsync</c> kicks off <c>ExecuteAsync</c>, which
///     opens <c>engine.log</c> in append mode and runs the drain
///     loop until the channel completes.</item>
///   <item><see cref="StopAsync(CancellationToken)"/> signals
///     end-of-stream on the ingest channel (so the drain loop's
///     <c>await foreach</c> over
///     <see cref="System.Threading.Channels.ChannelReader{T}.ReadAllAsync(CancellationToken)"/>
///     exits naturally once every buffered record has been
///     written), then defers to
///     <see cref="BackgroundService.StopAsync(CancellationToken)"/>
///     to await the drain task with the supplied
///     cancellation token.</item>
/// </list>
/// </para>
/// <para>
/// Rotation per <c>--log-rotation</c> thresholds and retention-aware
/// cleanup of rotated files run in the same drain loop: every
/// record drained from the channel updates a running byte / line
/// counter against the configured
/// <see cref="LogRotationThresholds"/>; once either ceiling is
/// crossed the active file is renamed to
/// <c>engine-yyyyMMddTHHmmssZ.log</c> and a fresh
/// <c>engine.log</c> is opened, after which the rotated-log
/// directory is swept by <see cref="RotatedLogCleaner"/>. The
/// service keeps owning the single drain loop and dispatches
/// each record to the file sink and to the
/// <c>logs</c>-pipe broadcaster downstream.
/// </para>
/// </remarks>
internal sealed partial class LogFileSinkService : BackgroundService
{
    /// <summary>Stable basename of the active engine log file
    /// (without the rotation timestamp segment). Used as the
    /// prefix for rotated <c>engine-yyyyMMddTHHmmssZ.log</c>
    /// files; the active-file basename itself is owned by
    /// <see cref="EngineCacheLayout.EngineLogFileName"/>.</summary>
    internal const string EngineLogBaseName = "engine";

    private static readonly SearchValues<char> InvalidWorkerIdChars =
        SearchValues.Create(Path.GetInvalidFileNameChars());

    private static readonly byte[] LineTerminator = "\n"u8.ToArray();

    private readonly Broadcaster<JsonLogRecord> _broadcaster;
    private readonly EngineCacheLayout _cacheLayout;
    private readonly LogChannel _channel;
    private readonly RotatedLogCleaner _cleaner;
    private readonly string _engineFilePath;
    private readonly TaskCompletionSource _executeStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ILogger<LogFileSinkService> _logger;
    private readonly LogRotationThresholds _thresholds;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new <see cref="LogFileSinkService"/> targeted at
    /// the per-instance subtree derived from <paramref name="cacheLayout"/>.
    /// Paths are resolved eagerly so the drain loop's hot path
    /// skips path work.
    /// </summary>
    /// <param name="channel">Ingest channel to drain.</param>
    /// <param name="cacheLayout">Resolved engine cache-root layout.
    /// The logs directory, the active <c>engine.log</c> path, and
    /// each per-worker <c>worker-&lt;workerId&gt;.log</c> path are
    /// resolved from it; the layout is retained for the lifetime
    /// of the service.</param>
    /// <param name="thresholds">Per-rotation-size thresholds
    /// — production composes via
    /// <see cref="LogRotationThresholds.ForRotationSize(LogRotationSize)"/>;
    /// tests pass small values directly to keep fixtures cheap.</param>
    /// <param name="cleaner">Retention-aware sweeper invoked on
    /// every successful rotation.</param>
    /// <param name="broadcaster">Sibling fan-out the drain loop
    /// publishes each record to after the file write succeeds. The
    /// broadcaster's per-subscriber buffers and slow-subscriber
    /// drop shield the file sink from subscriber slowness — a
    /// stalled <c>logs</c>-pipe consumer cannot stall the file
    /// sink.</param>
    /// <param name="timeProvider">Clock source used to stamp
    /// rotated-file names.</param>
    /// <param name="logger">Diagnostic sink for I/O failures inside
    /// the drain loop.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public LogFileSinkService(
        LogChannel channel,
        EngineCacheLayout cacheLayout,
        LogRotationThresholds thresholds,
        RotatedLogCleaner cleaner,
        Broadcaster<JsonLogRecord> broadcaster,
        TimeProvider timeProvider,
        ILogger<LogFileSinkService> logger)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(cacheLayout);
        ArgumentNullException.ThrowIfNull(thresholds);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(broadcaster);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _broadcaster = broadcaster;
        _channel = channel;
        _cleaner = cleaner;
        _logger = logger;
        _thresholds = thresholds;
        _timeProvider = timeProvider;
        _cacheLayout = cacheLayout;
        _engineFilePath = cacheLayout.EngineLogFilePath;
    }

    /// <inheritdoc />
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // BackgroundService.StartAsync (net8+) schedules
        // ExecuteAsync via Task.Run(..., _stoppingCts.Token).
        // If a caller flips straight from StartAsync to
        // StopAsync, the linked token can cancel before the
        // scheduled work item runs — the task is observed as
        // cancelled without ever entering ExecuteAsync, and
        // every buffered record is lost. Synchronise on the
        // ExecuteAsync entry signal so StartAsync only returns
        // once the drain loop has taken ownership of the
        // channel.
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
        await _executeStarted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Signal end-of-stream on the channel so the drain loop's
        // foreach completes naturally after flushing every
        // buffered record. base.StopAsync then awaits the
        // ExecuteAsync task with the supplied cancellation token,
        // so a tight host-shutdown budget can still interrupt
        // the drain if the file system is wedged.
        _channel.Complete();

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Per-record file write failures are swallowed (after one diagnostic log) so a transient I/O fault does not tear down the drain loop and drop every subsequent record on the floor.")]
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Ownership of the active FileStream travels through the rotation loop — each iteration either keeps the current stream or replaces it with a fresh one whose ownership transfers to the same local. The outer try/finally disposes whichever stream the loop holds when it exits.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Signal StartAsync that the drain loop has taken
        // ownership of the channel — see the StartAsync override
        // for the cancellation race this guards against.
        _executeStarted.TrySetResult();

        var directory = _cacheLayout.LogsDirPath;
        Directory.CreateDirectory(directory);

        // One appender per destination file, opened lazily the
        // first time a record routes to it. The engine's own
        // records land in engine.log; a worker's records
        // (category worker.<workerId>.*) land in that worker's
        // worker-<workerId>.log. Each destination tracks its own
        // rotation counters so a chatty worker rotates
        // independently of the engine.
        var targets = new Dictionary<string, LogTarget>(StringComparer.Ordinal);

        try
        {
            // ReadAllAsync and the per-record writes intentionally
            // run on CancellationToken.None: graceful shutdown
            // completes the channel via StopAsync's _channel.Complete()
            // call, so the foreach drains every buffered record
            // before exiting. We deliberately do NOT honour
            // stoppingToken inside the loop — BackgroundService
            // cancels it on StopAsync, which would race the drain
            // and lose buffered records (the very thing this service
            // exists to persist). Host-shutdown grace is bounded
            // upstream by the IHostApplicationLifetime shutdown
            // timeout, which BackgroundService.StopAsync threads
            // through its own Task.Delay race.

            await foreach (var record in _channel.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
            {
                var target = ResolveTarget(targets, record.Category);

                try
                {
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(
                        record,
                        ProtocolJsonContext.Default.JsonLogRecord);

                    await target.Stream.WriteAsync(bytes, CancellationToken.None).ConfigureAwait(false);
                    await target.Stream.WriteAsync(LineTerminator, CancellationToken.None).ConfigureAwait(false);
                    await target.Stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);

                    target.BytesWritten += bytes.Length + LineTerminator.Length;
                    target.LineCount += 1;
                }
                catch (Exception ex)
                {
                    LogAppendFailed(_logger, target.FilePath, ex);
                    continue;
                }

                // Fan-out to the logs-pipe broadcaster happens
                // AFTER the file write so a transient write
                // failure (handled above) keeps the record off
                // both sinks symmetrically. Every record — engine
                // or worker — fans out through the one shared
                // broadcaster; subscribers filter by category.
                // TryPublish is non-blocking: slow subscribers are
                // dropped by the broadcaster, never pushing
                // backpressure onto the drain loop.
                _broadcaster.TryPublish(record);

                if (target.LineCount >= _thresholds.MaxLines
                    || target.BytesWritten >= _thresholds.MaxBytes)
                {
                    var rotation = await TryRotateAsync(target, directory).ConfigureAwait(false);
                    target.Stream = rotation.Stream;

                    // Reset BOTH counters whether or not the
                    // rename actually happened. On a successful
                    // rotation the new active file starts empty.
                    // On a deferred rotation (same-UTC-second
                    // collision or a transient I/O failure) the
                    // active file is still in place, but we must
                    // not retry on every subsequent record — that
                    // would be a per-record busy loop of
                    // dispose+probe+reopen until the clock
                    // advances. Resetting lets writes accumulate
                    // another full threshold's worth before the
                    // next attempt, by which point the UTC second
                    // has long since advanced. The cost is a
                    // bounded worst-case overshoot of 2× the
                    // configured threshold on the deferred file,
                    // never compounding.
                    target.BytesWritten = rotation.Rotated ? 0L : target.Stream.Length;
                    target.LineCount = 0;
                }
            }
        }
        finally
        {
            // Signal end-of-stream to every connected logs-pipe
            // subscriber so their pumps observe a clean EOF rather
            // than hanging on a never-completing channel after the
            // engine has stopped producing records. Idempotent.
            _broadcaster.Complete();

            foreach (var target in targets.Values)
            {
                await target.Stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Extracts the worker id from a <c>worker.&lt;workerId&gt;[.&lt;…&gt;]</c>
    /// category. Returns <see langword="false"/> for any category
    /// that does not name a worker, or whose id segment would not
    /// compose a safe filename — such records route to the engine
    /// log.
    /// </summary>
    internal static bool TryExtractWorkerId(string category, out string workerId)
    {
        const string prefix = "worker.";
        workerId = string.Empty;

        if (string.IsNullOrEmpty(category)
            || !category.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = category.AsSpan(prefix.Length);
        var dot = rest.IndexOf('.');
        var idSpan = dot >= 0 ? rest[..dot] : rest;

        if (idSpan.IsEmpty || idSpan.IndexOfAny(InvalidWorkerIdChars) >= 0)
        {
            return false;
        }

        workerId = idSpan.ToString();
        return true;
    }

    /// <summary>
    /// Resolves the destination file path and rotation basename
    /// for a record's <paramref name="category"/> — the worker's
    /// log when the category names a worker, the engine log
    /// otherwise.
    /// </summary>
    private (string FilePath, string BaseName) ResolveDestination(string category)
    {
        if (TryExtractWorkerId(category, out var workerId))
        {
            return (_cacheLayout.WorkerLogFilePath(workerId), EngineCacheLayout.WorkerLogBaseName(workerId));
        }

        return (_engineFilePath, EngineLogBaseName);
    }

    /// <summary>
    /// Returns the appender the record routes to, opening it
    /// lazily on first use. Records whose category begins
    /// <c>worker.&lt;workerId&gt;.</c> route to that worker's
    /// <c>worker-&lt;workerId&gt;.log</c>; every other record
    /// routes to <c>engine.log</c>.
    /// </summary>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The opened FileStream's ownership transfers to the LogTarget stored in the targets map; ExecuteAsync's finally disposes every target stream when the drain loop exits.")]
    private LogTarget ResolveTarget(Dictionary<string, LogTarget> targets, string category)
    {
        var (filePath, baseName) = ResolveDestination(category);

        if (!targets.TryGetValue(filePath, out var target))
        {
            var stream = OpenAppendStream(filePath);
            target = new LogTarget
            {
                FilePath = filePath,
                BaseName = baseName,
                Stream = stream,
                BytesWritten = stream.Length,
                LineCount = 0,
            };
            targets[filePath] = target;
        }

        return target;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Failed to append log record to {FilePath}.")]
    private static partial void LogAppendFailed(ILogger logger, string filePath, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Rotation of {ActiveFile} collided with existing rotated file {RotatedFile}; deferring to the next record.")]
    private static partial void LogRotationCollision(ILogger logger, string activeFile, string rotatedFile);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Rotation of {ActiveFile} failed; the active file will be reopened and rotation retried on the next record.")]
    private static partial void LogRotationFailed(ILogger logger, string activeFile, Exception exception);

    private static FileStream OpenAppendStream(string path)
        => new(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

    /// <summary>
    /// Closes <paramref name="target"/>'s active stream, renames
    /// its active log file to a rotated sibling stamped with the
    /// current clock, invokes the retention sweeper, and opens a
    /// fresh active file. Returns the freshly opened stream
    /// together with a flag indicating whether the rename actually
    /// happened — <see langword="false"/> on a same-second
    /// collision or a transient I/O failure, in which case the
    /// active file stays in place and the next attempt fires
    /// against a fresh timestamp.
    /// </summary>
    /// <remarks>
    /// Retention sweep failures are isolated from the rotation
    /// outcome: a successful rename followed by a failing sweep
    /// still reports <see cref="RotationResult.Rotated"/> =
    /// <see langword="true"/>, because the active file genuinely
    /// rotated. The sweep failure is logged and survives until
    /// the next rotation retries it.
    /// </remarks>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Rotation failures are logged and swallowed so a wedged file system never tears down the drain loop — the active file stays in use and the next record retries the rotation.")]
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The freshly opened FileStream is returned to the caller (ExecuteAsync), which stores it back on the LogTarget and owns its disposal via the outer try/finally around the drain loop.")]
    private async Task<RotationResult> TryRotateAsync(LogTarget target, string directory)
    {
        try
        {
            await target.Stream.DisposeAsync().ConfigureAwait(false);

            var rotatedFileName = RotatedLogCleaner.ComposeRotatedFileName(
                target.BaseName,
                _timeProvider.GetUtcNow());
            var rotatedPath = Path.Combine(directory, rotatedFileName);

            if (File.Exists(rotatedPath))
            {
                LogRotationCollision(_logger, target.FilePath, rotatedPath);
                return new RotationResult(OpenAppendStream(target.FilePath), Rotated: false);
            }

            File.Move(target.FilePath, rotatedPath, overwrite: false);
            _cleaner.DeleteExpired(directory, target.BaseName);
            return new RotationResult(OpenAppendStream(target.FilePath), Rotated: true);
        }
        catch (Exception ex)
        {
            LogRotationFailed(_logger, target.FilePath, ex);
            return new RotationResult(OpenAppendStream(target.FilePath), Rotated: false);
        }
    }

    /// <summary>
    /// Mutable per-destination appender state the drain loop
    /// carries for one on-disk log file (the engine log or one
    /// worker log): the open stream plus the running rotation
    /// counters, and the immutable path/basename it rotates under.
    /// </summary>
    private sealed class LogTarget
    {
        /// <summary>Rotation basename (active file's name without
        /// the <c>.log</c> extension) — <c>engine</c> or
        /// <c>worker-&lt;workerId&gt;</c>.</summary>
        public required string BaseName { get; init; }

        /// <summary>Bytes written to the active file since it was
        /// opened, against <see cref="LogRotationThresholds.MaxBytes"/>.</summary>
        public long BytesWritten { get; set; }

        /// <summary>Absolute path to the active log file.</summary>
        public required string FilePath { get; init; }

        /// <summary>Lines written to the active file since it was
        /// opened, against <see cref="LogRotationThresholds.MaxLines"/>.</summary>
        public int LineCount { get; set; }

        /// <summary>The currently open append stream. Replaced on
        /// each rotation.</summary>
        public required FileStream Stream { get; set; }
    }

    private readonly record struct RotationResult(FileStream Stream, bool Rotated);
}
