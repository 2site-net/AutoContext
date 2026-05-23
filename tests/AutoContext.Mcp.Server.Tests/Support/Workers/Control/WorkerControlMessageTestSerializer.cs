namespace AutoContext.Mcp.Server.Tests.Support.Workers.Control;

using System.Text.Json;

using AutoContext.Mcp.Server.Workers.Control;
using AutoContext.Mcp.Server.Workers.Protocol;

/// <summary>
/// JSON helpers for the <see cref="EnsureRunningRequest"/> /
/// <see cref="EnsureRunningResponse"/> protocol pair used by the
/// <c>WorkerControlClient</c> tests.
/// </summary>
internal static class WorkerControlMessageTestSerializer
{
    public static EnsureRunningRequest DeserializeRequest(byte[] bytes) =>
        JsonSerializer.Deserialize<EnsureRunningRequest>(bytes, WorkerJsonOptions.Instance)
            ?? throw new InvalidOperationException("Null request payload.");

    public static byte[] SerializeResponse(EnsureRunningResponse response) =>
        JsonSerializer.SerializeToUtf8Bytes(response, WorkerJsonOptions.Instance);
}
