namespace AutoContext.Framework.Tests.Testing.Fakes;

using System.Text.Json;

using AutoContext.Mcp;

internal sealed class ThrowingTaskFake : IMcpTask
{
    public string TaskName => "boom";

    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("kaboom");
}
