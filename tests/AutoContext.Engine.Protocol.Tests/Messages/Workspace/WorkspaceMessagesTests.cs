namespace AutoContext.Engine.Protocol.Tests.Messages.Workspace;

using System.Text.Json;

using AutoContext.Engine.Protocol.Messages.Workspace;
using AutoContext.Engine.Protocol.Serialization;

public sealed class WorkspaceMessagesTests
{
    [Fact]
    public void Should_expose_detect_method_constant_matching_design()
    {
        // The wire identifier is part of the contract; this guards
        // against accidental rename. See design § RPC surface.
        Assert.Equal("Workspace.Detect", WorkspaceMethods.Detect);
    }

    [Fact]
    public void Should_expose_info_method_constant_matching_design()
    {
        Assert.Equal("Workspace.Info", WorkspaceMethods.Info);
    }

    [Fact]
    public void Should_serialize_detect_result_with_camelCase_flags_and_extensions()
    {
        // Arrange
        var result = new JsonWorkspaceDetectResult
        {
            Flags = new JsonWorkspaceFlags { HasDotNet = true, HasCSharp = true },
            Extensions = ["cs", "csproj"],
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            result, ProtocolJsonContext.Default.JsonWorkspaceDetectResult);

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var flags = root.GetProperty("flags");

        // Assert
        Assert.Multiple(
            () => Assert.True(flags.GetProperty("hasDotNet").GetBoolean()),
            () => Assert.True(flags.GetProperty("hasCSharp").GetBoolean()),
            () => Assert.False(flags.GetProperty("hasPython").GetBoolean()),
            () => Assert.Equal(
                ["cs", "csproj"],
                root.GetProperty("extensions").EnumerateArray().Select(e => e.GetString())));
    }

    [Fact]
    public void Should_round_trip_detect_result()
    {
        // Arrange
        var result = new JsonWorkspaceDetectResult
        {
            Flags = new JsonWorkspaceFlags { HasTypeScript = true, HasVitest = true, HasWebTesting = true },
            Extensions = ["ts", "tsx"],
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            result, ProtocolJsonContext.Default.JsonWorkspaceDetectResult);
        var roundTripped = JsonSerializer.Deserialize(
            bytes, ProtocolJsonContext.Default.JsonWorkspaceDetectResult);

        // Assert
        Assert.NotNull(roundTripped);
        Assert.Multiple(
            () => Assert.Equal(result.Flags, roundTripped!.Flags),
            () => Assert.Equal(result.Extensions, roundTripped!.Extensions));
    }

    [Fact]
    public void Should_serialize_info_result_with_camelCase_fields()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var result = new JsonWorkspaceInfoResult
        {
            EngineVersion = "0.9.5",
            IdleTimeout = TimeSpan.FromSeconds(300),
            InstanceId = instanceId,
            InstanceLabel = "primary",
            Revision = 42,
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            result, ProtocolJsonContext.Default.JsonWorkspaceInfoResult);

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        // Assert
        Assert.Multiple(
            () => Assert.Equal("0.9.5", root.GetProperty("engineVersion").GetString()),
            () => Assert.Equal(instanceId, root.GetProperty("instanceId").GetGuid()),
            () => Assert.Equal("primary", root.GetProperty("instanceLabel").GetString()),
            () => Assert.Equal(42, root.GetProperty("revision").GetInt64()),
            () => Assert.Equal("00:05:00", root.GetProperty("idleTimeout").GetString()));
    }

    [Fact]
    public void Should_round_trip_info_result()
    {
        // Arrange
        var result = new JsonWorkspaceInfoResult
        {
            EngineVersion = "0.9.5",
            IdleTimeout = TimeSpan.Zero,
            InstanceId = Guid.NewGuid(),
            InstanceLabel = "primary",
            Revision = 7,
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            result, ProtocolJsonContext.Default.JsonWorkspaceInfoResult);
        var roundTripped = JsonSerializer.Deserialize(
            bytes, ProtocolJsonContext.Default.JsonWorkspaceInfoResult);

        // Assert
        Assert.Equal(result, roundTripped);
    }
}
