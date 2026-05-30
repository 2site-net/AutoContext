namespace AutoContext.Mcp.Server.Tests.Support.Config;

using System.Text.Json;

using AutoContext.Mcp.Server.Config;

/// <summary>
/// JSON serializer for <see cref="JsonAutoContextConfigSnapshot"/>
/// frames written to the in-process pipe server in the
/// <c>AutoContextConfigClient</c> tests.
/// </summary>
internal static class AutoContextConfigSnapshotTestSerializer
{
    public static byte[] SerializeDto(JsonAutoContextConfigSnapshot dto) =>
        JsonSerializer.SerializeToUtf8Bytes(dto);
}
