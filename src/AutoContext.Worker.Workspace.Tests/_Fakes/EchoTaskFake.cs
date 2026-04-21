namespace AutoContext.Worker.Workspace.Tests._Fakes;

using System.Text.Json;

using AutoContext.Mcp.Abstractions;

internal sealed class EchoTaskFake : IMcpTask
{
    public string TaskName => "echo";

    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken ct)
    {
        return Task.FromResult(data.Clone());
    }
}
