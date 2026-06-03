namespace AutoContext.Engine.Core.Tests.Support.Workspace.Context;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure;

/// <summary>
/// In-memory <see cref="IWorkspaceEngineInfo"/> test double that lets
/// tests stage the workspace path, instance identity/label, and idle
/// window a <c>WorkspaceContextDetector</c> or
/// <see cref="FakeWorkspaceContextAccessor"/> should report, without
/// materialising a full <c>EngineOptions</c> instance.
/// </summary>
internal sealed record FakeWorkspaceEngineInfo : IWorkspaceEngineInfo
{
    public TimeSpan IdleTimeout { get; init; } = EngineOptions.DefaultIdleTimeout;

    public Guid InstanceId { get; init; } = Guid.NewGuid();

    public string InstanceLabel { get; init; } = string.Empty;

    public string WorkspacePath { get; init; } = string.Empty;
}
