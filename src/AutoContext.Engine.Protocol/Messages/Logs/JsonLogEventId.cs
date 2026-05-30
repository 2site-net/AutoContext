namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape of the optional <c>eventId</c> object on a
/// <see cref="JsonLogRecord"/>. Mirrors
/// <see cref="Microsoft.Extensions.Logging.EventId"/> on the
/// producer side without taking a dependency on the logging
/// abstractions package: the protocol assembly stays free of
/// runtime dependencies and the wire shape stays independent of
/// the engine-internal shape.
/// </summary>
/// <remarks>
/// Source: the <c>Engine.WriteLog</c> record shape under
/// <c>design § RPC surface</c>. Producers project from
/// <c>EventId</c> to this DTO at the seam where the record is
/// shaped for the wire.
/// </remarks>
public sealed record JsonLogEventId
{
    /// <summary>
    /// Numeric event id assigned by the producer. Matches the
    /// <c>id</c> component of
    /// <see cref="Microsoft.Extensions.Logging.EventId"/>.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>
    /// Optional symbolic name. Producers may set this to the
    /// constant or member name the numeric id was minted for;
    /// absent when the producer minted a bare numeric id.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
