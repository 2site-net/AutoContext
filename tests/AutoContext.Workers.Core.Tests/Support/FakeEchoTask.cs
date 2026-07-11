namespace AutoContext.Workers.Core.Tests.Support;

using System.Text.Json;

using AutoContext.Workers.Core;

internal sealed class FakeEchoTask : IMcpTask
{
    public string TaskName => "echo";

    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken cancellationToken) =>
        Task.FromResult(data.Clone());
}
