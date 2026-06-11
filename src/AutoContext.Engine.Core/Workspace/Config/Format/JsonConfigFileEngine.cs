namespace AutoContext.Engine.Core.Workspace.Config.Format;

using System.Text.Json.Serialization;

/// <summary>
/// Optional <c>engine</c> block of <c>.autocontext.json</c>. Holds the
/// engine-only settings that don't belong to any instructions file or
/// MCP tool, carried through verbatim on load and save so the engine
/// never drops them when it rewrites the file.
/// </summary>
/// <param name="InstructionsOverridesRoots">Workspace-relative directories,
/// in precedence order, whose <c>instructions/</c> subfolder the engine
/// watches for <c>*.instructions.md</c> overrides. Absent when the user
/// never set it, in which case the engine applies its default
/// (<c>.github</c>).</param>
internal sealed record JsonConfigFileEngine(
    [property: JsonPropertyName("instructions.overridesRoots")] IReadOnlyList<string>? InstructionsOverridesRoots = null);
