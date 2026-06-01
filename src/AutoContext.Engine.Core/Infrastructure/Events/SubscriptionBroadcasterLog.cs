namespace AutoContext.Engine.Core.Infrastructure.Events;

using Microsoft.Extensions.Logging;

/// <summary>
/// Source-generated diagnostics shared by every
/// <see cref="SubscriptionBroadcaster{TPayload, TSubscription}"/>.
/// Lives on a concrete, non-generic type because the
/// <c>[LoggerMessage]</c> source generator does not emit for methods
/// declared in open generic types; the per-domain channel label is
/// supplied as the <c>{Channel}</c> structured property.
/// </summary>
internal static partial class SubscriptionBroadcasterLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Evicted {EvictedCount} slow {Channel} subscriber(s) after bounded buffer overflow.")]
    internal static partial void SubscribersEvicted(
        ILogger logger, int evictedCount, string channel);
}
