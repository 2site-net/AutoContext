namespace AutoContext.Mcp.Server.Tests.Support.Workers.Control;

using AutoContext.Framework.Pipes;
using AutoContext.Mcp.Server.Tests.Support.Shared;
using AutoContext.Mcp.Server.Workers.Control;
using AutoContext.Mcp.Server.Workers.Protocol;

using static AutoContext.Mcp.Server.Tests.Support.Workers.Control.WorkerControlMessageTestSerializer;

/// <summary>
/// Persistent in-process pipe server harness for
/// <see cref="WorkerControlClient"/> tests: accepts a single client
/// connection and answers an arbitrary number of length-framed
/// <see cref="EnsureRunningRequest"/> messages until the client closes
/// the pipe or <paramref name="cancellationToken"/> fires.
/// </summary>
internal static class WorkerControlPipeServerHarness
{
    public static Task RunPersistentAsync(
        string pipeName,
        Func<EnsureRunningRequest, EnsureRunningResponse> handler,
        CancellationToken cancellationToken,
        Action<int>? onRequest = null) =>
        Task.Run(async () =>
        {
            var server = PipeServerHarness.Create(pipeName);

            await using (server.ConfigureAwait(false))
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                var channel = new LengthPrefixedFrameCodec(server);

                var i = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    var requestBytes = await channel.ReadAsync(cancellationToken).ConfigureAwait(false);
                    if (requestBytes is null)
                    {
                        return;
                    }

                    var request = DeserializeRequest(requestBytes);
                    onRequest?.Invoke(i++);

                    var response = handler(request);
                    var responseBytes = SerializeResponse(response);
                    await channel.WriteAsync(responseBytes, cancellationToken).ConfigureAwait(false);
                }
            }
        }, cancellationToken);
}
