namespace AutoContext.Mcp.Server.Tests.Support.Workers;

using AutoContext.Framework.Pipes;
using AutoContext.Mcp.Server.Tests.Support.Shared;

/// <summary>
/// Pipe-server harness used by <c>WorkerClient</c> timeout tests: spins
/// up a server that accepts a single request frame then blocks on
/// <paramref name="gate"/> indefinitely, simulating an unresponsive
/// worker.
/// </summary>
internal static class HangingWorkerPipeServerHarness
{
    public static Task RunHangingServerAsync(string pipeName, CancellationToken gate) =>
        Task.Run(
            async () =>
            {
                var server = PipeServerHarness.Create(pipeName);

                await using (server.ConfigureAwait(false))
                {
                    await server.WaitForConnectionAsync(gate).ConfigureAwait(false);
                    var channel = new LengthPrefixedFrameCodec(server);
                    _ = await channel.ReadAsync(gate).ConfigureAwait(false);

                    try
                    {
                        await Task.Delay(Timeout.Infinite, gate).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            },
            gate);
}
