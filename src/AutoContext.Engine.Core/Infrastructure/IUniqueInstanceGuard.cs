namespace AutoContext.Engine.Core.Infrastructure;

/// <summary>
/// Pre-bind sanity check that asserts no other engine is currently
/// bound to the endpoints this engine is about to claim. Enforces
/// the per-launch-UUID contract (P4): every launcher spawn must
/// mint a fresh <c>--instance-id</c>; reusing an id while another
/// engine is alive is a launcher bug, not a shape the bind has to
/// be idempotent against.
/// </summary>
/// <remarks>
/// <para>
/// The contract names <i>uniqueness</i> as the invariant; the
/// production implementation
/// (<c>Lifecycle.PerWorkspaceInstanceGuard</c>) scopes uniqueness
/// per workspace by probing the would-be <c>rpc</c> endpoint name
/// derived from <c>WorkspaceHash</c> ×
/// <c>EngineOptions.InstanceId</c>. Other scoping strategies (per
/// machine, per user) would be alternate implementations of this
/// same contract.
/// </para>
/// <para>
/// Called from <c>LifecycleService.StartAsync</c> before any pipe
/// bind. On violation the implementation throws
/// <see cref="System.IO.IOException"/> with a message describing
/// the colliding endpoint and the offending instance id; the
/// generic host treats the failed start as fatal and the process
/// exits non-zero.
/// </para>
/// </remarks>
internal interface IUniqueInstanceGuard
{
    /// <summary>
    /// Probes for a live peer at this engine's would-be endpoint
    /// address. Returns normally when no peer is found; throws
    /// <see cref="System.IO.IOException"/> when a live peer
    /// answers, indicating a launcher-bug instance-id collision.
    /// </summary>
    /// <param name="cancellationToken">Cancellation observed
    /// while probing. The probe is short-lived (sub-second under
    /// normal conditions); cancellation is honoured promptly.</param>
    /// <exception cref="System.IO.IOException">
    /// A live peer was detected at the would-be endpoint address.
    /// The message names the colliding endpoint and the offending
    /// instance id.
    /// </exception>
    /// <exception cref="System.OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled before
    /// the probe completed.
    /// </exception>
    Task EnsureUniqueAsync(CancellationToken cancellationToken);
}
