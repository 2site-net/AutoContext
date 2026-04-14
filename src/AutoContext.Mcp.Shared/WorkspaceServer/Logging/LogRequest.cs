namespace AutoContext.Mcp.Shared.WorkspaceServer.Logging;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Request to send a log message to the workspace service for centralized output.
/// </summary>
/// <param name="Source">Server identity (e.g. "DotNet", "Git", "TypeScript").</param>
/// <param name="Level">Log level (e.g. "Information", "Warning").</param>
/// <param name="Message">The log message text.</param>
internal sealed record LogRequest(string Source, string Level, string Message)
{
    /// <summary>Gets the request type discriminator.</summary>
    [SuppressMessage("Performance", "CA1822",
        Justification = "Must be an instance property for JSON serialization.")]
    public string Type
        => "log";
}
