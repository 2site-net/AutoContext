namespace AutoContext.Worker.Shared.Tests.Testing.Fakes;

using System.Text.Json;

using AutoContext.Mcp.Abstractions;

internal sealed class EchoTaskFake : IMcpTask
{
    public string TaskName => "echo";

    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken ct) =>
        Task.FromResult(data.Clone());
}
