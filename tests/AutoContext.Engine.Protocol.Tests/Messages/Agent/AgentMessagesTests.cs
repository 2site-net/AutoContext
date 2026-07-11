namespace AutoContext.Engine.Protocol.Tests.Messages.Agent;

using System.Text.Json;

using AutoContext.Engine.Protocol.Messages.Agent;
using AutoContext.Engine.Protocol.Serialization;

public sealed class AgentMessagesTests
{
    [Fact]
    public void Should_expose_notification_method_constants_matching_design()
        => Assert.Multiple(
            () => Assert.Equal("Agent.SubagentStarted", AgentMethods.SubagentStarted),
            () => Assert.Equal("Agent.SubagentStopped", AgentMethods.SubagentStopped),
            () => Assert.Equal("Agent.Compacted", AgentMethods.Compacted),
            () => Assert.Equal("Agent.ToolUsed", AgentMethods.ToolUsed),
            () => Assert.Equal("Agent.TurnEnded", AgentMethods.TurnEnded),
            () => Assert.Equal("Agent.Events.Subscribe", AgentMethods.EventsSubscribe));

    [Fact]
    public void Should_expose_event_kind_constants_in_kebab_case()
        => Assert.Multiple(
            () => Assert.Equal("subagent-started", AgentEventKinds.SubagentStarted),
            () => Assert.Equal("subagent-stopped", AgentEventKinds.SubagentStopped),
            () => Assert.Equal("compacted", AgentEventKinds.Compacted),
            () => Assert.Equal("tool-used", AgentEventKinds.ToolUsed),
            () => Assert.Equal("turn-ended", AgentEventKinds.TurnEnded),
            () => Assert.Equal("dropped", AgentEventKinds.Dropped));

    [Fact]
    public void Should_serialize_event_with_camelCase_keys_and_omit_absent_fields()
    {
        var agentEvent = new JsonAgentEvent
        {
            Kind = AgentEventKinds.SubagentStarted,
            SessionId = "s-1",
            TaskPrompt = "port this to c#",
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            agentEvent, ProtocolJsonContext.Default.JsonAgentEvent);

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        Assert.Multiple(
            () => Assert.Equal("subagent-started", root.GetProperty("kind").GetString()),
            () => Assert.Equal("s-1", root.GetProperty("sessionId").GetString()),
            () => Assert.Equal("port this to c#", root.GetProperty("taskPrompt").GetString()),
            // Absent fields are omitted by the WhenWritingNull policy.
            () => Assert.False(root.TryGetProperty("toolName", out _)),
            () => Assert.False(root.TryGetProperty("outcome", out _)),
            () => Assert.False(root.TryGetProperty("reason", out _)));
    }

    [Fact]
    public void Should_round_trip_a_tool_used_event()
    {
        var agentEvent = new JsonAgentEvent
        {
            Kind = AgentEventKinds.ToolUsed,
            ToolName = "analyze_csharp_code_style",
            Outcome = "success",
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            agentEvent, ProtocolJsonContext.Default.JsonAgentEvent);
        var round = JsonSerializer.Deserialize(bytes, ProtocolJsonContext.Default.JsonAgentEvent);

        Assert.Equal(agentEvent, round);
    }

    [Fact]
    public void Should_deserialize_subagent_started_params_from_camelCase()
    {
        const string json = """{"sessionId":"s-9","taskPrompt":"fix the parser"}""";

        var parameters = JsonSerializer.Deserialize(
            json, ProtocolJsonContext.Default.JsonAgentSubagentStartedParams);

        Assert.Multiple(
            () => Assert.Equal("s-9", parameters!.SessionId),
            () => Assert.Equal("fix the parser", parameters!.TaskPrompt));
    }

    [Fact]
    public void Should_deserialize_tool_used_params_from_camelCase()
    {
        const string json = """{"sessionId":"s-4","toolName":"analyze_nuget_references","outcome":"error"}""";

        var parameters = JsonSerializer.Deserialize(
            json, ProtocolJsonContext.Default.JsonAgentToolUsedParams);

        Assert.Multiple(
            () => Assert.Equal("s-4", parameters!.SessionId),
            () => Assert.Equal("analyze_nuget_references", parameters!.ToolName),
            () => Assert.Equal("error", parameters!.Outcome));
    }
}
