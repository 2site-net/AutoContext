namespace AutoContext.Workers.Core.Tests.Support;

using System.Text.Json;

using AutoContext.Workers.Core;

internal sealed class FakeThrowingTask : IMcpTask
{
    public string TaskName => "boom";

    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("kaboom");
}
