namespace AutoContext.Client.Core.Engine;

using System.Diagnostics;
using System.Globalization;

using AutoContext.Client.Core.Engine.Rpc;

using Microsoft.Extensions.Logging;

/// <summary>
/// Production <see cref="IEngineSpawner"/>: launches a detached
/// <c>autocontext-engine</c> with <c>Process.Start</c>. The spawned
/// engine is not a child in any meaningful sense — its stdio is
/// redirected away from the host console and its lifetime is governed
/// by its own idle timer and its other clients, not by the process
/// that happened to start it. Signals to the host do not propagate to
/// it.
/// </summary>
public sealed partial class EngineSpawner : IEngineSpawner
{
    private readonly ILogger<EngineSpawner> _logger;

    /// <summary>
    /// Creates a new <see cref="EngineSpawner"/>.
    /// </summary>
    /// <param name="logger">Logger for spawn diagnostics. Must not be
    /// <see langword="null"/>.</param>
    public EngineSpawner(ILogger<EngineSpawner> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <inheritdoc />
    public Task SpawnAsync(EngineSpawnRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = request.EngineBinaryPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.ArgumentList.Add("--workspace");
        startInfo.ArgumentList.Add(request.WorkspacePath);
        startInfo.ArgumentList.Add("--instance-id");
        startInfo.ArgumentList.Add(request.InstanceId.ToString("D"));

        if (request.InstanceLabel.Length > 0)
        {
            startInfo.ArgumentList.Add("--instance-label");
            startInfo.ArgumentList.Add(request.InstanceLabel);
        }

        if (request.IdleTimeout is { } idleTimeout)
        {
            startInfo.ArgumentList.Add("--idle-timeout");
            startInfo.ArgumentList.Add(
                ((long)idleTimeout.TotalSeconds).ToString(CultureInfo.InvariantCulture));
        }

        Process? process = null;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new EngineUnavailableException(
                    $"Starting the engine binary at '{request.EngineBinaryPath}' produced no process.");
            LogSpawned(_logger, process.Id, request.EngineBinaryPath);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new EngineUnavailableException(
                $"Failed to start the engine binary at '{request.EngineBinaryPath}'.", ex);
        }
        finally
        {
            // Dispose our handle so the engine runs detached: the OS
            // process keeps running while the redirected stdio pipes
            // close, which the daemon role tolerates (it logs to files,
            // never stdout).
            process?.Dispose();
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Spawned engine process {ProcessId} from '{BinaryPath}'.")]
    private static partial void LogSpawned(ILogger logger, int processId, string binaryPath);
}
