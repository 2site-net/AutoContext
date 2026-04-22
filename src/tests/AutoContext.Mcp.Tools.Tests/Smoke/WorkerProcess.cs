namespace AutoContext.Mcp.Tools.Tests.Smoke;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Launches an <c>AutoContext.Worker.*</c> executable, waits for its
/// stderr ready-marker, and kills the process on <see cref="IDisposable.Dispose"/>.
/// </summary>
/// <remarks>
/// The Mcp.Tools side is driven by <c>StdioClientTransport</c>, which
/// manages its own process lifecycle. Worker processes, by contrast, are
/// long-lived pipe servers that the smoke test must spawn and tear down
/// explicitly. <see cref="StartAsync"/> returns only after the worker has
/// written its ready-marker, so callers can connect to the pipe
/// immediately on return.
/// </remarks>
internal sealed class WorkerProcess : IAsyncDisposable
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly Process _process;
    private readonly List<string> _stderrLines;
    private readonly object _stderrLock;

    private WorkerProcess(Process process, List<string> stderrLines, object stderrLock)
    {
        _process = process;
        _stderrLines = stderrLines;
        _stderrLock = stderrLock;
    }

    internal IReadOnlyList<string> StandardErrorLines
    {
        get
        {
            lock (_stderrLock)
            {
                return [.. _stderrLines];
            }
        }
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Ownership of the Process is transferred to the returned WorkerProcess, which disposes it via DisposeAsync. Failure paths below dispose the process explicitly before throwing.")]
    internal static async Task<WorkerProcess> StartAsync(
        string executablePath,
        string pipeName,
        string readyMarker,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? extraArguments = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        ArgumentException.ThrowIfNullOrEmpty(readyMarker);

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                $"Worker executable not found. Run '.\\build.ps1 Compile DotNet' before running smoke tests.",
                executablePath);
        }

        var readySignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderrLines = new List<string>();
        var stderrLock = new object();

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        try
        {
            process.StartInfo.ArgumentList.Add("--pipe");
            process.StartInfo.ArgumentList.Add(pipeName);

            if (extraArguments is not null)
            {
                foreach (var arg in extraArguments)
                {
                    process.StartInfo.ArgumentList.Add(arg);
                }
            }

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                lock (stderrLock)
                {
                    stderrLines.Add(e.Data);
                }

                if (e.Data.Contains(readyMarker, StringComparison.Ordinal))
                {
                    readySignal.TrySetResult();
                }
            };

            process.Exited += (_, _) =>
            {
                string stderrSnapshot;
                lock (stderrLock)
                {
                    stderrSnapshot = stderrLines.Count == 0
                        ? "(no stderr)"
                        : string.Join(Environment.NewLine, stderrLines);
                }

                readySignal.TrySetException(new InvalidOperationException(
                    $"Worker '{Path.GetFileName(executablePath)}' exited (code {process.ExitCode}) before emitting the ready marker. Stderr:{Environment.NewLine}{stderrSnapshot}"));
            };

            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start worker '{executablePath}'.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ReadyTimeout);

            await readySignal.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch
        {
            await KillAsync(process).ConfigureAwait(false);
            process.Dispose();
            throw;
        }

        return new WorkerProcess(process, stderrLines, stderrLock);
    }

    public async ValueTask DisposeAsync()
    {
        await KillAsync(_process).ConfigureAwait(false);
        _process.Dispose();
    }

    private static async Task KillAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var shutdownCts = new CancellationTokenSource(ShutdownTimeout);
                await process.WaitForExitAsync(shutdownCts.Token).ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited before Kill reached it.
        }
        catch (OperationCanceledException)
        {
            // Shutdown timed out; nothing more we can do from the test side.
        }
    }
}
