namespace AutoContext.Engine.Core.Workspace.Context;

using AutoContext.Engine.Core.Infrastructure;

/// <summary>
/// Read-only seam over the latest workspace detection result. Decouples
/// detection readers — the <c>Workspace.Detect</c> RPC handler — from the
/// stateful <see cref="WorkspaceContextDetector"/> so they depend only on
/// the ability to read the current result, not on the detector's scan,
/// watch, and dispose surface.
/// </summary>
internal interface IWorkspaceContextAccessor
{
    /// <summary>
    /// The detection result currently held in memory. Each read returns
    /// an immutable value that is safe to use without locking.
    /// </summary>
    WorkspaceDetectionResult Current { get; }

    /// <summary>
    /// Engine-instance metadata — workspace path, instance
    /// identity/label, and idle timeout — bound to this accessor.
    /// </summary>
    IWorkspaceEngineInfo EngineInfo { get; }

    /// <summary>
    /// Monotonic state-version counter of the workspace snapshot held
    /// by this accessor.
    /// </summary>
    long Revision { get; }
}
