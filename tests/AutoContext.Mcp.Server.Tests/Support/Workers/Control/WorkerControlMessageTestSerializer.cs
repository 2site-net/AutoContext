namespace AutoContext.Mcp.Server.Tests.Support.Workers.Control;

using System.Text.Json;

using AutoContext.Mcp.Server.Workers.Control;
using AutoContext.Mcp.Server.Workers.Protocol;

/// <summary>
/// JSON helpers for the <see cref="JsonEnsureRunningRequest"/> /
/// <see cref="JsonEnsureRunningResponse"/> protocol pair used by the
/// <c>WorkerControlClient</c> tests.
/// </summary>
internal static class WorkerControlMessageTestSerializer
{
    public static JsonEnsureRunningRequest DeserializeRequest(byte[] bytes) =>
        JsonSerializer.Deserialize<JsonEnsureRunningRequest>(bytes, WorkerJsonOptions.Instance)
            ?? throw new InvalidOperationException("Null request payload.");

    public static byte[] SerializeResponse(JsonEnsureRunningResponse response) =>
        JsonSerializer.SerializeToUtf8Bytes(response, WorkerJsonOptions.Instance);
}
