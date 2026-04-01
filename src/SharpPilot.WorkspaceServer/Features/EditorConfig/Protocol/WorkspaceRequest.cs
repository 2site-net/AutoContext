namespace SharpPilot.WorkspaceServer.Features.EditorConfig.Protocol;

/// <summary>
/// Minimal envelope used to read the <c>type</c> discriminator before
/// deserializing the full request payload.
/// </summary>
internal sealed record WorkspaceRequest(string? Type);
