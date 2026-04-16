namespace AutoContext.Mcp.Shared;

using System.IO.Pipes;
using System.Text;

/// <summary>
/// Keeps a persistent named-pipe connection to the extension's health
/// monitor.  The connection signals "I'm alive" for the given MCP server
/// category; when the process exits, the OS closes the socket and the
/// monitor detects the disconnect.
/// </summary>
internal sealed class HealthMonitorClient : IDisposable
{
    private NamedPipeClientStream? _pipe;

    /// <summary>
    /// Connects to the health monitor pipe and sends the category name.
    /// The connection is kept alive until <see cref="Dispose"/> is called
    /// or the process exits.
    /// </summary>
    public async Task ConnectAsync(string pipeName, string category)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000).ConfigureAwait(false);

        var bytes = Encoding.UTF8.GetBytes(category);
        await pipe.WriteAsync(bytes).ConfigureAwait(false);
        await pipe.FlushAsync().ConfigureAwait(false);

        _pipe = pipe;
    }

    public void Dispose()
    {
        _pipe?.Dispose();
        _pipe = null;
    }
}
