namespace AutoContext.Framework.Tests.Support.Pipes;

using System.IO.Pipes;

using static AutoContext.Framework.Tests.Support.Encodings.TestEncodings;

/// <summary>
/// Stream-level helpers used by named-pipe tests that drive the
/// server side inline (drain to disconnect, pump bytes into a sink,
/// read a single client-id frame).
/// </summary>
public static class PipeStreamTestExtensions
{
    /// <summary>
    /// Server-side dispose can hang when the server is still CONNECTED
    /// with no observed disconnect. Reads once after the client has
    /// gone so the server transitions cleanly; swallows the expected
    /// I/O and cancellation faults that arise from doing so.
    /// </summary>
    public static async Task DrainToDisconnectAsync(this NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);

        var sink = new byte[64];
        try
        {
            _ = await server.ReadAsync(sink, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Pumps bytes from <paramref name="server"/> into <paramref name="captured"/>
    /// until the peer closes the stream or <paramref name="cancellationToken"/>
    /// fires. Used by streaming-client tests that need to assert on the
    /// raw byte sequence the production code wrote.
    /// </summary>
    public static async Task ServeIntoAsync(
        this NamedPipeServerStream server,
        MemoryStream captured,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(captured);

        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[256];
        while (true)
        {
            int read;
            try
            {
                read = await server.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }

            if (read == 0)
            {
                break;
            }
            await captured.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the unframed client-id payload written by
    /// <c>WorkerHealthMonitorService.StartAsync</c>: up to 64 bytes,
    /// returning when the client falls silent for ~100ms or a 5s total
    /// budget expires. Returns the UTF-8 decoded id.
    /// </summary>
    public static async Task<string> ReadClientIdAsync(this Stream server, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);

        var buffer = new byte[64];
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            using var readTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
            readTimeoutCts.CancelAfter(TimeSpan.FromMilliseconds(100));
            try
            {
                var read = await server.ReadAsync(buffer.AsMemory(totalRead), readTimeoutCts.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                totalRead += read;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        return Utf8NoBom.GetString(buffer, 0, totalRead);
    }
}
