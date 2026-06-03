namespace AutoContext.Engine.Core.Tests.Support.Workspace.Context;

using AutoContext.Engine.Core.Workspace.Context;

public static class WorkspaceContextDetectorTestFactory
{
    internal static WorkspaceContextDetector Create(string workspacePath)
        => new(
            new FakeWorkspaceEngineInfo { WorkspacePath = workspacePath },
            WorkspaceDetectionRules.FileRules,
            WorkspaceDetectionRules.ContentScans,
            WorkspaceDetectionRules.FlagActivationEdges);

    internal static WorkspaceContextDetector Create(string workspacePath, TimeSpan debounceDelay)
        => new(
            new FakeWorkspaceEngineInfo { WorkspacePath = workspacePath },
            WorkspaceDetectionRules.FileRules,
            WorkspaceDetectionRules.ContentScans,
            WorkspaceDetectionRules.FlagActivationEdges,
            debounceDelay: debounceDelay);
}
