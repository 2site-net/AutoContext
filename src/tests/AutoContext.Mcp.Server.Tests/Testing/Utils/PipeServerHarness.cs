namespace AutoContext.Mcp.Server.Tests.Testing.Utils;

using System.IO.Pipes;

using AutoContext.Mcp.Server.Workers.Transport;
using AutoContext.Framework.Workers;
using AutoContext.Framework.Transport;

internal static class PipeServerHarness
{
    public static string UniquePipeName() =>
        $"autocontext-test-{Guid.NewGuid():N}";

    /// <summary>
    /// Returns a unique service role for tests that drive requests
    /// through <c>WorkerClient</c> / <c>EditorConfigBatcher</c>, both
    /// of which format the pipe address from the role via
    /// <see cref="ServiceAddressFormatter.Format"/>.
    /// </summary>
    public static string UniqueRole() =>
        $"test-{Guid.NewGuid():N}";

    /// <summary>Returns the pipe address production code derives for <paramref name="role"/>.</summary>
    public static string AddressFor(string role) =>
        ServiceAddressFormatter.Format(role, instanceId: null);

    /// <summary>
    /// Returns a unique, schema-valid worker <c>id</c> (kebab-case,
    /// lowercase) for tests that drive flows through
    /// <c>McpWorker</c> whose listen address is derived from the id
    /// via <see cref="ServiceAddressFormatter.Format"/>.
    /// </summary>
    public static string UniqueWorkerId() =>
        $"test-{Guid.NewGuid():N}";

    /// <summary>Composes the pipe name production code would derive for <paramref name="workerId"/>.</summary>
    public static string PipeNameFor(string workerId) =>
        ServiceAddressFormatter.Format($"worker-{workerId}", instanceId: null);

    public static Task RunOneShotAsync(
        string pipeName,
        Func<byte[], byte[]?> handler,
        CancellationToken ct) =>
        Task.Run(
            () => HandleOneAsync(pipeName, maxInstances: 1, handler, ct),
            ct);

    /// <summary>
    /// Spins up <paramref name="connectionCount"/> server instances on the
    /// same pipeName, each handling one request synchronously via
    /// <paramref name="handler"/>. The handler may be invoked concurrently
    /// — implementations must be thread-safe.
    /// </summary>
    public static Task RunMultiAsync(
        string pipeName,
        int connectionCount,
        Func<byte[], byte[]?> handler,
        CancellationToken ct)
    {
        var pending = new Task[connectionCount];

        for (var i = 0; i < connectionCount; i++)
        {
            pending[i] = Task.Run(
                () => HandleOneAsync(pipeName, connectionCount, handler, ct),
                ct);
        }

        return Task.WhenAll(pending);
    }

    private static async Task HandleOneAsync(
        string pipeName,
        int maxInstances,
        Func<byte[], byte[]?> handler,
        CancellationToken ct)
    {
        var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: maxInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        await using (server.ConfigureAwait(false))
        {
            await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

            var channel = new LengthPrefixedFrameCodec(server);
            var requestBytes = await channel.ReadAsync(ct).ConfigureAwait(false);
            if (requestBytes is null)
            {
                return;
            }

            var responseBytes = handler(requestBytes);
            if (responseBytes is null)
            {
                return;
            }

            await channel.WriteAsync(responseBytes, ct).ConfigureAwait(false);
        }
    }
}
