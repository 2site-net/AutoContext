namespace AutoContext.WorkspaceServer.Hosting;

/// <summary>
/// Minimal envelope used to read the <c>type</c> discriminator before
/// deserializing the full request payload.
/// </summary>
internal sealed record WorkspaceRequest(string? Type);
