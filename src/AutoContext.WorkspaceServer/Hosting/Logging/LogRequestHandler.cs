namespace AutoContext.WorkspaceServer.Hosting.Logging;

using System.Text.Json;

/// <summary>
/// Handles <c>"log"</c> pipe requests by writing the message to stderr,
/// which the extension captures and forwards to the Output channel.
/// </summary>
internal sealed class LogRequestHandler : IRequestHandler
{
    private static readonly byte[] AckResponse =
        JsonSerializer.SerializeToUtf8Bytes(new { }, WorkspaceService.JsonOptions);

    public string RequestType
        => "log";

    public byte[] Process(ReadOnlySpan<byte> json)
    {
        using var doc = JsonDocument.Parse(json.ToArray());

        var source = doc.RootElement.TryGetProperty("source", out var s) ? s.GetString() : "Unknown";
        var message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;

        if (message is not null)
        {
            Console.Error.WriteLine($"[{source}] {message}");
        }

        return AckResponse;
    }
}
