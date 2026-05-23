namespace AutoContext.Mcp.Server.Tests.Support.Smoke;

using System.Text.Json;

using AutoContext.Mcp.Server.Tools.Results;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

/// <summary>
/// Drives an MCP tool call against a real <see cref="McpClient"/>,
/// asserts the response carries a single
/// <see cref="TextContentBlock"/>, deserializes the
/// <see cref="ToolResultEnvelope"/>, and fails the test if the
/// envelope's status is <see cref="ToolResultEnvelope.StatusError"/>.
/// Used by the end-to-end smoke suite.
/// </summary>
internal static class SmokeTestToolCaller
{
    public static async Task<ToolResultEnvelope> CallToolAsync(
        McpClient client,
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var result = await client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var textBlock = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        var envelope = JsonSerializer.Deserialize<ToolResultEnvelope>(textBlock.Text)
            ?? throw new InvalidOperationException(
                $"Tool '{toolName}' returned an empty envelope.");

        if (string.Equals(envelope.Status, ToolResultEnvelope.StatusError, StringComparison.Ordinal))
        {
            throw new Xunit.Sdk.XunitException(
                $"Tool '{toolName}' returned status='error'. Raw envelope:\n{textBlock.Text}");
        }

        return envelope;
    }
}
