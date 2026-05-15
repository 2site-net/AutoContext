namespace AutoContext.Engine;

using AutoContext.Engine.Core;

/// <summary>
/// Stub composition root for the <c>--mcp-server with-stdio</c>
/// role. The argv parser already enforces the role's strict
/// subset of switches; this factory currently exits non-zero with
/// a one-line stderr diagnostic so callers see a clean "not yet
/// implemented" signal rather than a silent no-op.
/// </summary>
/// <remarks>
/// The real composition lands in
/// <c>docs/autocontext-engine-implementation-plan.md</c> Phase 11
/// — registering <c>AddMcpServer().WithStdioServerTransport()</c>,
/// wiring the per-request <c>.autocontext.json</c> re-read
/// pipeline, and reusing the daemon-side handler classes for
/// <c>Instructions.*</c> and <c>McpTools.*</c>. Until then this
/// stub keeps the binary's role dispatch end-to-end testable
/// without forcing every caller to also build the MCP host.
/// </remarks>
internal static class McpServerHostFactory
{
    public static int Run(EngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // TODO(plan Phase 11): compose AddMcpServer().WithStdioServerTransport()
        // and wire the per-request .autocontext.json re-read pipeline. No pipes
        // are bound, no registry entry is written, no engine.log is opened —
        // see design § Engine binary > MCP-server-only role.
        Console.Error.WriteLine(
            "autocontext-engine: --mcp-server with-stdio role is not implemented yet (Phase 11). "
            + $"Workspace: '{options.WorkspacePath}'.");
        return 1;
    }
}
