namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// Terminal <see cref="JsonLogStreamFrame"/> arm the engine writes to
/// a slow <c>logs</c>-pipe subscriber immediately before closing
/// the connection. Carries the drop reason so the peer can
/// distinguish a slow-subscriber kick from a normal EOF.
/// </summary>
/// <remarks>
/// <para>
/// Currently the only emitted reason is
/// <see cref="SlowSubscriberReason"/>; the field is a free-form
/// string so a future drop trigger can be added without bumping
/// the protocol version.
/// </para>
/// </remarks>
public sealed record JsonLogDroppedFrame : JsonLogStreamFrame
{
    /// <summary>
    /// Wire reason string for the slow-subscriber drop path.
    /// </summary>
    public const string SlowSubscriberReason = "slow-subscriber";

    /// <summary>
    /// Creates a new <see cref="JsonLogDroppedFrame"/>.
    /// </summary>
    /// <param name="reason">Human-readable drop reason.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="reason"/> is <see langword="null"/> or empty.
    /// </exception>
    public JsonLogDroppedFrame(string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        Reason = reason;
    }

    /// <summary>
    /// Human-readable drop reason (e.g.
    /// <see cref="SlowSubscriberReason"/>).
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; }
}
