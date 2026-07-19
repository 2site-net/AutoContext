namespace AutoContext.Engine.Core.McpServer.Tools;

using System.Collections.Generic;

/// <summary>
/// Produces the <see cref="IMcpTool"/> leaves for one tool family. The
/// adapter concatenates every registered source into its flat routing map,
/// so exposing a new family is registering a source — never editing the
/// adapter or duplicating routing logic.
/// </summary>
internal interface IMcpToolSource
{
    /// <summary>Returns this family's tools.</summary>
    IReadOnlyList<IMcpTool> GetTools();
}
