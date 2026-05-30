namespace AutoContext.Mcp.Server.Tests.Support.Config;

using System.IO.Pipes;

using AutoContext.Framework.Pipes;
using AutoContext.Mcp.Server.Config;
using AutoContext.Mcp.Server.Tests.Support.Config;
using AutoContext.Mcp.Server.Tests.Support.Shared;

/// <summary>
/// Pipe-server harness for <c>AutoContextConfigClient</c> tests: spins
/// up a one-shot named-pipe server that, on a single client
/// connection, writes <paramref name="frames"/> in order (each as a
/// length-prefixed message) and then holds the connection open until
/// the test signals release via the supplied
/// <see cref="TaskCompletionSource"/>.
/// </summary>
internal static class AutoContextConfigPipeServerHarness
{
    public static Task RunServerAsync(
        string pipeName,
        IReadOnlyList<JsonAutoContextConfigSnapshot> frames,
        TaskCompletionSource release,
        CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            var server = PipeServerHarness.Create(pipeName, PipeDirection.Out);

            await using (server.ConfigureAwait(false))
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                var channel = new LengthPrefixedFrameCodec(server);

                foreach (var frame in frames)
                {
                    await channel.WriteAsync(AutoContextConfigSnapshotTestSerializer.SerializeDto(frame), cancellationToken).ConfigureAwait(false);
                }

                using (cancellationToken.Register(() => release.TrySetResult()))
                {
                    await release.Task.ConfigureAwait(false);
                }
            }
        }, cancellationToken);
}
