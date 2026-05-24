namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape of the optional <c>exception</c> object on a
/// <see cref="LogRecord"/>. Flattens an <see cref="System.Exception"/>
/// instance to the four fields consumers actually render — type,
/// message, stack trace, and an optional recursive inner exception
/// — without dragging the producer's CLR exception type or any
/// runtime dependency across the wire.
/// </summary>
/// <remarks>
/// Source: the <c>Engine.WriteLog</c> record shape under
/// <c>design § RPC surface</c>. Producers project a CLR exception
/// to this DTO at the seam where the record is shaped for the
/// wire; the recursive <see cref="Inner"/> field preserves the
/// chain depth-first so consumers can render nested causes
/// without inventing their own walker.
/// </remarks>
public sealed record LogExceptionInfo
{
    /// <summary>
    /// Fully-qualified CLR type name of the originating exception
    /// (e.g. <c>System.IO.IOException</c>). Used by consumers to
    /// distinguish kinds without round-tripping the type into the
    /// reader's process.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// <see cref="System.Exception.Message"/> from the originating
    /// exception. Captured at log time; never re-evaluated.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// <see cref="System.Exception.StackTrace"/> from the
    /// originating exception, captured at log time. May be empty
    /// if the producer chose not to materialise a stack (e.g. a
    /// pre-thrown sentinel).
    /// </summary>
    [JsonPropertyName("stackTrace")]
    public string StackTrace { get; init; } = string.Empty;

    /// <summary>
    /// Optional inner exception in the same flattened shape.
    /// Walks <see cref="System.Exception.InnerException"/> chains
    /// depth-first; absent when the originating exception had no
    /// inner cause.
    /// </summary>
    [JsonPropertyName("inner")]
    public LogExceptionInfo? Inner { get; init; }
}
