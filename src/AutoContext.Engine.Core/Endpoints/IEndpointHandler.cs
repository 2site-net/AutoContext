namespace AutoContext.Engine.Core.Endpoints;

using AutoContext.Engine.Protocol;

/// <summary>
/// Handles a single accepted connection for one
/// <see cref="EndpointKind"/>. <see cref="EndpointHostService"/>
/// owns the accept loops and delegates each accepted stream whole to
/// the handler registered for the connection's kind.
/// </summary>
/// <remarks>
/// <para>
/// The <c>cancellationToken</c> passed to <see cref="HandleAsync"/>
/// is the accept-loop stop token: it signals that the host is
/// shutting down and governs connection <em>establishment</em>
/// (e.g. the <c>Engine.Hello</c> handshake). It does <em>not</em>
/// uniformly tear down a handler's steady-state work. The
/// <c>events</c> and <c>logs</c> writer pumps deliberately stop
/// observing it once streaming begins and instead watch the
/// shared <see cref="ShutdownDrainDeadline"/>, so that a queued
/// terminal frame still reaches the wire during graceful stop.
/// Cancelling the token therefore does not, on its own, abort a
/// draining pump — that is the deadline's job. Handlers with no
/// establishment phase (such as <c>logs</c>) may ignore the token
/// entirely.
/// </para>
/// </remarks>
internal interface IEndpointHandler
{
    /// <summary>
    /// The endpoint kind this handler services. The host builds its
    /// kind-to-handler map from this value, so each kind must be
    /// claimed by at most one registered handler.
    /// </summary>
    EndpointKind Kind { get; }

    /// <summary>
    /// Drives one accepted connection to completion.
    /// </summary>
    /// <param name="stream">Connected pipe stream. The caller owns
    /// the stream lifetime; the handler neither closes nor disposes
    /// it.</param>
    /// <param name="cancellationToken">Accept-loop stop token; see
    /// the type remarks for its scope and the drain-deadline
    /// caveat.</param>
    /// <returns>A task that completes when the connection has been
    /// fully serviced.</returns>
    Task HandleAsync(Stream stream, CancellationToken cancellationToken);
}
