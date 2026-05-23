namespace AutoContext.Framework.Workers.Tests.Support;

using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Framework.Pipes;
using AutoContext.Framework.Workers;

internal static class WorkerDispatcherPipeTestClient
{
    public static async Task<JsonElement> SendAsync(string pipeName, object request, CancellationToken cancellationToken)
    {
        var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await using var _ = client.ConfigureAwait(false);
        await client.ConnectAsync(5000, cancellationToken).ConfigureAwait(false);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(request, WorkerTaskDispatcherService.WorkerJsonOptions);
        var channel = new LengthPrefixedFrameCodec(client);
        await channel.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);

        var responseBytes = await channel.ReadAsync(cancellationToken).ConfigureAwait(false);
        Assert.NotNull(responseBytes);

        return JsonDocument.Parse(responseBytes!).RootElement.Clone();
    }
}
