namespace AutoContext.Engine.Core.Tests.Support.Workspace.Context;

using AutoContext.Engine.Core.Workspace.Context;

using Microsoft.Extensions.Logging.Abstractions;

public static class WorkspaceContextDetectorTestFactory
{
    internal static WorkspaceContextDetector Create(string workspacePath)
        => new(
            new FakeWorkspaceEngineInfo { WorkspacePath = workspacePath },
            WorkspaceDetectionRules.FileRules,
            WorkspaceDetectionRules.ContentScans,
            WorkspaceDetectionRules.FlagActivationEdges,
            TimeProvider.System,
            WorkspaceContextDetector.DefaultDebounceDelay,
            NullLogger<WorkspaceContextDetector>.Instance);

    internal static WorkspaceContextDetector Create(string workspacePath, TimeSpan debounceDelay)
        => new(
            new FakeWorkspaceEngineInfo { WorkspacePath = workspacePath },
            WorkspaceDetectionRules.FileRules,
            WorkspaceDetectionRules.ContentScans,
            WorkspaceDetectionRules.FlagActivationEdges,
            TimeProvider.System,
            debounceDelay,
            NullLogger<WorkspaceContextDetector>.Instance);
}
