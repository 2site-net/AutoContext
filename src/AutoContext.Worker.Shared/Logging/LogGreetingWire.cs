namespace AutoContext.Worker.Logging;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape for the greeting line every <see cref="LogServerClient"/>
/// sends as the very first NDJSON line on the pipe — lets the extension
/// route subsequent records to the per-worker output channel.
/// </summary>
internal sealed record LogGreetingWire(
    [property: JsonPropertyName("clientId")] string ClientId);
