namespace AutoContext.Framework.Logging;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape for the greeting line every <see cref="LoggingClient"/>
/// sends as the very first NDJSON line on the pipe — lets the extension
/// route subsequent records to the per-client output channel.
/// </summary>
internal sealed record JsonLogGreeting(
    [property: JsonPropertyName("clientName")] string ClientName);
