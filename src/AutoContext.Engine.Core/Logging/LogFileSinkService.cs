namespace AutoContext.Engine.Core.Logging;

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
/// and appends every record to the engine's <c>engine.log</c> file
/// under the per-instance subtree
/// <c>&lt;cacheRoot&gt;/&lt;workspaceHash&gt;/&lt;instanceId&gt;/logs/engine.log</c>.
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
/// Rotation per <c>--logging</c> thresholds and retention-aware
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

    private static readonly byte[] LineTerminator = "\n"u8.ToArray();

    private readonly Broadcaster<JsonLogRecord> _broadcaster;
    private readonly LogChannel _channel;
    private readonly RotatedLogCleaner _cleaner;
    private readonly TaskCompletionSource _executeStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string _filePath;
    private readonly ILogger<LogFileSinkService> _logger;
    private readonly LogRotationThresholds _thresholds;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new <see cref="LogFileSinkService"/> targeted at
    /// the per-instance subtree derived from <paramref name="options"/>.
    /// The target path is composed eagerly so the drain loop's
    /// hot path skips path work.
    /// </summary>
    /// <param name="channel">Ingest channel to drain.</param>
    /// <param name="cacheLayout">Resolved engine cache-root layout.
    /// The active <c>engine.log</c> path is read from
    /// <see cref="EngineCacheLayout.EngineLogFilePath"/> at
    /// construction time and reused for the lifetime of the
    /// service.</param>
    /// <param name="thresholds">Per-verbosity rotation thresholds
    /// — production composes via
    /// <see cref="LogRotationThresholds.ForVerbosity(LogVerbosity)"/>;
    /// tests pass small values directly to keep fixtures cheap.</param>
    /// <param name="cleaner">Retention-aware sweeper invoked on
    /// every successful rotation.</param>
    /// <param name="broadcaster">Sibling fan-out the drain loop
    /// publishes each record to after the file write succeeds. The
    /// broadcaster's per-subscriber buffers and slow-subscriber
    /// eviction shield the file sink from subscriber slowness — a
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
        _filePath = cacheLayout.EngineLogFilePath;
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

        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException(
                $"Engine log path '{_filePath}' has no parent directory; "
                + "EngineCacheLayout must always yield a rooted path.");

        Directory.CreateDirectory(directory);

        var stream = OpenAppendStream(_filePath);
        var bytesWritten = stream.Length;
        var lineCount = 0;

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
                try
                {
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(
                        record,
                        ProtocolJsonContext.Default.JsonLogRecord);

                    await stream.WriteAsync(bytes, CancellationToken.None).ConfigureAwait(false);
                    await stream.WriteAsync(LineTerminator, CancellationToken.None).ConfigureAwait(false);
                    await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);

                    bytesWritten += bytes.Length + LineTerminator.Length;
                    lineCount += 1;
                }
                catch (Exception ex)
                {
                    LogAppendFailed(_logger, _filePath, ex);
                    continue;
                }

                // Fan-out to the logs-pipe broadcaster happens
                // AFTER the file write so a transient write
                // failure (handled above) keeps the record off
                // both sinks symmetrically. TryPublish is
                // non-blocking: slow subscribers are evicted by
                // the broadcaster, never pushing backpressure
                // onto the drain loop.
                _broadcaster.TryPublish(record);

                if (lineCount >= _thresholds.MaxLines
                    || bytesWritten >= _thresholds.MaxBytes)
                {
                    var rotation = await TryRotateAsync(stream, directory).ConfigureAwait(false);
                    stream = rotation.Stream;

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
                    bytesWritten = rotation.Rotated ? 0L : stream.Length;
                    lineCount = 0;
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

            await stream.DisposeAsync().ConfigureAwait(false);
        }
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
    /// Closes <paramref name="current"/>, renames the active log
    /// file to a rotated sibling stamped with the current clock,
    /// invokes the retention sweeper, and opens a fresh active
    /// file. Returns the freshly opened stream together with a
    /// flag indicating whether the rename actually happened —
    /// <see langword="false"/> on a same-second collision or a
    /// transient I/O failure, in which case the active file
    /// stays in place and the next attempt fires against a fresh
    /// timestamp.
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
        Justification = "The freshly opened FileStream is returned to the caller (ExecuteAsync), which owns its disposal via the outer try/finally around the drain loop.")]
    private async Task<RotationResult> TryRotateAsync(FileStream current, string directory)
    {
        try
        {
            await current.DisposeAsync().ConfigureAwait(false);

            var rotatedFileName = RotatedLogCleaner.ComposeRotatedFileName(
                EngineLogBaseName,
                _timeProvider.GetUtcNow());
            var rotatedPath = Path.Combine(directory, rotatedFileName);

            if (File.Exists(rotatedPath))
            {
                LogRotationCollision(_logger, _filePath, rotatedPath);
                return new RotationResult(OpenAppendStream(_filePath), Rotated: false);
            }

            File.Move(_filePath, rotatedPath, overwrite: false);
            _cleaner.DeleteExpired(directory, EngineLogBaseName);
            return new RotationResult(OpenAppendStream(_filePath), Rotated: true);
        }
        catch (Exception ex)
        {
            LogRotationFailed(_logger, _filePath, ex);
            return new RotationResult(OpenAppendStream(_filePath), Rotated: false);
        }
    }

    private readonly record struct RotationResult(FileStream Stream, bool Rotated);
}
