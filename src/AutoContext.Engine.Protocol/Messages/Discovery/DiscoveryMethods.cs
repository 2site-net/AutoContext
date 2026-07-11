namespace AutoContext.Engine.Protocol.Messages.Discovery;

/// <summary>
/// JSON-RPC method-name constants for the <c>Discovery.*</c> family —
/// the engine's prompt- and tool-routing surface. Both methods read
/// indices the engine already owns (the <c>category → MCP tool</c>
/// inversion of the tool catalog and the <c>extension → instructions
/// file</c> projection of each file's <c>applyTo</c>) and answer with
/// the strongly-relevant tools and instructions files, filtered by the
/// current disabled state. Grouped here so handlers and transports
/// share one spelling of each dotted method name per
/// <c>design § RPC surface (Discovery.*)</c>.
/// </summary>
public static class DiscoveryMethods
{
    /// <summary>
    /// Prompt-routing RPC. Scans the caller's prompt for category words
    /// (word-boundary literal match) and file extensions
    /// (<c>\.&lt;ext&gt;</c> regex) and returns the matched categories and
    /// extensions together with the strongly-relevant MCP tools and
    /// instructions files. Purely prompt-driven — it answers "what did the
    /// user reference", never "what is in the workspace" (that narrowing
    /// belongs to <c>Instructions.List</c>). Takes
    /// <see cref="JsonDiscoveryRouteForPromptParams"/>; returns
    /// <see cref="JsonDiscoveryRouteForPromptResult"/>.
    /// </summary>
    public const string RouteForPrompt = "Discovery.RouteForPrompt";

    /// <summary>
    /// Tool-routing RPC. Maps a tool identity to the instructions files
    /// whose domain it shares, via the workspace-context activation flags
    /// both the tool and each instructions file declare (e.g.
    /// <c>analyze_csharp_code</c> → the <c>hasDotNet</c>/<c>hasCSharp</c>
    /// family). Takes <see cref="JsonDiscoveryRouteForToolParams"/>;
    /// returns <see cref="JsonDiscoveryRouteForToolResult"/>.
    /// </summary>
    public const string RouteForTool = "Discovery.RouteForTool";
}
