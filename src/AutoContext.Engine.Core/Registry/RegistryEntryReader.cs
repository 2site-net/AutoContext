namespace AutoContext.Engine.Core.Registry;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;
using AutoContext.Engine.Protocol.Messages.Registry;

using Microsoft.Extensions.Logging;

/// <summary>
/// Reads <c>engine-registry.json</c> entries and classifies each
/// as <see cref="RegistryEntryProbeState.Live"/> or
/// <see cref="RegistryEntryProbeState.Stale"/> by probing the
/// recorded <see cref="JsonRegistryEntry.ProcessId"/> through
/// <see cref="IProcessLookup"/> and comparing
/// <see cref="IProcessHandle.StartTimeUtc"/> against the entry's
/// <see cref="JsonRegistryEntry.ProcessStartTimeUtc"/> to defeat pid
/// recycling.
/// </summary>
/// <remarks>
/// <para>
/// Composes over <see cref="RegistryFileReader"/> — the file
/// mechanics (retry, corrupt-file tolerance, schema-version
/// observation) all live there. This entry reader only layers
/// the <see cref="System.Diagnostics.Process.StartTime"/> peer-
/// liveness check on top of the entries it gets back.
/// </para>
/// <para>
/// Stateless: instantiate freely (singleton in production DI; per
/// call in tests). Owns no OS handles — each <see cref="IProcessHandle"/>
/// returned by the lookup is disposed before this method returns.
/// </para>
/// <para>
/// This type supplies the registration half of the
/// <c>CacheRootScanner</c> classification. The cache-root walk,
/// <c>SubtreeRegistryStatus</c> shape, and any deletion are
/// downstream concerns owned by other types.
/// </para>
/// </remarks>
internal sealed partial class RegistryEntryReader
{
    /// <summary>
    /// Tolerance applied when comparing the live process's
    /// <see cref="IProcessHandle.StartTimeUtc"/> against the
    /// entry's <see cref="JsonRegistryEntry.ProcessStartTimeUtc"/>.
    /// Both values originate from <see cref="System.Diagnostics.Process.StartTime"/>
    /// after <c>ToUniversalTime()</c>, so they should agree
    /// exactly for the same process; the window absorbs any drift
    /// introduced by JSON roundtripping or platform-specific
    /// <see cref="System.Diagnostics.Process.StartTime"/> jitter.
    /// </summary>
    private static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(1);

    private readonly RegistryFileReader _fileReader;
    private readonly ILogger<RegistryEntryReader> _logger;
    private readonly IProcessLookup _processLookup;

    /// <summary>
    /// Creates a new entry reader composing over
    /// <paramref name="fileReader"/> and
    /// <paramref name="processLookup"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any argument is
    /// <see langword="null"/>.</exception>
    public RegistryEntryReader(
        RegistryFileReader fileReader,
        IProcessLookup processLookup,
        ILogger<RegistryEntryReader> logger)
    {
        ArgumentNullException.ThrowIfNull(fileReader);
        ArgumentNullException.ThrowIfNull(processLookup);
        ArgumentNullException.ThrowIfNull(logger);

        _fileReader = fileReader;
        _processLookup = processLookup;
        _logger = logger;
    }

    /// <summary>
    /// Reads the current registry snapshot and returns each entry
    /// tagged with its liveness verdict. A missing, empty, or
    /// malformed registry file yields an empty list — the underlying
    /// <see cref="RegistryFileReader"/> swallows corruption,
    /// and there is nothing to classify on an empty list.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Frozen list of entries paired with their liveness
    /// verdicts, in the order the file reader returned them.</returns>
    /// <exception cref="IOException">The underlying
    /// <see cref="RegistryFileReader"/> exhausted its retry
    /// budget without acquiring a shared read handle.</exception>
    public async Task<IReadOnlyList<RegistryEntryProbeResult>> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        var entries = await _fileReader.ReadAsync(cancellationToken).ConfigureAwait(false);

        if (entries.Count == 0)
        {
            return [];
        }

        var results = new List<RegistryEntryProbeResult>(entries.Count);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(new RegistryEntryProbeResult(entry, ClassifyEntry(entry)));
        }

        return results;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Registry entry {InstanceId} marked Stale: pid {ProcessId} recycled (entry start {EntryStart:o}, live start {LiveStart:o}).")]
    private static partial void LogEntryPidRecycled(
        ILogger logger,
        Guid instanceId,
        int processId,
        DateTimeOffset entryStart,
        DateTimeOffset liveStart);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Registry entry {InstanceId} marked Stale: process {ProcessId} not running.")]
    private static partial void LogEntryProcessGone(ILogger logger, Guid instanceId, int processId);

    private RegistryEntryProbeState ClassifyEntry(JsonRegistryEntry entry)
    {
        var handle = _processLookup.TryOpen(entry.ProcessId);

        if (handle is null)
        {
            LogEntryProcessGone(_logger, entry.InstanceId, entry.ProcessId);
            return RegistryEntryProbeState.Stale;
        }

        try
        {
            // Both sides went through Process.StartTime.ToUniversalTime();
            // SpecifyKind shields against a fake handle exposing a
            // Kind=Unspecified value in tests without altering the
            // production path (SystemProcessHandle already returns Utc).
            var handleStart = new DateTimeOffset(
                DateTime.SpecifyKind(handle.StartTimeUtc, DateTimeKind.Utc));
            var delta = (handleStart - entry.ProcessStartTimeUtc).Duration();

            if (delta <= StartTimeTolerance)
            {
                return RegistryEntryProbeState.Live;
            }

            LogEntryPidRecycled(
                _logger,
                entry.InstanceId,
                entry.ProcessId,
                entry.ProcessStartTimeUtc,
                handleStart);
            return RegistryEntryProbeState.Stale;
        }
        finally
        {
            handle.Dispose();
        }
    }
}
