namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// The override-resolution selector a caller passes to
/// <see cref="InstructionsMethods.GetRaw"/> via
/// <see cref="JsonInstructionsGetRawParams.Source"/>. Unlike the
/// concrete <see cref="InstructionsSource"/>, this adds the
/// <see cref="Active"/> selector — the default that mirrors the
/// projection rule the rest of the surface uses.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Active"/> is the zero value so a request that omits
/// <c>source</c> resolves to "the same content the engine would
/// project from". Callers whose byte offsets must align with a
/// specific on-disk file (CodeLens, "open instruction source") pass
/// <see cref="Bundled"/> or <see cref="Override"/> explicitly.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<InstructionsRawSource>))]
public enum InstructionsRawSource
{
    /// <summary>
    /// Default. Returns the override if one exists, else the bundled
    /// file — the projection rule the rest of the surface uses.
    /// </summary>
    [JsonStringEnumMemberName("active")]
    Active,

    /// <summary>Returns the bundled file even when an override exists.</summary>
    [JsonStringEnumMemberName("bundled")]
    Bundled,

    /// <summary>
    /// Returns the override, or <c>not-found</c> when none exists.
    /// </summary>
    [JsonStringEnumMemberName("override")]
    Override,
}
