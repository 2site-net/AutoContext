namespace AutoContext.Engine.Tests.Testing.Integration;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Pipes;

using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Protocol;

/// <summary>
/// Spawns the <c>autocontext-engine</c> binary in daemon role and
/// blocks until its <see cref="EndpointKind.Rpc"/> pipe is
/// connectable — the engine's atomic four-pipe bind guarantees that
/// once <c>rpc</c> is reachable every other endpoint is too.
/// Captures stderr for diagnostics and kills the process on
/// <see cref="DisposeAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// The engine emits no stderr ready-marker today (its diagnostics
/// channel is reserved for error reporting), so this harness derives
/// readiness from a pipe probe rather than a marker scan: a
/// <see cref="NamedPipeClientStream"/> connect against the
/// <c>rpc</c> endpoint with a short per-attempt timeout, retried
/// until success or until the overall <see cref="ReadinessTimeout"/>
/// elapses.
/// </para>
/// <para>
/// Every spawned instance carries
/// <c>--parent-pid &lt;Environment.ProcessId&gt;</c> so a crashed
/// test run cannot leak a stale engine, and
/// <c>--idle-timeout 0</c> so the idle gate cannot race the test
/// budget. Extra arguments may be appended via
/// <paramref name="extraArguments"/>.
/// </para>
/// </remarks>
internal sealed class EngineProcess : IAsyncDisposable
{
    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ConnectPollTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly Process _process;
    private readonly List<string> _stderrLines;
    private readonly object _stderrLock;

    private EngineProcess(
        Process process,
        string workspacePath,
        Guid instanceId,
        List<string> stderrLines,
        object stderrLock)
    {
        _process = process;
        _stderrLines = stderrLines;
        _stderrLock = stderrLock;
        WorkspacePath = workspacePath;
        InstanceId = instanceId;
    }

    /// <summary>Workspace path passed to the engine via <c>--workspace</c>.</summary>
    internal string WorkspacePath { get; }

    /// <summary>Instance id passed to the engine via <c>--instance-id</c>.</summary>
    internal Guid InstanceId { get; }

    /// <summary>Underlying engine <see cref="Process"/> for exit-code inspection and graceful waits.</summary>
    internal Process Process => _process;

    /// <summary>Snapshot of every stderr line the engine has written so far.</summary>
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
        Justification = "Process ownership transfers to the returned EngineProcess, which disposes it via DisposeAsync. Failure paths kill and dispose the process explicitly before throwing.")]
    internal static async Task<EngineProcess> StartAsync(
        string workspacePath,
        Guid instanceId,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? extraArguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        var executablePath = EngineBinaryPath.Value;
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                "autocontext-engine binary not found. Run '.\\build.ps1 Compile DotNet' before running engine integration tests.",
                executablePath);
        }

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
            process.StartInfo.ArgumentList.Add("--workspace");
            process.StartInfo.ArgumentList.Add(workspacePath);
            process.StartInfo.ArgumentList.Add("--instance-id");
            process.StartInfo.ArgumentList.Add(instanceId.ToString("D"));
            process.StartInfo.ArgumentList.Add("--idle-timeout");
            process.StartInfo.ArgumentList.Add("0");
            process.StartInfo.ArgumentList.Add("--parent-pid");
            process.StartInfo.ArgumentList.Add(
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

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
            };

            if (!process.Start())
            {
                throw new InvalidOperationException(
                    $"Failed to start '{executablePath}'.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await WaitForReadinessAsync(
                process, workspacePath, instanceId, stderrLines, stderrLock, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await KillAsync(process).ConfigureAwait(false);
            process.Dispose();
            throw;
        }

        return new EngineProcess(process, workspacePath, instanceId, stderrLines, stderrLock);
    }

    public async ValueTask DisposeAsync()
    {
        await KillAsync(_process).ConfigureAwait(false);
        _process.Dispose();
    }

    private static async Task WaitForReadinessAsync(
        Process process,
        string workspacePath,
        Guid instanceId,
        List<string> stderrLines,
        object stderrLock,
        CancellationToken cancellationToken)
    {
        var hash = WorkspaceHash.Compute(workspacePath);
        var pipeName = new Endpoint(EndpointKind.Rpc, hash.Value, instanceId).ToString();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ReadinessTimeout);

        while (true)
        {
            // Honour caller cancellation directly; convert the
            // internal readiness-budget timeout into a diagnostic
            // TimeoutException that names the pipe and includes the
            // stderr snapshot.
            cancellationToken.ThrowIfCancellationRequested();
            if (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"autocontext-engine did not bind the rpc pipe '{pipeName}' within {ReadinessTimeout.TotalSeconds:0}s. Stderr:{Environment.NewLine}{SnapshotStderr(stderrLines, stderrLock)}");
            }

            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"autocontext-engine exited (code {process.ExitCode}) before binding the rpc pipe. Stderr:{Environment.NewLine}{SnapshotStderr(stderrLines, stderrLock)}");
            }

            var probe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            try
            {
                await probe.ConnectAsync(ConnectPollTimeout, timeoutCts.Token).ConfigureAwait(false);
                await probe.DisposeAsync().ConfigureAwait(false);
                return;
            }
            catch (TimeoutException)
            {
                await probe.DisposeAsync().ConfigureAwait(false);
                // Pipe not bound yet; loop and retry until the readiness budget elapses.
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                await probe.DisposeAsync().ConfigureAwait(false);
                // Loop back so the readiness-budget branch above produces the diagnostic message.
            }
            catch
            {
                await probe.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }

    private static string SnapshotStderr(List<string> stderrLines, object stderrLock)
    {
        lock (stderrLock)
        {
            return stderrLines.Count == 0
                ? "(no stderr)"
                : string.Join(Environment.NewLine, stderrLines);
        }
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
