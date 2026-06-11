namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Terminal <see cref="JsonInstructionsStreamFrame"/> arm the engine
/// emits to a slow <see cref="InstructionsMethods.Subscribe"/>
/// subscriber immediately before closing the stream. Carries the drop
/// reason so the peer can distinguish a slow-subscriber kick from a
/// normal EOF.
/// </summary>
public sealed record JsonInstructionsDroppedFrame : JsonInstructionsStreamFrame
{
    /// <summary>Wire reason string for the slow-subscriber drop path.</summary>
    public const string SlowSubscriberReason = "slow-subscriber";

    /// <summary>
    /// Creates a new <see cref="JsonInstructionsDroppedFrame"/>.
    /// </summary>
    /// <param name="reason">Human-readable drop reason.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="reason"/> is <see langword="null"/> or empty.
    /// </exception>
    public JsonInstructionsDroppedFrame(string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        Reason = reason;
    }

    /// <summary>
    /// Human-readable drop reason (e.g. <see cref="SlowSubscriberReason"/>).
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; }
}
