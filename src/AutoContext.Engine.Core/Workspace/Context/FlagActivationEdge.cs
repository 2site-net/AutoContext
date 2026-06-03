namespace AutoContext.Engine.Core.Workspace.Context;

/// <summary>
/// A single activation edge in the flag cascade: when the
/// <paramref name="Child"/> flag is set, the <paramref name="Parent"/>
/// flag is implied and set too (e.g. <c>hasNextJs</c> implies
/// <c>hasReact</c>). Pure data — the cascade walk lives in the detector.
/// </summary>
/// <param name="Child">The flag whose presence implies
/// <paramref name="Parent"/>.</param>
/// <param name="Parent">The flag activated when
/// <paramref name="Child"/> is set.</param>
internal sealed record FlagActivationEdge(string Child, string Parent);
