namespace AutoContext.Engine.Core.Infrastructure.Diagnostics;

using System.Diagnostics;

/// <summary>
/// Production <see cref="IProcessHandle"/> wrapping a
/// <see cref="Process"/> instance opened via
/// <see cref="Process.GetProcessById(int)"/>. Owns the
/// <see cref="Process"/> handle and releases it on
/// <see cref="Dispose"/>.
/// </summary>
internal sealed class SystemProcessHandle : IProcessHandle
{
    private readonly Process _process;

    /// <summary>
    /// Creates a handle that owns <paramref name="process"/>.
    /// <paramref name="startTimeUtc"/> is captured by the caller
    /// (<see cref="SystemProcessLookup"/>) before construction so a
    /// later access on the underlying <see cref="Process"/> cannot
    /// race the OS reclaiming the metadata.
    /// </summary>
    public SystemProcessHandle(Process process, DateTime startTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(process);

        _process = process;
        StartTimeUtc = startTimeUtc;
    }

    /// <inheritdoc/>
    public DateTime StartTimeUtc { get; }

    /// <inheritdoc/>
    public void Dispose() => _process.Dispose();

    /// <inheritdoc/>
    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        _process.WaitForExitAsync(cancellationToken);
}
