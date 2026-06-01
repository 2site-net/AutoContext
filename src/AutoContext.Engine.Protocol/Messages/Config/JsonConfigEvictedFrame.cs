namespace AutoContext.Engine.Protocol.Messages.Config;

using System.Text.Json.Serialization;

/// <summary>
/// Terminal <see cref="JsonConfigStreamFrame"/> arm the engine emits
/// to a slow <c>Config.Subscribe</c> subscriber immediately before
/// closing the stream. Carries the eviction reason so the peer can
/// distinguish a slow-subscriber kick from a normal EOF.
/// </summary>
/// <remarks>
/// <para>
/// Currently the only emitted reason is
/// <see cref="SlowSubscriberReason"/>; the field is a free-form
/// string so a future eviction trigger can be added without bumping
/// the protocol version.
/// </para>
/// </remarks>
public sealed record JsonConfigEvictedFrame : JsonConfigStreamFrame
{
    /// <summary>
    /// Wire reason string for the slow-subscriber eviction path.
    /// </summary>
    public const string SlowSubscriberReason = "slow-subscriber";

    /// <summary>
    /// Creates a new <see cref="JsonConfigEvictedFrame"/>.
    /// </summary>
    /// <param name="reason">Human-readable eviction reason.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="reason"/> is <see langword="null"/> or empty.
    /// </exception>
    public JsonConfigEvictedFrame(string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        Reason = reason;
    }

    /// <summary>
    /// Human-readable eviction reason (e.g.
    /// <see cref="SlowSubscriberReason"/>).
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; }
}
