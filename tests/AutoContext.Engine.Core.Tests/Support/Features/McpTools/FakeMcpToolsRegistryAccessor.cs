namespace AutoContext.Engine.Core.Tests.Support.Features.McpTools;

using AutoContext.Engine.Core.Features.McpTools;
using AutoContext.Engine.Core.Features.McpTools.Snapshot;

/// <summary>
/// In-memory <see cref="IMcpToolsRegistryAccessor"/> test double that
/// exposes a fixed registry snapshot, letting tests drive the
/// <c>McpTools.*</c> handlers without a stateful
/// <see cref="McpToolsRegistryService"/> or the build-time side-cars.
/// </summary>
/// <param name="registry">The registry snapshot to expose, defaulting to
/// <see cref="McpToolsRegistry.Empty"/> when none is supplied.</param>
internal sealed class FakeMcpToolsRegistryAccessor(McpToolsRegistry? registry = null) : IMcpToolsRegistryAccessor
{
    public McpToolsRegistry Current { get; } = registry ?? McpToolsRegistry.Empty;
}
