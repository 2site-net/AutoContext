namespace AutoContext.Engine.Protocol.Tests.Messages.Lifecycle;

using System.Text.Json;

using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Lifecycle;
using AutoContext.Engine.Protocol.Serialization;

public sealed class LifecycleEventMessagesTests
{
    [Fact]
    public void Should_expose_kebab_case_event_kind_constants()
    {
        Assert.Multiple(
            () => Assert.Equal("started", LifecycleEventKinds.Started),
            () => Assert.Equal("reloading", LifecycleEventKinds.Reloading),
            () => Assert.Equal("reloaded", LifecycleEventKinds.Reloaded),
            () => Assert.Equal("shutting-down", LifecycleEventKinds.ShuttingDown),
            () => Assert.Equal("dropped", LifecycleEventKinds.Dropped));
    }

    [Fact]
    public void Should_expose_method_constants_matching_design()
    {
        Assert.Multiple(
            () => Assert.Equal("Engine.Lifecycle.Subscribe", LifecycleMethods.Subscribe),
            () => Assert.Equal("Engine.Lifecycle", LifecycleMethods.Notification));
    }

    [Fact]
    public void Should_omit_optional_fields_on_dropped_terminal_frame()
    {
        // Arrange
        var evt = new JsonLifecycleEvent
        {
            Kind = LifecycleEventKinds.Dropped,
            Reason = "slow-subscriber",
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            evt, ProtocolJsonContext.Default.JsonLifecycleEvent);

        // Assert
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        Assert.Multiple(
            () => Assert.Equal("dropped", root.GetProperty("kind").GetString()),
            () => Assert.Equal("slow-subscriber", root.GetProperty("reason").GetString()),
            () => Assert.False(root.TryGetProperty("instanceId", out _)),
            () => Assert.False(root.TryGetProperty("revision", out _)));
    }

    [Fact]
    public void Should_round_trip_lifecycle_notification_envelope()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var payload = new JsonLifecycleEvent
        {
            Kind = LifecycleEventKinds.ShuttingDown,
            InstanceId = instanceId,
            Revision = 0,
        };
        var paramsElement = JsonSerializer.SerializeToElement(
            payload, ProtocolJsonContext.Default.JsonLifecycleEvent);
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

        // Assert
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var decoded = roundTripped?.Params?.Deserialize(
            ProtocolJsonContext.Default.JsonLifecycleEvent);

        Assert.Multiple(
            () => Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString()),
            () => Assert.Equal("Engine.Lifecycle", root.GetProperty("method").GetString()),
            () => Assert.False(root.TryGetProperty("id", out _)),
            () => Assert.NotNull(roundTripped),
            () => Assert.Equal(JsonRpcVersion.Value, roundTripped!.JsonRpc),
            () => Assert.Equal(LifecycleMethods.Notification, roundTripped!.Method),
            () => Assert.NotNull(decoded),
            () => Assert.Equal(LifecycleEventKinds.ShuttingDown, decoded!.Kind),
            () => Assert.Equal(instanceId, decoded!.InstanceId));
    }

    [Fact]
    public void Should_serialize_lifecycle_event_with_camelCase_fields()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var evt = new JsonLifecycleEvent
        {
            Kind = LifecycleEventKinds.Started,
            InstanceId = instanceId,
            Revision = 0,
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            evt, ProtocolJsonContext.Default.JsonLifecycleEvent);

        // Assert
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        Assert.Multiple(
            () => Assert.Equal("started", root.GetProperty("kind").GetString()),
            () => Assert.Equal(instanceId, root.GetProperty("instanceId").GetGuid()),
            () => Assert.Equal(0, root.GetProperty("revision").GetInt64()),
            () => Assert.False(root.TryGetProperty("reason", out _)));
    }
}
