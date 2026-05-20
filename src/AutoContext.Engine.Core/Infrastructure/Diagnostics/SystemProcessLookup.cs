namespace AutoContext.Engine.Core.Infrastructure.Diagnostics;

using System.ComponentModel;
using System.Diagnostics;

/// <summary>
/// Production <see cref="IProcessLookup"/> that resolves pids via
/// <see cref="Process.GetProcessById(int)"/>. Returns
/// <see langword="null"/> for any of the OS error shapes the caller
/// treats uniformly as "parent already gone".
/// </summary>
internal sealed class SystemProcessLookup : IProcessLookup
{
    /// <inheritdoc/>
    public IProcessHandle? TryOpen(int processId)
    {
        Process? process = null;
        try
        {
            process = Process.GetProcessById(processId);
            // Touch StartTime once up-front: it can throw
            // Win32Exception (access denied, process gone between
            // the lookup and the read) or InvalidOperationException
            // (process exited before we could query). Capturing the
            // value here means SystemProcessHandle never observes a
            // mid-flight failure on the property access.
            var startTimeUtc = process.StartTime.ToUniversalTime();
            return new SystemProcessHandle(process, startTimeUtc);
        }
        catch (ArgumentException)
        {
            // No live process with that pid.
            process?.Dispose();
            return null;
        }
        catch (InvalidOperationException)
        {
            // Process exited between GetProcessById and StartTime.
            process?.Dispose();
            return null;
        }
        catch (Win32Exception)
        {
            // OS denied access to the process metadata.
            process?.Dispose();
            return null;
        }
    }
}
