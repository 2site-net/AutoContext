namespace SharpPilot.Core;

using System.Text.Json.Nodes;

/// <summary>
/// Common contract for all code and commit quality checkers.
/// </summary>
public interface IChecker
{
    /// <summary>
    /// The MCP tool name used for toggle lookup in <c>tools-status.json</c>.
    /// </summary>
    string ToolName { get; }

    /// <summary>
    /// Analyses <paramref name="content"/> and returns a report string
    /// starting with ✅ (pass) or ❌ (violations found).
    /// </summary>
    string Check(string content, JsonObject? data = null);
}
