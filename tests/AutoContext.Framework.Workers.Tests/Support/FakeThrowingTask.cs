namespace AutoContext.Framework.Workers.Tests.Support;

using System.Text.Json;

using AutoContext.Framework.Workers;

internal sealed class FakeThrowingTask : IMcpTask
{
    public string TaskName => "boom";

    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("kaboom");
}
