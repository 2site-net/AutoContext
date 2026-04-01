namespace SharpPilot.Mcp.DotNet.Tools.Checkers;

/// <summary>
/// Common contract for all code and commit quality checkers.
/// </summary>
public interface IChecker
{
    /// <summary>
    /// The MCP tool name used for toggle lookup in <c>.sharppilot.json</c>.
    /// </summary>
    string ToolName { get; }

    /// <summary>
    /// Analyses <paramref name="content"/> and returns a report string
    /// starting with ✅ (pass) or ❌ (violations found).
    /// </summary>
    Task<string> CheckAsync(string content, IReadOnlyDictionary<string, string>? data = null);
}
