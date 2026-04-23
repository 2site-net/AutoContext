namespace AutoContext.Mcp.Server.Hosting;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Writes the stderr ready-marker — used by the parent process (the
/// VS Code extension or an integration test) to detect that the MCP
/// server is initialized — once the host has fully started.
/// </summary>
public sealed class ReadyMarkerService : IHostedLifecycleService
{
    private readonly string _marker;

    public ReadyMarkerService(string marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        _marker = marker;
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        // Write through the long-lived Console.Error TextWriter rather
        // than opening (and disposing!) a fresh stderr stream — the
        // stream returned by Console.OpenStandardError wraps the process
        // stderr handle, and disposing it is contractually fragile.
        await Console.Error.WriteLineAsync(_marker.AsMemory(), cancellationToken).ConfigureAwait(false);
        await Console.Error.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
