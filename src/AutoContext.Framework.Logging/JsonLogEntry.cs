namespace AutoContext.Framework.Logging;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape for one NDJSON log record streamed from a client to the
/// extension's LogServer. Property names are intentionally lowercased to
/// keep the serialised payload compact.
/// </summary>
internal sealed record JsonLogEntry(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("exception")] string? Exception,
    [property: JsonPropertyName("correlationId")] string? CorrelationId);
