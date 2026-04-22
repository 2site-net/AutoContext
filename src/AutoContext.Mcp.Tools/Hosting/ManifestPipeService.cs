namespace AutoContext.Mcp.Tools.Hosting;

using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Text;

using AutoContext.Worker.Hosting;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosts the <c>autocontext.mcp-tools-manifest</c> named pipe and serves
/// the raw <c>.mcp-tools.json</c> bytes (as embedded in the assembly) to
/// any client that connects.
/// </summary>
/// <remarks>
/// Wire framing matches the rest of the worker pipe protocol:
/// length-prefixed UTF-8 JSON via <see cref="PipeFraming.WriteMessageAsync"/>.
/// One connection = one read of the manifest. Connections are served
/// sequentially: clients waiting for a busy server are queued by the OS
/// (see <see cref="NamedPipeServerStream.MaxAllowedServerInstances"/>)
/// and admitted on the next loop iteration — there is no need to fan
/// out per-connection tasks for a payload measured in microseconds.
/// </remarks>
public sealed partial class ManifestPipeService : BackgroundService
{
    /// <summary>The well-known pipe name advertised to consumers.</summary>
    public const string PipeName = "autocontext.mcp-tools-manifest";

    private readonly byte[] _manifestBytes;
    private readonly ILogger<ManifestPipeService> _logger;

    public ManifestPipeService(IManifestSource manifestSource, ILogger<ManifestPipeService> logger)
    {
        ArgumentNullException.ThrowIfNull(manifestSource);
        ArgumentNullException.ThrowIfNull(logger);

        _manifestBytes = Encoding.UTF8.GetBytes(manifestSource.Json);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!await ServeOneAsync(stoppingToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Creates the pipe server, waits for one client, writes the manifest,
    /// and disposes the stream. Returns <c>false</c> when the loop should
    /// stop (cancellation or unrecoverable creation failure).
    /// </summary>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification =
            "CA2000 cannot trace the assign-in-try → await-using-after-try flow. " +
            "The only path from a successful 'new NamedPipeServerStream' to method " +
            "exit is through the immediately-following 'await using' block.")]
    private async Task<bool> ServeOneAsync(CancellationToken ct)
    {
        NamedPipeServerStream server;

        try
        {
            server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.Out,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogPipeCreateFailed(_logger, ex, PipeName);
            return false;
        }

        await using (server.ConfigureAwait(false))
        {
            try
            {
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            await TryWriteAsync(server, ct).ConfigureAwait(false);
            return true;
        }
    }

    private async Task TryWriteAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        try
        {
            await PipeFraming.WriteMessageAsync(server, _manifestBytes, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutdown — drop silently.
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            LogPipeWriteFailed(_logger, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to create manifest pipe server '{PipeName}'.")]
    private static partial void LogPipeCreateFailed(ILogger logger, Exception ex, string pipeName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Manifest pipe write failed; client disconnected.")]
    private static partial void LogPipeWriteFailed(ILogger logger, Exception ex);
}
