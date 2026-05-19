namespace AutoContext.Engine.Core.Tests.Testing.Fakes;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;

/// <summary>
/// In-memory <see cref="IProcessLookup"/> used by
/// <see cref="HostWatchdog"/> tests. Returns a configured
/// <see cref="FakeProcessHandle"/> for the pid the test cares
/// about and <see langword="null"/> for every other pid (modelling
/// "no such process"). Counts lookup calls so tests can assert the
/// watchdog only probes once at startup.
/// </summary>
internal sealed class FakeProcessLookup : IProcessLookup
{
    private readonly Dictionary<int, FakeProcessHandle?> _handlesByPid = [];
    private int _tryOpenCallCount;

    public int TryOpenCallCount => Volatile.Read(ref _tryOpenCallCount);

    /// <summary>
    /// Registers <paramref name="handle"/> as the result the next
    /// <see cref="TryOpen"/> for <paramref name="pid"/> returns.
    /// Passing <see langword="null"/> models "no live process with
    /// that pid".
    /// </summary>
    public void Register(int pid, FakeProcessHandle? handle) => _handlesByPid[pid] = handle;

    /// <inheritdoc/>
    public IProcessHandle? TryOpen(int processId)
    {
        Interlocked.Increment(ref _tryOpenCallCount);
        return _handlesByPid.TryGetValue(processId, out var handle) ? handle : null;
    }
}
