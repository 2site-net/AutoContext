namespace AutoContext.Worker.Logging;

using System.Text.Json.Serialization;

/// <summary>
/// Source-generation context for the two NDJSON wire types — keeps the
/// LogServer transport AOT-friendly and CA1869-clean (no per-call
/// <c>JsonSerializerOptions</c> allocations).
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(JsonLogEntry))]
[JsonSerializable(typeof(JsonLogGreeting))]
internal sealed partial class LogServerJsonContext : JsonSerializerContext;
