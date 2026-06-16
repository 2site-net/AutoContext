namespace AutoContext.Engine.Core.Workers;

using System.ComponentModel;
using System.Diagnostics;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;

/// <summary>
/// Production <see cref="IProcess"/> wrapping a
/// <see cref="Process"/>. Redirects both standard
/// streams: stderr lines are forwarded to the supplied
/// <see cref="IProcessObserver"/> (the manager logs them for
/// diagnostics) and stdout is drained and discarded so a full pipe
/// buffer can never block the worker. The single terminal exit is
/// forwarded exactly once.
/// </summary>
internal sealed class WorkerProcess : IProcess
{
    private int _exitForwarded;
    private readonly IProcessObserver _observer;
    private readonly Process _process;
    private readonly WorkerProcessInfo _processInfo;

    /// <summary>
    /// Creates a new <see cref="WorkerProcess"/> for
    /// <paramref name="processInfo"/> without starting it. Call
    /// <see cref="Start"/> to launch.
    /// </summary>
    /// <param name="processInfo">The resolved launch specification.</param>
    /// <param name="observer">The sink for stderr and exit
    /// notifications.</param>
    /// <exception cref="ArgumentNullException">Any argument is
    /// <see langword="null"/>.</exception>
    public WorkerProcess(WorkerProcessInfo processInfo, IProcessObserver observer)
    {
        ArgumentNullException.ThrowIfNull(processInfo);
        ArgumentNullException.ThrowIfNull(observer);

        _processInfo = processInfo;
        _observer = observer;

        var startInfo = new ProcessStartInfo
        {
            FileName = processInfo.Command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in processInfo.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        _process.ErrorDataReceived += OnErrorDataReceived;
        _process.OutputDataReceived += OnOutputDataReceived;
        _process.Exited += OnProcessExited;
    }

    /// <inheritdoc/>
    public int? ProcessId { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _process.ErrorDataReceived -= OnErrorDataReceived;
        _process.OutputDataReceived -= OnOutputDataReceived;
        _process.Exited -= OnProcessExited;
        _process.Dispose();
    }

    /// <inheritdoc/>
    public void Kill()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process has already exited; nothing to terminate.
        }
        catch (Win32Exception)
        {
            // The OS refused the termination (the process is racing its
            // own exit); treat as already gone.
        }
    }

    /// <summary>
    /// Starts the process and begins pumping its redirected streams.
    /// </summary>
    /// <exception cref="ProcessLaunchException{T}">
    /// The OS could not start the process.</exception>
    public void Start()
    {
        try
        {
            if (!_process.Start())
            {
                throw new ProcessLaunchException<WorkerProcessInfo>(
                    _processInfo,
                    $"The OS reused an existing process instead of starting worker '{_processInfo.WorkerId}'.");
            }
        }
        catch (Win32Exception exception)
        {
            throw new ProcessLaunchException<WorkerProcessInfo>(_processInfo, exception.Message, exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new ProcessLaunchException<WorkerProcessInfo>(_processInfo, exception.Message, exception);
        }

        ProcessId = TryReadProcessId();
        _process.BeginErrorReadLine();
        _process.BeginOutputReadLine();
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
        {
            _observer.OnStandardErrorLine(e.Data);
        }
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        // Drain stdout so a full OS pipe buffer can never block the
        // worker. The engine consumes worker output over the log pipe,
        // not stdout, so the data itself is discarded here.
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _exitForwarded, 1) != 0)
        {
            return;
        }

        _observer.OnExited(TryReadExitCode());
    }

    private int? TryReadExitCode()
    {
        try
        {
            return _process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private int? TryReadProcessId()
    {
        try
        {
            return _process.Id;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
