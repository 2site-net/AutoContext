namespace AutoContext.Mcp.Tools.Tests.Testing.Utils;

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
            async () =>
            {
                var server = new NamedPipeServerStream(
                    endpoint,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
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
            },
            ct);
}
