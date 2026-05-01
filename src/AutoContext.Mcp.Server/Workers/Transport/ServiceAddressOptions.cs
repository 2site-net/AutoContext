namespace AutoContext.Mcp.Server.Workers.Transport;

using AutoContext.Framework.Workers;

/// <summary>
/// Holds the per-window <see cref="InstanceId"/> the orchestrator
/// received via <c>--instance-id</c> and exposes it as the
/// composition primitive for every outbound service address. When
/// <see cref="InstanceId"/> is null/empty (standalone runs, smoke
/// tests), addresses fall back to the un-suffixed
/// <c>autocontext.&lt;role&gt;</c> form.
/// </summary>
/// <remarks>
/// Mirrors the TypeScript-side <c>IdentifierFactory</c> in the
/// extension. Every process in one VS Code window receives the same
/// <see cref="InstanceId"/>, so all of them format the same address
/// for any given role.
/// </remarks>
public sealed class ServiceAddressOptions
{
    /// <summary>
    /// Per-window identifier propagated from the extension. When null
    /// or empty, addresses are formatted without an instance suffix.
    /// </summary>
    public string? InstanceId { get; init; }

    /// <summary>
    /// Returns the canonical service address for <paramref name="role"/>
    /// using the configured <see cref="InstanceId"/>.
    /// </summary>
    public string Format(string role) =>
        ServiceAddressFormatter.Format(role, InstanceId);
}
