namespace AutoContext.Engine.Core.Workspace.Context;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Hosted service that brings the workspace's
/// <see cref="WorkspaceContextDetector"/> online at engine start: it runs
/// the initial full scan so the in-memory result is populated before the
/// first <c>Workspace.Detect</c> RPC can land, then arms the recursive
/// filesystem watcher so later edits keep
/// <see cref="WorkspaceContextDetector.Current"/> current. The detector
/// singleton owns its own teardown (it is an <see cref="IDisposable"/> the
/// container disposes on host stop), so
/// <see cref="StopAsync(CancellationToken)"/> is a no-op. Registered
/// before <c>EndpointHostService</c> so its scan completes before the RPC
/// dispatcher begins accepting connections.
/// </summary>
internal sealed class WorkspaceDetectionService : IHostedService
{
    private readonly WorkspaceContextDetector _detector;

    /// <summary>
    /// Creates a new <see cref="WorkspaceDetectionService"/>.
    /// </summary>
    /// <param name="detector">Detector this service scans and whose
    /// watcher it arms.</param>
    /// <exception cref="ArgumentNullException"><paramref name="detector"/>
    /// is <see langword="null"/>.</exception>
    public WorkspaceDetectionService(WorkspaceContextDetector detector)
    {
        ArgumentNullException.ThrowIfNull(detector);

        _detector = detector;
    }

    /// <summary>
    /// Runs the initial workspace scan so the detection result is
    /// populated, then arms the filesystem watcher for incremental
    /// updates.
    /// </summary>
    /// <param name="cancellationToken">Cancels the initial scan.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _detector.DetectAsync(cancellationToken).ConfigureAwait(false);
        _detector.Watch();
    }

    /// <summary>
    /// No-op: the detector singleton is disposed by the container on host
    /// stop, which tears down its watcher.
    /// </summary>
    /// <param name="cancellationToken">Ignored.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
