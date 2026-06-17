namespace AutoContext.Engine.Tests.Support.Diagnostics;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Pipes;

using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Tests.Support.IO;

/// <summary>
/// Spawns a single <c>autocontext-engine</c> binary in daemon role
/// and blocks until its <see cref="EndpointKind.Rpc"/> pipe is
/// connectable — the engine's atomic four-pipe bind guarantees that
/// once <c>rpc</c> is reachable every other endpoint is too.
/// Captures stderr for diagnostics and kills the process on
/// <see cref="DisposeAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// Usage is construct, configure <see cref="Options"/>, then
/// <see cref="SpawnAsync"/>. Each instance owns exactly one engine
/// process; spawn a separate instance per engine and dispose each
/// (typically with <c>await using</c>) to reap it.
/// </para>
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
/// The <see cref="EngineTestProcessOptions"/> testing defaults pin
/// <c>--idle-timeout 0</c> so the idle gate cannot race the test
/// budget and <c>--parent-pid &lt;current process&gt;</c> so a
/// crashed run cannot leak a stale engine.
/// </para>
/// </remarks>
public sealed class EngineTestProcess : IAsyncDisposable
{
    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ConnectPollTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly List<string> _stderrLines = [];
    private readonly Lock _stderrLock = new();

    private Process? _process;
    private bool _spawned;

    /// <summary>
    /// CLI-shaped spawn configuration. Populate before calling
    /// <see cref="SpawnAsync"/>; the value is snapshotted at spawn,
    /// so later mutation has no effect on the running process.
    /// </summary>
    public EngineTestProcessOptions Options { get; set; } = new();

    /// <summary>Workspace path the engine was spawned against (resolved at spawn).</summary>
    public string WorkspacePath { get; private set; } = string.Empty;

    /// <summary>Instance id the engine was spawned with (resolved at spawn).</summary>
    public Guid InstanceId { get; private set; }

    /// <summary>Underlying engine <see cref="Process"/> for exit-code inspection and graceful waits.</summary>
    public Process Process =>
        _process ?? throw new InvalidOperationException(
            "The engine has not been spawned yet; call SpawnAsync first.");

    /// <summary>Snapshot of every stderr line the engine has written so far.</summary>
    public IReadOnlyList<string> StandardErrorLines
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
        Justification = "Process ownership transfers to this EngineTestProcess, which disposes it via DisposeAsync. Failure paths kill and dispose the process explicitly before throwing.")]
    public async Task<EngineTestProcess> SpawnAsync(CancellationToken cancellationToken)
    {
        if (_spawned)
        {
            throw new InvalidOperationException(
                "This EngineTestProcess has already been spawned; create a new instance per engine.");
        }

        _spawned = true;
        var options = Options;
        var workspacePath = options.WorkspacePath ?? WorkspaceTestDirectoryFactory.Create();
        var instanceId = options.InstanceId ?? Guid.NewGuid();
        WorkspacePath = workspacePath;
        InstanceId = instanceId;

        var executablePath = EngineBinaryPath.Value;
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                "autocontext-engine binary not found. Run '.\\build.ps1 Compile DotNet' before running engine integration tests.",
                executablePath);
        }

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
            var args = process.StartInfo.ArgumentList;
            args.Add("--workspace");
            args.Add(workspacePath);
            args.Add("--instance-id");
            args.Add(instanceId.ToString("D"));
            args.Add("--idle-timeout");
            args.Add(((int)options.IdleTimeout.TotalSeconds).ToString(CultureInfo.InvariantCulture));

            if (options.ParentProcessId is { } parentProcessId)
            {
                args.Add("--parent-pid");
                args.Add(parentProcessId.ToString(CultureInfo.InvariantCulture));
            }

            if (options.CacheRootOverride is { } cacheRoot)
            {
                args.Add("--cache-root");
                args.Add(cacheRoot);
            }

            if (options.ResourcesRootOverride is { } resourcesRoot)
            {
                args.Add("--resources-root");
                args.Add(resourcesRoot);
            }

            if (options.Retention is { } retention)
            {
                args.Add("--retention");
                args.Add(retention);
            }

            foreach (var arg in options.ExtraArguments)
            {
                args.Add(arg);
            }

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                lock (_stderrLock)
                {
                    _stderrLines.Add(e.Data);
                }
            };

            if (!process.Start())
            {
                throw new InvalidOperationException(
                    $"Failed to start '{executablePath}'.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await WaitForReadinessAsync(process, workspacePath, instanceId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await KillAsync(process).ConfigureAwait(false);
            process.Dispose();
            throw;
        }

        _process = process;
        return this;
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is not null)
        {
            await KillAsync(_process).ConfigureAwait(false);
            _process.Dispose();
        }
    }

    private async Task WaitForReadinessAsync(
        Process process,
        string workspacePath,
        Guid instanceId,
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
                    $"autocontext-engine did not bind the rpc pipe '{pipeName}' within {ReadinessTimeout.TotalSeconds:0}s. Stderr:{System.Environment.NewLine}{SnapshotStderr()}");
            }

            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"autocontext-engine exited (code {process.ExitCode}) before binding the rpc pipe. Stderr:{System.Environment.NewLine}{SnapshotStderr()}");
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

    private string SnapshotStderr()
    {
        lock (_stderrLock)
        {
            return _stderrLines.Count == 0
                ? "(no stderr)"
                : string.Join(System.Environment.NewLine, _stderrLines);
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
