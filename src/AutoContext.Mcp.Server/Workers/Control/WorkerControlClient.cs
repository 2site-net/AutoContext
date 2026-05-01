namespace AutoContext.Mcp.Server.Workers.Control;

using System.Collections.Concurrent;
using System.Text.Json;

using AutoContext.Framework.Transport;
using AutoContext.Mcp.Server.Workers.Protocol;
using AutoContext.Mcp.Server.Workers.Transport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Client for the extension's worker-control named pipe. Sends an
/// <see cref="EnsureRunningRequest"/> per worker before the
/// orchestrator dispatches a tool call so the extension can spawn or
/// confirm the target worker.
/// </summary>
/// <remarks>
/// <para>
/// Wire format: 4-byte little-endian payload length followed by that
/// many UTF-8 JSON bytes — same framing as the worker task pipe.
/// Composed over <see cref="PipePersistentExchangeClient"/>, which
/// owns the persistent connection and serializes round-trips through
/// an internal lock; concurrent callers for the same <c>workerId</c>
/// coalesce here onto a single shared task so they do not each pay
/// for a separate round-trip.
/// </para>
/// <para>
/// When constructed without a pipe name (standalone runs of
/// <c>Mcp.Server</c>, smoke tests not driven by the extension)
/// <see cref="EnsureRunningAsync"/> is a no-op that completes
/// successfully — callers can wire this client unconditionally.
/// </para>
/// <para>
/// Failures (connect, write, read, parse, EOF, deadline) reset the
/// underlying connection and surface as a thrown
/// <see cref="WorkerControlException"/>. <see cref="WorkerClient"/>
/// catches and converts these into the synthesized error envelope
/// returned to MCP callers.
/// </para>
/// </remarks>
public sealed partial class WorkerControlClient : IAsyncDisposable
{
    /// <summary>Default wait deadline for connect + round-trip.</summary>
    public static readonly TimeSpan DefaultDeadline = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Shared no-op instance for convenience constructors and unit
    /// tests that exercise <see cref="WorkerClient"/> without driving
    /// the worker-control pipe. Safe to share because no-op mode never
    /// allocates pipe resources; intentionally never disposed.
    /// </summary>
    public static readonly WorkerControlClient Disabled = new();

    private readonly TimeSpan _deadline;
    private readonly ILogger<WorkerControlClient> _logger;
    private readonly PipePersistentExchangeClient? _exchange;

    /// <summary>
    /// Per-worker coalescing map. While a round-trip for
    /// <c>workerId</c> is in flight, sibling callers receive the same
    /// task; the entry is removed once the task settles so the next
    /// call after completion does its own round-trip.
    /// </summary>
    /// <remarks>
    /// <see cref="Lazy{T}"/> is required (rather than a bare
    /// <see cref="Task{TResult}"/>) because
    /// <see cref="ConcurrentDictionary{TKey, TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/>
    /// can invoke its factory more than once under contention &mdash;
    /// without <see cref="Lazy{T}"/> two callers racing for the same
    /// worker would each kick off a real round-trip even though only
    /// one would be stored. <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>
    /// guarantees the factory runs at most once.
    /// </remarks>
    private readonly ConcurrentDictionary<string, Lazy<Task<EnsureRunningResponse>>> _inFlight = new();

    private bool _disposed;

    public WorkerControlClient()
        : this(pipeName: null, DefaultDeadline, NullLogger<WorkerControlClient>.Instance)
    {
    }

    public WorkerControlClient(string? pipeName)
        : this(pipeName, DefaultDeadline, NullLogger<WorkerControlClient>.Instance)
    {
    }

    public WorkerControlClient(string? pipeName, ILogger<WorkerControlClient> logger)
        : this(pipeName, DefaultDeadline, logger)
    {
    }

    public WorkerControlClient(string? pipeName, TimeSpan deadline, ILogger<WorkerControlClient> logger)
    {
        if (deadline <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deadline),
                deadline,
                "Deadline must be positive.");
        }

        ArgumentNullException.ThrowIfNull(logger);

        _deadline = deadline;
        _logger = logger;

        if (!string.IsNullOrEmpty(pipeName))
        {
            var transport = new PipeTransport(NullLogger<PipeTransport>.Instance);
            _exchange = new PipePersistentExchangeClient(
                transport,
                pipeName,
                NullLogger<PipePersistentExchangeClient>.Instance);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when this client has no pipe to
    /// talk to (standalone runs / smoke tests). In that mode
    /// <see cref="EnsureRunningAsync"/> succeeds without doing any
    /// work.
    /// </summary>
    public bool IsNoOp => _exchange is null;

    /// <summary>
    /// Asks the extension to ensure the worker identified by
    /// <paramref name="workerId"/> is running. Concurrent callers for
    /// the same id coalesce onto the same round-trip. No-op when the
    /// client was constructed without a pipe name.
    /// </summary>
    /// <param name="workerId">
    /// Short worker id from <c>mcp-workers-registry.json</c>
    /// (e.g. <c>"workspace"</c>). Must not be null/empty.
    /// </param>
    /// <param name="ct">Caller cancellation token.</param>
    /// <exception cref="WorkerControlException">
    /// The extension reported a spawn failure, or the round-trip
    /// failed (connect/write/read/parse/EOF/deadline).
    /// </exception>
    public async Task EnsureRunningAsync(string workerId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsNoOp)
        {
            return;
        }

        // Coalesce concurrent calls for the same worker. The shared
        // task is removed from the map *after* it completes so the next
        // caller (after the worker has been confirmed once) still gets
        // a fresh round-trip — important when a worker exits between
        // calls. The round-trip itself runs with the client's own
        // deadline (see RunEnsureRunningAsync) — never with any single
        // caller's CancellationToken — so caller A canceling its token
        // does not poison caller B's view of the result. Per-caller
        // cancellation is applied at the await via Task.WaitAsync(ct).
        var lazy = _inFlight.GetOrAdd(
            workerId,
            id => new Lazy<Task<EnsureRunningResponse>>(
                () => RunEnsureRunningAsync(id),
                LazyThreadSafetyMode.ExecutionAndPublication));

        var task = lazy.Value;

        EnsureRunningResponse response;
        try
        {
            response = await task.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _inFlight.TryRemove(
                new KeyValuePair<string, Lazy<Task<EnsureRunningResponse>>>(workerId, lazy));
        }

        if (!string.Equals(response.Status, EnsureRunningResponse.StatusReady, StringComparison.Ordinal))
        {
            var message = response.Error ?? "Worker control reported an unspecified failure.";
            LogEnsureFailed(_logger, workerId, message);
            throw new WorkerControlException(workerId, message);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        if (_exchange is not null)
        {
            await _exchange.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<EnsureRunningResponse> RunEnsureRunningAsync(string workerId)
    {
        // Underlying round-trip runs only against the client's own
        // deadline. Per-caller cancellation is layered on at
        // EnsureRunningAsync via Task.WaitAsync(ct) so a single caller
        // bailing out does not abort sibling callers coalesced onto
        // the same task.
        using var deadlineCts = new CancellationTokenSource(_deadline);
        var token = deadlineCts.Token;

        var request = new EnsureRunningRequest { WorkerId = workerId };
        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, WorkerJsonOptions.Instance);

        try
        {
            var responseBytes = await _exchange!.ExchangeAsync(requestBytes, token).ConfigureAwait(false);

            var response = JsonSerializer.Deserialize<EnsureRunningResponse>(
                responseBytes, WorkerJsonOptions.Instance)
                ?? throw new JsonException("Worker-control response payload was null.");

            return response;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException
            or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            throw new WorkerControlException(workerId, ex.Message, ex);
        }
        catch (OperationCanceledException) when (deadlineCts.IsCancellationRequested)
        {
            throw new WorkerControlException(
                workerId,
                $"Worker-control round-trip exceeded the {_deadline.TotalSeconds:0.##}s deadline.");
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Worker-control EnsureRunning('{WorkerId}') failed: {Message}")]
    private static partial void LogEnsureFailed(ILogger logger, string workerId, string message);
}

/// <summary>
/// Thrown by <see cref="WorkerControlClient.EnsureRunningAsync"/> when
/// the round-trip cannot be completed (connect/write/read/parse/EOF/
/// deadline) or the extension reports a spawn failure. Carries the
/// <see cref="WorkerId"/> of the worker the call was about so callers
/// can include it in error envelopes.
/// </summary>
public sealed class WorkerControlException : Exception
{
    public WorkerControlException(string workerId, string message)
        : base(message)
    {
        WorkerId = workerId;
    }

    public WorkerControlException(string workerId, string message, Exception inner)
        : base(message, inner)
    {
        WorkerId = workerId;
    }

    public string WorkerId { get; }
}
