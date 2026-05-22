namespace AutoContext.Engine.Core.Tests.Support.Lifecycle;

using System.Text.Json;

using AutoContext.Engine.Protocol.Messages.Lifecycle;
using AutoContext.Engine.Protocol.Serialization;

internal static class LifecycleNotificationTestDecoder
{
    public static LifecycleEvent Decode(byte[]? frame)
    {
        Assert.NotNull(frame);

        var notification = JsonSerializer.Deserialize(
            frame, ProtocolJsonContext.Default.JsonRpcNotification);
        Assert.NotNull(notification);
        Assert.Equal(LifecycleMethods.Notification, notification.Method);
        Assert.NotNull(notification.Params);

        var evt = notification.Params.Value.Deserialize(
            ProtocolJsonContext.Default.LifecycleEvent);
        Assert.NotNull(evt);

        return evt;
    }
}
