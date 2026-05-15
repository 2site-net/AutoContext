namespace AutoContext.Framework.Workers.Tests.Fakes;

using System.Text.Json;

using AutoContext.Framework.Workers;

internal sealed class ThrowingTaskFake : IMcpTask
{
    public string TaskName => "boom";

    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("kaboom");
}
