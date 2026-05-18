namespace AutoContext.Engine.Protocol.Tests.Messages.Lifecycle;

using System.Text.Json;

using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Lifecycle;
using AutoContext.Engine.Protocol.Serialization;

public sealed class LifecycleEventMessagesTests
{
    [Fact]
    public void Should_serialize_lifecycle_event_with_camelCase_fields()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var evt = new LifecycleEvent
        {
            Kind = LifecycleEventKinds.Started,
            InstanceId = instanceId,
            Revision = 0,
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            evt, ProtocolJsonContext.Default.LifecycleEvent);

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        // Assert
        Assert.Multiple(
            () => Assert.Equal("started", root.GetProperty("kind").GetString()),
            () => Assert.Equal(instanceId, root.GetProperty("instanceId").GetGuid()),
            () => Assert.Equal(0, root.GetProperty("revision").GetInt64()),
            () => Assert.False(root.TryGetProperty("reason", out _)));
    }

    [Fact]
    public void Should_omit_optional_fields_on_evicted_terminal_frame()
    {
        // Arrange — the terminal eviction frame carries only kind
        // and reason; instanceId and revision are absent per design.
        var evt = new LifecycleEvent
        {
            Kind = LifecycleEventKinds.Evicted,
            Reason = "slow-subscriber",
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            evt, ProtocolJsonContext.Default.LifecycleEvent);

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        // Assert
        Assert.Multiple(
            () => Assert.Equal("evicted", root.GetProperty("kind").GetString()),
            () => Assert.Equal("slow-subscriber", root.GetProperty("reason").GetString()),
            () => Assert.False(root.TryGetProperty("instanceId", out _)),
            () => Assert.False(root.TryGetProperty("revision", out _)));
    }

    [Fact]
    public void Should_round_trip_lifecycle_notification_envelope()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var payload = new LifecycleEvent
        {
            Kind = LifecycleEventKinds.ShuttingDown,
            InstanceId = instanceId,
            Revision = 0,
        };
        var paramsElement = JsonSerializer.SerializeToElement(
            payload, ProtocolJsonContext.Default.LifecycleEvent);
        var notification = new JsonRpcNotification
        {
            Method = LifecycleMethods.Notification,
            Params = paramsElement,
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            notification, ProtocolJsonContext.Default.JsonRpcNotification);

        var roundTripped = JsonSerializer.Deserialize(
            bytes, ProtocolJsonContext.Default.JsonRpcNotification);
        Assert.NotNull(roundTripped);

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        // Assert — wire envelope has jsonrpc + method + params and
        // no id (notifications are non-replyable).
        Assert.Multiple(
            () => Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString()),
            () => Assert.Equal("Engine.Lifecycle", root.GetProperty("method").GetString()),
            () => Assert.False(root.TryGetProperty("id", out _)),
            () => Assert.Equal(JsonRpcVersion.Value, roundTripped.Jsonrpc),
            () => Assert.Equal(LifecycleMethods.Notification, roundTripped.Method));

        Assert.NotNull(roundTripped.Params);
        var decoded = roundTripped.Params.Value.Deserialize(
            ProtocolJsonContext.Default.LifecycleEvent);
        Assert.NotNull(decoded);
        Assert.Multiple(
            () => Assert.Equal(LifecycleEventKinds.ShuttingDown, decoded.Kind),
            () => Assert.Equal(instanceId, decoded.InstanceId));
    }

    [Fact]
    public void Should_expose_method_constants_matching_design()
    {
        Assert.Multiple(
            () => Assert.Equal("Engine.Lifecycle.Subscribe", LifecycleMethods.Subscribe),
            () => Assert.Equal("Engine.Lifecycle", LifecycleMethods.Notification));
    }

    [Fact]
    public void Should_expose_kebab_case_event_kind_constants()
    {
        Assert.Multiple(
            () => Assert.Equal("started", LifecycleEventKinds.Started),
            () => Assert.Equal("reloading", LifecycleEventKinds.Reloading),
            () => Assert.Equal("reloaded", LifecycleEventKinds.Reloaded),
            () => Assert.Equal("shutting-down", LifecycleEventKinds.ShuttingDown),
            () => Assert.Equal("evicted", LifecycleEventKinds.Evicted));
    }
}
