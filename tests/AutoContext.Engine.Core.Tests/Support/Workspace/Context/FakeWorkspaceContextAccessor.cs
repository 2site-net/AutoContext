namespace AutoContext.Engine.Core.Tests.Support.Workspace.Context;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure;
using AutoContext.Engine.Core.Workspace.Context;

/// <summary>
/// In-memory <see cref="IWorkspaceContextAccessor"/> test double that
/// holds a detection result, letting tests drive the
/// <c>Workspace.Detect</c> RPC path without spinning up a stateful
/// <see cref="WorkspaceContextDetector"/> (no workspace scan, no file
/// watcher, nothing to dispose). The held value is settable so a test
/// can stage the exact result the handler should project.
/// </summary>
internal sealed class FakeWorkspaceContextAccessor : IWorkspaceContextAccessor
{
    public WorkspaceDetectionResult Current { get; set; } = WorkspaceDetectionResult.Empty;

    public IWorkspaceEngineInfo EngineInfo { get; set; } = new FakeWorkspaceEngineInfo();

    public long Revision { get; set; }
}
