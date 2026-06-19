namespace AutoContext.Engine.Core.Rpc.Policies;

using AutoContext.Engine.Core.Rpc.Handlers;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Constructs a fresh <see cref="DispatchPolicy"/> for each accepted
/// RPC connection.
/// </summary>
/// <remarks>
/// <para>
/// A new <see cref="DispatchPolicy"/> is created per connection so each
/// connection gets its own router instance. This factory holds the
/// shared, DI-resolved dependencies — the lifetime and the registered
/// method handlers — so the RPC endpoint host does not have to forward
/// them through its own constructor; it simply calls <see cref="Create"/>
/// whenever a connection arrives.
/// </para>
/// </remarks>
internal sealed class DispatchPolicyFactory
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IEnumerable<IRpcMethodHandler> _methodHandlers;
    private readonly ILogger<DispatchPolicy> _logger;

    public DispatchPolicyFactory(
        IHostApplicationLifetime lifetime,
        IEnumerable<IRpcMethodHandler> methodHandlers,
        ILogger<DispatchPolicy> logger)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(methodHandlers);
        ArgumentNullException.ThrowIfNull(logger);

        _lifetime = lifetime;
        _methodHandlers = methodHandlers;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new <see cref="DispatchPolicy"/> bound to the shared
    /// engine dependencies, ready to serve a single RPC connection.
    /// </summary>
    public DispatchPolicy Create() =>
        new(
            _lifetime,
            _methodHandlers,
            _logger);
}
