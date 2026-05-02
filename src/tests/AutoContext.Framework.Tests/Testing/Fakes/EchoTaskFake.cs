namespace AutoContext.Framework.Tests.Testing.Fakes;

using System.Text.Json;

using AutoContext.Mcp;

internal sealed class EchoTaskFake : IMcpTask
{
    public string TaskName => "echo";

    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken cancellationToken) =>
        Task.FromResult(data.Clone());
}
