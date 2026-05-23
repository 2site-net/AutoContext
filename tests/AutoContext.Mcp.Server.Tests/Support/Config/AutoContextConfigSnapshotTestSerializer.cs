namespace AutoContext.Mcp.Server.Tests.Support.Config;

using System.Text.Json;

using AutoContext.Mcp.Server.Config;

/// <summary>
/// JSON serializer for <see cref="AutoContextConfigSnapshotDto"/>
/// frames written to the in-process pipe server in the
/// <c>AutoContextConfigClient</c> tests.
/// </summary>
internal static class AutoContextConfigSnapshotTestSerializer
{
    public static byte[] SerializeDto(AutoContextConfigSnapshotDto dto) =>
        JsonSerializer.SerializeToUtf8Bytes(dto);
}
