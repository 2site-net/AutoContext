namespace AutoContext.Engine.Core.Rpc;

using System.Text.Json;

/// <summary>
/// Helpers for the JSON-RPC 2.0 <c>id</c> field used by every
/// request/response frame on the engine's <c>rpc</c> and
/// <c>events</c> endpoints.
/// </summary>
/// <remarks>
/// JSON-RPC 2.0 requires the response to carry <c>"id": null</c>
/// when the inbound request omitted the field, rather than dropping
/// the property entirely. <see cref="Null"/> is the canonical
/// <c>null</c> element used for that case, and
/// <see cref="Normalize"/> projects an inbound id to a wire-safe
/// echo value.
/// </remarks>
internal static class JsonRpcId
{
    /// <summary>
    /// A <see cref="JsonElement"/> whose value kind is
    /// <see cref="JsonValueKind.Null"/>. Used as the response
    /// <c>id</c> when the inbound request did not carry one.
    /// </summary>
    public static JsonElement Null { get; } = JsonDocument.Parse("null").RootElement;

    /// <summary>
    /// Returns the inbound id verbatim, or <see cref="Null"/> when
    /// the inbound id was absent
    /// (<see cref="JsonValueKind.Undefined"/>).
    /// </summary>
    public static JsonElement Normalize(JsonElement id) =>
        id.ValueKind == JsonValueKind.Undefined ? Null : id;
}
