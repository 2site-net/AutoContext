namespace AutoContext.Engine.Core.Tests.Support.McpServer.Tools;

using System.Collections.Generic;

using AutoContext.Engine.Core.McpServer.Tools;

/// <summary>
/// In-memory <see cref="IMcpToolSource"/> test double that yields a fixed set
/// of tools, letting the adapter's aggregation and routing be tested without
/// the real instruction or registry sources.
/// </summary>
internal sealed class FakeMcpToolSource(params IMcpTool[] tools) : IMcpToolSource
{
    public IReadOnlyList<IMcpTool> GetTools() => tools;
}
