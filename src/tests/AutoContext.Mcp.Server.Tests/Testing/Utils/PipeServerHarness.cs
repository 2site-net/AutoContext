namespace AutoContext.Mcp.Server.Tests.Testing.Utils;

using System.IO.Pipes;

using AutoContext.Mcp.Server.Workers.Transport;
using AutoContext.Worker.Hosting;

internal static class PipeServerHarness
{
    public static string UniqueEndpoint() =>
        $"autocontext-test-{Guid.NewGuid():N}";

    /// <summary>
    /// Returns a unique, schema-valid worker <c>id</c> (kebab-case,
    /// lowercase) for tests that drive flows through
    /// <c>McpWorker</c> whose endpoint is derived from the id via
    /// <see cref="EndpointFormatter.Format"/>.
    /// </summary>
    public static string UniqueWorkerId() =>
        $"test-{Guid.NewGuid():N}";

    /// <summary>Composes the pipe name production code would derive for <paramref name="workerId"/>.</summary>
    public static string PipeNameFor(string workerId) =>
        EndpointFormatter.Format(workerId);

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
