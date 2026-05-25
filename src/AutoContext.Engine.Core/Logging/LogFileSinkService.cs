namespace AutoContext.Engine.Core.Logging;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Core.Registry;
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
/// Each <see cref="LogRecord"/> is written as one NDJSON line
/// serialised through <see cref="ProtocolJsonContext"/>
/// (the source-generated, AOT-safe converter the protocol owns).
/// The on-disk byte shape matches the wire shape on the
/// <c>logs</c> pipe and the <c>Logs.Tail*</c> RPC stream — there
/// is one envelope, not two (<c>P1</c>: one record envelope).
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
/// Rotation per <c>--logging</c> thresholds, retention-aware
/// cleanup of rotated files, fan-out to <c>logs</c>-pipe and
/// <c>Logs.Tail*</c> subscribers, and per-worker routing all land
/// in later commits of Phase 2 — this row introduces the ingest
/// channel and the active-file writer, nothing more. Row 5
/// reshapes the drain loop into a dispatcher that fans each
/// drained record out to two inner sinks (file + broadcaster);
/// the service keeps owning the single drain loop.
/// </para>
/// </remarks>
internal sealed partial class LogFileSinkService : BackgroundService
{
    /// <summary>Basename of the active engine log file.</summary>
    internal const string EngineLogFileName = "engine.log";

    private static readonly byte[] LineTerminator = "\n"u8.ToArray();

    private readonly LogChannel _channel;
    private readonly TaskCompletionSource _executeStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string _filePath;
    private readonly ILogger<LogFileSinkService> _logger;

    /// <summary>
    /// Creates a new <see cref="LogFileSinkService"/> targeted at
    /// the per-instance subtree derived from <paramref name="options"/>.
    /// The target path is composed eagerly so the drain loop's
    /// hot path skips path work.
    /// </summary>
    /// <param name="channel">Ingest channel to drain.</param>
    /// <param name="options">Engine options carrying the workspace
    /// path, instance id, and optional cache-root override the
    /// target path is derived from.</param>
    /// <param name="logger">Diagnostic sink for I/O failures inside
    /// the drain loop.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public LogFileSinkService(
        LogChannel channel,
        IOptions<EngineOptions> options,
        ILogger<LogFileSinkService> logger)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _channel = channel;
        _logger = logger;
        _filePath = ComposeEngineLogPath(options.Value);
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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Signal StartAsync that the drain loop has taken
        // ownership of the channel — see the StartAsync override
        // for the cancellation race this guards against.
        _executeStarted.TrySetResult();

        var directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stream = new FileStream(
            _filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        await using var configured = stream.ConfigureAwait(false);

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
                    ProtocolJsonContext.Default.LogRecord);

                await stream.WriteAsync(bytes, CancellationToken.None).ConfigureAwait(false);
                await stream.WriteAsync(LineTerminator, CancellationToken.None).ConfigureAwait(false);
                await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogAppendFailed(_logger, _filePath, ex);
            }
        }
    }

    private static string ComposeEngineLogPath(EngineOptions options)
    {
        var cacheRoot = EngineCacheRoot.Resolve(options.CacheRootOverride);
        var workspaceHash = WorkspaceHash.Compute(options.WorkspacePath).Value;

        return Path.Combine(
            cacheRoot,
            workspaceHash,
            options.InstanceId.ToString("D"),
            EngineCrashWriter.LogsSubdirectory,
            EngineLogFileName);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Failed to append log record to {FilePath}.")]
    private static partial void LogAppendFailed(ILogger logger, string filePath, Exception exception);
}
