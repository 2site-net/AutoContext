namespace AutoContext.Mcp.Server.Tests.Support.Shared;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builds an empty <see cref="ServiceProvider"/> for tests that need
/// to supply an <see cref="IServiceProvider"/> but don't depend on any
/// resolved service.
/// </summary>
internal static class EmptyTestServiceProvider
{
    public static ServiceProvider EmptyServices() =>
        new ServiceCollection().BuildServiceProvider();
}
