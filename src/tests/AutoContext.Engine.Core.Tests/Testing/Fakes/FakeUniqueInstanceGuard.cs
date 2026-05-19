namespace AutoContext.Engine.Core.Tests.Testing.Fakes;

using AutoContext.Engine.Core.Infrastructure;

/// <summary>
/// Test stand-in for <see cref="IUniqueInstanceGuard"/> that
/// always reports the would-be address as free. Used by every
/// <see cref="Core.Lifecycle.LifecycleService"/> test in this
/// suite so the pre-bind probe doesn't dial a real pipe (which
/// would otherwise race or interact with co-running tests).
/// </summary>
internal sealed class FakeUniqueInstanceGuard : IUniqueInstanceGuard
{
    public Task EnsureUniqueAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
