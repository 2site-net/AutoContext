namespace AutoContext.Mcp.Server.Tests.Testing.Utils;

using System.IO.Pipes;

using AutoContext.Worker.Hosting;

internal static class PipeServerHarness
{
    public static string UniqueEndpoint() =>
        $"autocontext-test-{Guid.NewGuid():N}";

    public static Task RunOneShotAsync(
        string endpoint,
        Func<byte[], byte[]?> handler,
        CancellationToken ct) =>
        Task.Run(
            () => HandleOneAsync(endpoint, maxInstances: 1, handler, ct),
            ct);

    /// <summary>
    /// Spins up <paramref name="connectionCount"/> server instances on the
    /// same endpoint, each handling one request synchronously via
    /// <paramref name="handler"/>. The handler may be invoked concurrently
    /// — implementations must be thread-safe.
    /// </summary>
    public static Task RunMultiAsync(
        string endpoint,
        int connectionCount,
        Func<byte[], byte[]?> handler,
        CancellationToken ct)
    {
        var pending = new Task[connectionCount];

        for (var i = 0; i < connectionCount; i++)
        {
            pending[i] = Task.Run(
                () => HandleOneAsync(endpoint, connectionCount, handler, ct),
                ct);
        }

        return Task.WhenAll(pending);
    }

    private static async Task HandleOneAsync(
        string endpoint,
        int maxInstances,
        Func<byte[], byte[]?> handler,
        CancellationToken ct)
    {
        var server = new NamedPipeServerStream(
            endpoint,
            PipeDirection.InOut,
            maxNumberOfServerInstances: maxInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        await using (server.ConfigureAwait(false))
        {
            await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

            var requestBytes = await PipeFraming.ReadMessageAsync(server, ct).ConfigureAwait(false);
            if (requestBytes is null)
            {
                return;
            }

            var responseBytes = handler(requestBytes);
            if (responseBytes is null)
            {
                return;
            }

            await PipeFraming.WriteMessageAsync(server, responseBytes, ct).ConfigureAwait(false);
        }
    }
}
