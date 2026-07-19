namespace AutoContext.Engine.Core.McpServer;

using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Server;

/// <summary>
/// Composes the MCP SDK stdio server onto the reduced service collection of
/// the <c>--mcp-server with-stdio</c> role. Registers the
/// <see cref="McpSdkAdapter"/> and routes the SDK's protocol-level
/// <c>tools/list</c> and <c>tools/call</c> handlers to it, so a future
/// non-stdio transport (an HTTP MCP server) would be the sibling entry point
/// over the same adapter.
/// </summary>
internal static class StdioMcpServerEntryPoint
{
    /// <summary>
    /// Registers <c>AddMcpServer().WithStdioServerTransport()</c> and wires
    /// its <c>tools/list</c> / <c>tools/call</c> handlers to the
    /// <see cref="McpSdkAdapter"/>.
    /// </summary>
    /// <param name="services">The reduced service collection.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddStdioMcpServer(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<McpSdkAdapter>();

        services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithListToolsHandler((context, cancellationToken) =>
                ResolveAdapter(context).HandleListToolsAsync(context, cancellationToken))
            .WithCallToolHandler((context, cancellationToken) =>
                ResolveAdapter(context).HandleCallToolAsync(context, cancellationToken));

        return services;
    }

    private static McpSdkAdapter ResolveAdapter<TParams>(RequestContext<TParams> context)
        => (context.Server?.Services
                ?? throw new InvalidOperationException("The MCP server request has no service provider."))
            .GetRequiredService<McpSdkAdapter>();
}
