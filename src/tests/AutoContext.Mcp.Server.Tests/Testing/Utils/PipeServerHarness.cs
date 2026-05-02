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

    /// <summary>
    /// Creates a server stream with the framework's production
    /// listener defaults (<see cref="PipeTransmissionMode.Byte"/>,
    /// <see cref="PipeOptions.Asynchronous"/>). Tests that need
    /// imperative inline access to the server use this directly;
    /// <see cref="RunOneShotAsync"/> / <see cref="RunMultiAsync"/>
    /// build on it.
    /// </summary>
    public static NamedPipeServerStream Create(
        string pipeName,
        PipeDirection direction = PipeDirection.InOut,
        int maxInstances = 1) =>
        new(
            pipeName,
            direction,
            maxNumberOfServerInstances: maxInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

    public static Task RunOneShotAsync(
        string pipeName,
        Func<byte[], byte[]?> handler,
        CancellationToken cancellationToken) =>
        Task.Run(
            () => HandleOneAsync(pipeName, maxInstances: 1, handler, cancellationToken),
            cancellationToken);

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
        CancellationToken cancellationToken)
    {
        var pending = new Task[connectionCount];

        for (var i = 0; i < connectionCount; i++)
        {
            pending[i] = Task.Run(
                () => HandleOneAsync(pipeName, connectionCount, handler, cancellationToken),
                cancellationToken);
        }

        return Task.WhenAll(pending);
    }

    private static async Task HandleOneAsync(
        string pipeName,
        int maxInstances,
        Func<byte[], byte[]?> handler,
        CancellationToken cancellationToken)
    {
        var server = Create(pipeName, PipeDirection.InOut, maxInstances);

        await using (server.ConfigureAwait(false))
        {
            await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

            var channel = new LengthPrefixedFrameCodec(server);
            var requestBytes = await channel.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (requestBytes is null)
            {
                return;
            }

            var responseBytes = handler(requestBytes);
            if (responseBytes is null)
            {
                return;
            }

            await channel.WriteAsync(responseBytes, cancellationToken).ConfigureAwait(false);
        }
    }
}
